namespace DTOs.Assets;

/// <summary>An uploaded image.</summary>
/// <param name="Id">Use as <c>assetId</c> in figure blocks.</param>
/// <param name="WorkId">Work the image belongs to.</param>
/// <param name="ContentType">image/png or image/jpeg (detected from the content).</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="SizeBytes">File size.</param>
/// <param name="OriginalName">Uploaded file name (display only).</param>
/// <param name="Url">Relative URL of the image.</param>
/// <param name="CreatedAt">Upload time.</param>
public sealed record AssetResponse(
    string Id,
    Guid WorkId,
    string ContentType,
    int Width,
    int Height,
    long SizeBytes,
    string OriginalName,
    string Url,
    DateTimeOffset CreatedAt);
