using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Exporter.Pdf;

/// <summary>Configuration section <c>LibreOffice</c>.</summary>
public sealed class LibreOfficeOptions
{
    public const string SectionName = "LibreOffice";

    /// <summary>Path to soffice. Empty: <c>soffice</c> on PATH (Linux/macOS) or the default install folder (Windows).</summary>
    public string? SofficePath { get; set; }

    /// <summary>A conversion running longer than this is killed.</summary>
    public int TimeoutSeconds { get; set; } = 120;
}

public sealed class PdfConversionException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Converts the generated .docx to PDF with LibreOffice headless, so both files come from the same
/// source. Every conversion uses its own temporary LibreOffice user profile (-env:UserInstallation),
/// which allows conversions to run in parallel and never touches a user's profile.
/// </summary>
public sealed class LibreOfficeConverter(IOptions<LibreOfficeOptions> options, ILogger<LibreOfficeConverter>? logger = null)
{
    private readonly LibreOfficeOptions _options = options.Value;
    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;

    public string? SofficePath => Locate(_options.SofficePath);

    public bool IsAvailable => SofficePath is not null;

    public static string? Locate(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return File.Exists(configured) ? configured : null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
            {
                // soffice.com is the console launcher: it waits for the conversion to finish.
                var candidate = Path.Combine(root, "LibreOffice", "program", "soffice.com");
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            const string mac = "/Applications/LibreOffice.app/Contents/MacOS/soffice";
            if (File.Exists(mac)) return mac;
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir, "soffice"))
            .FirstOrDefault(File.Exists);
    }

    public async Task<byte[]> ConvertDocxToPdfAsync(byte[] docx, CancellationToken ct)
    {
        var soffice = SofficePath ?? throw new PdfConversionException("LibreOffice (soffice) não foi encontrado no servidor.");

        var workDir = Path.Combine(Path.GetTempPath(), "docboo-lo-" + Guid.NewGuid().ToString("N"));
        var profileDir = Path.Combine(workDir, "profile");
        var input = Path.Combine(workDir, "document.docx");
        var output = Path.Combine(workDir, "document.pdf");
        Directory.CreateDirectory(workDir);

        try
        {
            await File.WriteAllBytesAsync(input, docx, ct);

            var psi = new ProcessStartInfo(soffice)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workDir,
            };
            foreach (var arg in new[]
                     {
                         "--headless", "--invisible", "--norestore", "--nologo", "--nodefault", "--nolockcheck",
                         $"-env:UserInstallation={new Uri(profileDir + Path.DirectorySeparatorChar).AbsoluteUri}",
                         "--convert-to", "pdf:writer_pdf_Export", "--outdir", workDir, input,
                     })
            {
                psi.ArgumentList.Add(arg);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var stopwatch = Stopwatch.StartNew();
            using var process = Process.Start(psi) ?? throw new PdfConversionException("Falha ao iniciar o LibreOffice.");
            var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                if (ct.IsCancellationRequested) throw;
                throw new PdfConversionException($"A conversão para PDF excedeu {_options.TimeoutSeconds} s.");
            }

            var errorText = (await stderr).Trim();
            _logger.LogInformation("LibreOffice conversion finished in {ElapsedMs} ms with exit code {ExitCode}",
                stopwatch.ElapsedMilliseconds, process.ExitCode);

            if (process.ExitCode != 0 || !File.Exists(output))
            {
                _logger.LogWarning("LibreOffice failed. stdout: {Stdout} stderr: {Stderr}", (await stdout).Trim(), errorText);
                throw new PdfConversionException($"O LibreOffice não gerou o PDF (código {process.ExitCode}).");
            }

            return await File.ReadAllBytesAsync(output, ct);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // already exited
        }
    }

    private void TryDelete(string dir)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(200); // LibreOffice may still hold the profile for a moment on Windows.
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not delete LibreOffice work directory {Dir}", dir);
                return;
            }
        }
    }
}
