using System.Text.RegularExpressions;
using Application.Exports;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Infrastructure.Storage;

/// <summary>Configuration section <c>Storage</c>.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Folder for generated files. Relative paths are resolved from the app content root.</summary>
    public string Root { get; set; } = "data/exports";
}

/// <summary>
/// Stores files in a local folder (a Docker volume in containers). Keys are flat, server-generated
/// names; anything that could escape the folder is rejected.
/// </summary>
public sealed partial class DiskFileStorage : IFileStorage
{
    private readonly string _root;

    public DiskFileStorage(IOptions<StorageOptions> options, IHostEnvironment environment)
    {
        _root = Path.GetFullPath(Path.IsPathRooted(options.Value.Root)
            ? options.Value.Root
            : Path.Combine(environment.ContentRootPath, options.Value.Root));
        Directory.CreateDirectory(_root);
    }

    public string Root => _root;

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")]
    private static partial Regex KeyPattern();

    public async Task SaveAsync(string key, byte[] content, CancellationToken ct)
    {
        var path = PathFor(key);
        var temp = path + ".tmp";
        await File.WriteAllBytesAsync(temp, content, ct);
        File.Move(temp, path, overwrite: true); // readers never see a partial file
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string PathFor(string key)
    {
        if (!KeyPattern().IsMatch(key) || key.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException($"Invalid storage key '{key}'.", nameof(key));
        return Path.Combine(_root, key);
    }
}
