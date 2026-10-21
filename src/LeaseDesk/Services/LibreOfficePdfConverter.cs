using System.Diagnostics;

namespace LeaseDesk.Services;

public class LibreOfficePdfConverter
{
    public Task<byte[]> PrintPagesAsync(string html, CancellationToken cancellationToken = default) =>
        PrintHtmlAsync(PagePrintHtml.Wrap(html), cancellationToken);

    public async Task<byte[]> ConvertAsync(byte[] docx, CancellationToken cancellationToken = default)
    {
        var soffice = FindSoffice();
        if (soffice is null)
            return await ConvertWithBrowserAsync(docx, cancellationToken);

        var directory = Path.Combine(Path.GetTempPath(), "leasedesk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var docxPath = Path.Combine(directory, "lease.docx");
        var pdfPath = Path.Combine(directory, "lease.pdf");

        try
        {
            await File.WriteAllBytesAsync(docxPath, docx, cancellationToken);
            var start = new ProcessStartInfo
            {
                FileName = soffice,
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("--headless");
            start.ArgumentList.Add("--convert-to");
            start.ArgumentList.Add("pdf");
            start.ArgumentList.Add("--outdir");
            start.ArgumentList.Add(directory);
            start.ArgumentList.Add(docxPath);

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("LibreOffice could not be started.");
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0 || !File.Exists(pdfPath))
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(stderr)
                        ? "LibreOffice could not convert the contract to PDF."
                        : stderr.Trim());

            return await File.ReadAllBytesAsync(pdfPath, cancellationToken);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    public static string? FindSoffice()
    {
        var candidates = new[]
        {
            "soffice",
            "libreoffice",
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
        };

        foreach (var candidate in candidates)
        {
            if (candidate.Contains('\\') || candidate.Contains('/'))
            {
                if (File.Exists(candidate))
                    return candidate;
                continue;
            }

            var path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(path))
                continue;

            foreach (var folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var exe = Path.Combine(folder, OperatingSystem.IsWindows() ? candidate + ".exe" : candidate);
                if (File.Exists(exe))
                    return exe;
            }
        }

        return null;
    }

    private static Task<byte[]> ConvertWithBrowserAsync(byte[] docx, CancellationToken cancellationToken) =>
        PrintHtmlAsync(DocxPrintHtml.Create(docx), cancellationToken);

    private static async Task<byte[]> PrintHtmlAsync(string html, CancellationToken cancellationToken)
    {
        var browser = FindBrowser()
            ?? throw new InvalidOperationException(
                "The PDF could not be created because neither LibreOffice nor Microsoft Edge is available. Download the Word file instead.");

        var directory = Path.Combine(Path.GetTempPath(), "leasedesk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var htmlPath = Path.Combine(directory, "lease.html");
        var pdfPath = Path.Combine(directory, "lease.pdf");
        var profile = Path.Combine(directory, "profile");

        try
        {
            await File.WriteAllTextAsync(htmlPath, html, cancellationToken);
            var start = new ProcessStartInfo
            {
                FileName = browser,
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("--headless");
            start.ArgumentList.Add("--disable-gpu");
            start.ArgumentList.Add("--no-first-run");
            start.ArgumentList.Add("--no-pdf-header-footer");
            start.ArgumentList.Add("--user-data-dir=" + profile);
            start.ArgumentList.Add("--print-to-pdf=" + pdfPath);
            start.ArgumentList.Add(new Uri(htmlPath).AbsoluteUri);

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("The browser could not be started to create the PDF.");
            await process.WaitForExitAsync(cancellationToken);
            if (!File.Exists(pdfPath))
                throw new InvalidOperationException("The PDF could not be created. Download the Word file instead.");

            return await File.ReadAllBytesAsync(pdfPath, cancellationToken);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static string? FindBrowser()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
