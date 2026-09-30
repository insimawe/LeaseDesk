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

app.MapGet("/contracts/{id:int}/word", async (int id, bool preview, LeaseContractService contracts, LeaseDocumentService documents) =>
{
    var contract = await contracts.GetAsync(id);
    if (contract is null)
        return Results.NotFound();
    if (!preview && LeaseDocumentService.MissingFields(contract).Count > 0)
        return Results.BadRequest("Fill in the missing details before downloading.");

    var bytes = documents.CreateWord(contract);
    return Results.File(
        bytes,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        preview ? null : LeaseDocumentService.DownloadName(contract, "docx"));
});

app.MapGet("/contracts/{id:int}/pdf", async (int id, bool download, LeaseContractService contracts, LeaseDocumentService documents, LibreOfficePdfConverter pdf) =>
{
    var contract = await contracts.GetAsync(id);
    if (contract is null)
        return Results.NotFound();
    if (download && LeaseDocumentService.MissingFields(contract).Count > 0)
        return Results.BadRequest("Fill in the missing details before downloading.");

    try
    {
        var docx = documents.CreateWord(contract);
        var bytes = await pdf.ConvertAsync(docx);
        return Results.File(bytes, "application/pdf", download ? LeaseDocumentService.DownloadName(contract, "pdf") : null);
    }
    catch (InvalidOperationException exception)
    {
        return Results.Text(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
