using Application.Abstractions;
using Application.Common;
using Application.Exports;
using Domain.Assets;
using DTOs.Assets;
using Microsoft.Extensions.Options;

namespace Application.Assets;

/// <summary>Configuration section <c>Assets</c>.</summary>
public sealed class AssetOptions
{
    public const string SectionName = "Assets";

    /// <summary>Hard cap of the upload request, enforced by the HTTP layer.</summary>
    public const int MaxRequestBytes = 12 * 1024 * 1024;

    public long MaxBytes { get; set; } = 10 * 1024 * 1024;
    public int MaxDimensionPixels { get; set; } = 10_000;
}

public interface IAssetRepository
{
    void Add(Asset asset);

    Task<Asset?> FindAsync(Guid userId, Guid assetId, CancellationToken ct);

    Task<IReadOnlyList<Asset>> ListByWorkAsync(Guid userId, Guid workId, CancellationToken ct);

    /// <summary>Assets of a work with the given ids (unknown ids are simply absent).</summary>
    Task<IReadOnlyList<Asset>> FindManyAsync(Guid workId, IReadOnlyCollection<Guid> ids, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Image uploads for figures.</summary>
public sealed class AssetService(
    IWorkRepository works,
    IAssetRepository assets,
    IFileStorage storage,
    ICurrentUser currentUser,
    IOptions<AssetOptions> options,
    TimeProvider clock)
{
    private readonly AssetOptions _options = options.Value;

    public async Task<Result<AssetResponse>> UploadAsync(Guid workId, Stream content, string? fileName, CancellationToken ct)
    {
        if (await works.FindAsync(currentUser.UserId, workId, ct) is null) return new ServiceError.NotFound();

        // Read at most MaxBytes + 1 to detect oversized files without buffering them whole.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > _options.MaxBytes)
                return Invalid("file.tooLarge", $"A imagem excede {_options.MaxBytes / (1024 * 1024)} MiB.");
        }
        if (buffer.Length == 0) return Invalid("file.empty", "Arquivo vazio.");

        var bytes = buffer.ToArray();
        var info = ImageInspector.Inspect(bytes);
        if (info is null) return Invalid("file.type", "Envie uma imagem PNG ou JPEG.");
        if (info.Width > _options.MaxDimensionPixels || info.Height > _options.MaxDimensionPixels)
            return Invalid("file.dimensions", $"A imagem excede {_options.MaxDimensionPixels} pixels em uma das dimensões.");

        var asset = Asset.Create(currentUser.UserId, workId, info.ContentType, info.Width, info.Height, bytes.LongLength,
            SafeName(fileName), clock.GetUtcNow());
        await storage.SaveAsync(asset.StorageKey, bytes, ct);
        assets.Add(asset);
        await assets.SaveChangesAsync(ct);
        return ToResponse(asset);
    }

    public async Task<Result<IReadOnlyList<AssetResponse>>> ListAsync(Guid workId, CancellationToken ct)
    {
        if (await works.FindAsync(currentUser.UserId, workId, ct) is null) return new ServiceError.NotFound();
        var list = await assets.ListByWorkAsync(currentUser.UserId, workId, ct);
        return Result<IReadOnlyList<AssetResponse>>.Success(list.Select(ToResponse).ToList());
    }

    public async Task<Result<ExportFile>> OpenAsync(string assetId, CancellationToken ct)
    {
        if (!Guid.TryParseExact(assetId, "N", out var id)) return new ServiceError.NotFound();
        var asset = await assets.FindAsync(currentUser.UserId, id, ct);
        if (asset is null) return new ServiceError.NotFound();
        var stream = await storage.OpenReadAsync(asset.StorageKey, ct);
        return stream is null ? new ServiceError.Gone("A imagem não está mais disponível.") : new ExportFile(stream, asset.ContentType, asset.OriginalName);
    }

    private static AssetResponse ToResponse(Asset a) =>
        new(a.DocumentId, a.WorkId, a.ContentType, a.Width, a.Height, a.SizeBytes, a.OriginalName, $"/api/assets/{a.DocumentId}", a.CreatedAt);

    private static ServiceError Invalid(string code, string message) =>
        new ServiceError.Invalid([new ValidationError("file", code, message)]);

    private static string SafeName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? "");
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return name.Length == 0 ? "imagem" : name.Length > 120 ? name[..120] : name;
    }
}
