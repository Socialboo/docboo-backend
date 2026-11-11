using Domain.Exports;

namespace DTOs.Exports;

/// <summary>Requests a new export of the work's current document.</summary>
public sealed record CreateExportRequest
{
    /// <summary>docx or pdf.</summary>
    public required ExportFormat Format { get; init; }
}

/// <summary>Status of an export job. Poll until <c>status</c> is succeeded or failed.</summary>
/// <param name="Id">Job id.</param>
/// <param name="WorkId">Exported work.</param>
/// <param name="Format">docx or pdf.</param>
/// <param name="Status">queued, running, succeeded, failed or expired.</param>
/// <param name="DocumentVersion">Work version that was exported.</param>
/// <param name="FileName">Suggested download name.</param>
/// <param name="FileSize">Size in bytes, when succeeded.</param>
/// <param name="Error">Why the export failed (user-facing).</param>
/// <param name="Warnings">Content that could not be rendered (user-facing).</param>
/// <param name="CreatedAt">When the export was requested.</param>
/// <param name="StartedAt">When processing started.</param>
/// <param name="CompletedAt">When processing finished.</param>
/// <param name="ExpiresAt">When the file stops being available.</param>
/// <param name="DownloadUrl">Relative URL of the file, when succeeded.</param>
public sealed record ExportJobResponse(
    Guid Id,
    Guid WorkId,
    ExportFormat Format,
    ExportStatus Status,
    int DocumentVersion,
    string FileName,
    long? FileSize,
    string? Error,
    IReadOnlyList<string> Warnings,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ExpiresAt,
    string? DownloadUrl);
