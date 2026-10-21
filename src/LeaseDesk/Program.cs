using LeaseDesk.Components;
using LeaseDesk.Data;
using LeaseDesk.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = SqlitePath.ResolveConnectionString(builder.Configuration);
builder.Services.AddDbContext<LeaseDeskDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddScoped<LeaseContractService>();
builder.Services.AddSingleton<LeaseDocumentService>();
builder.Services.AddSingleton<LibreOfficePdfConverter>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LeaseDeskDbContext>();
    SqlitePath.EnsureDirectory(db.Database.GetConnectionString() ?? connectionString);
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapGet("/contracts/{id:int}/word", async (int id, bool? preview, LeaseContractService contracts, LeaseDocumentService documents) =>
{
    var contract = await contracts.GetAsync(id);
    if (contract is null)
        return Results.NotFound();
    if (preview != true && LeaseDocumentService.MissingFields(contract).Count > 0)
        return Results.BadRequest("Fill in the missing details before downloading.");

    var bytes = documents.CreateWord(contract);
    return Results.File(
        bytes,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        preview == true ? null : LeaseDocumentService.DownloadName(contract, "docx"));
});

app.MapPost("/contracts/{id:int}/word", async (int id, bool pages, HttpRequest request, LeaseContractService contracts, LeaseDocumentService documents) =>
{
    var contract = await contracts.GetAsync(id);
    if (contract is null)
        return Results.NotFound();
    if (LeaseDocumentService.MissingFields(contract).Count > 0)
        return Results.BadRequest("Fill in the missing details before downloading.");

    var bytes = documents.CreateWord(contract);
    if (pages)
    {
        using var reader = new StreamReader(request.Body);
        var html = await reader.ReadToEndAsync();
        var edited = PageHtmlDocument.Create(html);
        if (edited.Length > 0)
            bytes = edited;
    }

    return Results.File(
        bytes,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        LeaseDocumentService.DownloadName(contract, "docx"));
}).DisableAntiforgery();

app.MapPost("/contracts/{id:int}/pdf", async (int id, HttpRequest request, LeaseContractService contracts, LibreOfficePdfConverter pdf) =>
{
    var contract = await contracts.GetAsync(id);
    if (contract is null)
        return Results.NotFound();
    if (LeaseDocumentService.MissingFields(contract).Count > 0)
        return Results.BadRequest("Fill in the missing details before downloading.");

    using var reader = new StreamReader(request.Body);
    var html = await reader.ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(html))
        return Results.BadRequest("The contract pages are not ready to download yet.");

    try
    {
        var bytes = await pdf.PrintPagesAsync(html);
        return Results.File(bytes, "application/pdf", LeaseDocumentService.DownloadName(contract, "pdf"));
    }
    catch (InvalidOperationException exception)
    {
        return Results.Text(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).DisableAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
