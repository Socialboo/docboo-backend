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

    /// <summary>Path to soffice. Empty: <c>soffice</c> on PATH (Linux), the default install folder (Windows/macOS).</summary>
    public string? SofficePath { get; set; }

    /// <summary>Python with the <c>uno</c> module. Empty: LibreOffice's bundled Python (Windows/macOS) or python3 (Linux).</summary>
    public string? PythonPath { get; set; }

    /// <summary>A conversion running longer than this is killed.</summary>
    public int TimeoutSeconds { get; set; } = 120;
}

public sealed class PdfConversionException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Converts the generated .docx to PDF with LibreOffice headless, so both files come from the same
/// source. A UNO script (convert_to_pdf.py) updates the table of contents, lists and fields before
/// exporting, which <c>--convert-to</c> alone does not do. Every conversion uses its own LibreOffice
/// user profile and pipe, so conversions can run in parallel and never touch a user's profile.
/// </summary>
public sealed class LibreOfficeConverter(IOptions<LibreOfficeOptions> options, ILogger<LibreOfficeConverter>? logger = null)
{
    private const string ScriptResource = "Exporter.Pdf.convert_to_pdf.py";

    private readonly LibreOfficeOptions _options = options.Value;
    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;

    public string? SofficePath => LocateSoffice(_options.SofficePath);

    public string? PythonPath => LocatePython(_options.PythonPath, SofficePath);

    public bool IsAvailable => SofficePath is not null && PythonPath is not null;

    public static string? LocateSoffice(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return File.Exists(configured) ? configured : null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
            {
                // soffice.com is the console launcher: it stays attached until LibreOffice exits.
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

        return FindOnPath("soffice");
    }

    public static string? LocatePython(string? configured, string? soffice)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return File.Exists(configured) ? configured : null;
        if (soffice is null) return null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var bundled = Path.Combine(Path.GetDirectoryName(soffice)!, "python.exe");
            return File.Exists(bundled) ? bundled : null;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var bundled = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(soffice)!, "..", "Resources", "python"));
            if (File.Exists(bundled)) return bundled;
        }

        // Linux: system python3 with the uno module (Debian/Ubuntu package python3-uno).
        return FindOnPath("python3");
    }

    private static string? FindOnPath(string name) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir, name))
            .FirstOrDefault(File.Exists);

    public async Task<byte[]> ConvertDocxToPdfAsync(byte[] docx, CancellationToken ct)
    {
        var soffice = SofficePath ?? throw new PdfConversionException("LibreOffice (soffice) não foi encontrado no servidor.");
        var python = PythonPath ?? throw new PdfConversionException("O Python do LibreOffice (módulo uno) não foi encontrado no servidor.");

        var id = Guid.NewGuid().ToString("N");
        var workDir = Path.Combine(Path.GetTempPath(), "docboo-lo-" + id);
        var profileDir = Path.Combine(workDir, "profile");
        var input = Path.Combine(workDir, "document.docx");
        var output = Path.Combine(workDir, "document.pdf");
        var script = Path.Combine(workDir, "convert_to_pdf.py");
        var pipe = "docboo_" + id;
        Directory.CreateDirectory(workDir);

        Process? office = null;
        try
        {
            await File.WriteAllBytesAsync(input, docx, ct);
            await WriteScriptAsync(script, ct);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            var stopwatch = Stopwatch.StartNew();

            office = Start(soffice, workDir,
                "--headless", "--invisible", "--norestore", "--nologo", "--nodefault", "--nolockcheck",
                $"-env:UserInstallation={new Uri(profileDir + Path.DirectorySeparatorChar).AbsoluteUri}",
                $"--accept=pipe,name={pipe};urp;StarOffice.ComponentContext");

            using var converter = Start(python, workDir, script, pipe, input, output);
            var stdout = converter.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var stderr = converter.StandardError.ReadToEndAsync(CancellationToken.None);
            try
            {
                await converter.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(converter);
                if (ct.IsCancellationRequested) throw;
                throw new PdfConversionException($"A conversão para PDF excedeu {_options.TimeoutSeconds} s.");
            }

            var log = (await stdout).Trim();
            var errors = (await stderr).Trim();
            _logger.LogInformation("LibreOffice conversion finished in {ElapsedMs} ms (exit {ExitCode}, {Log})",
                stopwatch.ElapsedMilliseconds, converter.ExitCode, log);

            if (converter.ExitCode != 0 || !File.Exists(output))
            {
                _logger.LogWarning("LibreOffice conversion failed. stdout: {Stdout} stderr: {Stderr}", log, errors);
                throw new PdfConversionException($"O LibreOffice não gerou o PDF (código {converter.ExitCode}).");
            }

            // The script asked LibreOffice to terminate; give it a moment before killing it.
            await WaitOrKillAsync(office, TimeSpan.FromSeconds(15));
            return await File.ReadAllBytesAsync(output, ct);
        }
        finally
        {
            if (office is not null)
            {
                TryKill(office);
                office.Dispose();
            }
            TryDelete(workDir);
        }
    }

    private static async Task WriteScriptAsync(string path, CancellationToken ct)
    {
        await using var resource = typeof(LibreOfficeConverter).Assembly.GetManifestResourceStream(ScriptResource)
            ?? throw new InvalidOperationException($"Missing resource {ScriptResource}.");
        await using var file = File.Create(path);
        await resource.CopyToAsync(file, ct);
    }

    private static Process Start(string fileName, string workDir, params string[] args)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workDir,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        return Process.Start(psi) ?? throw new PdfConversionException($"Falha ao iniciar {Path.GetFileName(fileName)}.");
    }

    private static async Task WaitOrKillAsync(Process process, TimeSpan grace)
    {
        using var cts = new CancellationTokenSource(grace);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
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
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 9)
                {
                    _logger.LogWarning(ex, "Could not delete LibreOffice work directory {Dir}", dir);
                    return;
                }
                Thread.Sleep(300); // LibreOffice may still hold the profile for a moment on Windows.
            }
        }
    }
}
