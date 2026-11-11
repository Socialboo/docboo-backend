namespace Domain.Assets;

/// <summary>An image uploaded for a work, referenced by figure blocks through <see cref="Id"/>.</summary>
public sealed class Asset
{
    private Asset() { } // EF Core

    public Guid Id { get; private set; }
    public Guid WorkId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>image/png or image/jpeg, detected from the file content (never trusted from the client).</summary>
    public string ContentType { get; private set; } = "";

    public int Width { get; private set; }
    public int Height { get; private set; }
    public long SizeBytes { get; private set; }

    /// <summary>Original file name, for display only.</summary>
    public string OriginalName { get; private set; } = "";

    public string StorageKey { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Id as used in the document model (<c>figure.assetId</c>).</summary>
    public string DocumentId => Id.ToString("N");

    public static Asset Create(Guid userId, Guid workId, string contentType, int width, int height, long sizeBytes,
        string originalName, DateTimeOffset now)
    {
        var id = Guid.CreateVersion7(now);
        return new Asset
        {
            Id = id,
            UserId = userId,
            WorkId = workId,
            ContentType = contentType,
            Width = width,
            Height = height,
            SizeBytes = sizeBytes,
            OriginalName = originalName,
            StorageKey = $"asset-{id:N}{(contentType == "image/png" ? ".png" : ".jpg")}",
            CreatedAt = now,
        };
    }
}

/// <summary>Image content handed to the document generator.</summary>
public sealed record ImageData(byte[] Content, string ContentType, int Width, int Height);
