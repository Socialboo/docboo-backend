using Application.Assets;
using DTOs.Assets;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Images used by figures. The type is detected from the content (PNG or JPEG only).</summary>
[ApiController]
public sealed class AssetsController(AssetService assets) : ControllerBase
{
    /// <summary>Uploads an image for the work. Use the returned <c>id</c> as the figure's <c>assetId</c>.</summary>
    [HttpPost("api/works/{workId:guid}/assets", Name = "UploadAsset")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(AssetOptions.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = AssetOptions.MaxRequestBytes)]
    [ProducesResponseType<AssetResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<AssetResponse>> Upload(Guid workId, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await assets.UploadAsync(workId, stream, file.FileName, ct);
        if (!result.IsSuccess) return this.ToActionResult(result.Error!);
        return CreatedAtRoute("GetAsset", new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Lists the images of a work.</summary>
    [HttpGet("api/works/{workId:guid}/assets", Name = "ListAssets")]
    [ProducesResponseType<IReadOnlyList<AssetResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<AssetResponse>>> List(Guid workId, CancellationToken ct)
    {
        var result = await assets.ListAsync(workId, ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToActionResult(result.Error!);
    }

    /// <summary>Returns the image content.</summary>
    [HttpGet("api/assets/{id}", Name = "GetAsset")]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, "image/png", "image/jpeg")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult> Get(string id, CancellationToken ct)
    {
        var result = await assets.OpenAsync(id, ct);
        if (!result.IsSuccess) return this.ToActionResult(result.Error!);
        Response.Headers.CacheControl = "private, max-age=86400";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(result.Value!.Content, result.Value.ContentType);
    }
}
