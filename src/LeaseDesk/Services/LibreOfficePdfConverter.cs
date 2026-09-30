using System.Diagnostics;

namespace LeaseDesk.Services;

public class LibreOfficePdfConverter
{
    public async Task<byte[]> ConvertAsync(byte[] docx, CancellationToken cancellationToken = default)
    {
        var soffice = FindSoffice()
            ?? throw new InvalidOperationException(
                "LibreOffice is not installed, so the PDF could not be created. The Word file uses the same filled contract.");

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
}
