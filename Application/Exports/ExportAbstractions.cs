using Domain.Documents;
using Domain.Exports;

namespace Application.Exports;

/// <param name="Content">Generated .docx.</param>
/// <param name="Warnings">Content not rendered (user-facing).</param>
public sealed record GeneratedDocument(byte[] Content, IReadOnlyList<string> Warnings);

/// <summary>Generates the .docx (single source of truth) from the document model.</summary>
public interface IDocumentGenerator
{
    GeneratedDocument GenerateDocx(DocumentModel document);
}

/// <summary>Converts a .docx to PDF (LibreOffice in the default implementation).</summary>
public interface IPdfConverter
{
    bool IsAvailable { get; }

    Task<byte[]> ConvertAsync(byte[] docx, CancellationToken ct);
}

/// <summary>Where generated files live. Disk now; MinIO/S3 later.</summary>
public interface IFileStorage
{
    Task SaveAsync(string key, byte[] content, CancellationToken ct);

    /// <returns>null when the file does not exist.</returns>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);

    Task DeleteAsync(string key, CancellationToken ct);
}

/// <summary>
/// Hands job ids to whatever executes them. The default is an in-process Channel + BackgroundService;
/// a Hangfire implementation would enqueue a job that calls <see cref="ExportJobProcessor"/>.
/// </summary>
public interface IExportQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken ct);
}

public interface IExportJobRepository
{
    void Add(ExportJob job);

    Task<ExportJob?> FindAsync(Guid userId, Guid jobId, CancellationToken ct);

    /// <summary>Unscoped lookup for the background worker.</summary>
    Task<ExportJob?> FindForProcessingAsync(Guid jobId, CancellationToken ct);

    Task<IReadOnlyList<ExportJob>> ListByWorkAsync(Guid userId, Guid workId, int take, CancellationToken ct);

    /// <summary>Jobs left queued or running (e.g. after a restart).</summary>
    Task<IReadOnlyList<ExportJob>> ListUnfinishedAsync(CancellationToken ct);

    Task<IReadOnlyList<ExportJob>> ListExpiredAsync(DateTimeOffset now, int take, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Configuration section <c>Export</c>.</summary>
public sealed class ExportOptions
{
    public const string SectionName = "Export";

    /// <summary>Jobs processed at the same time (each PDF conversion runs its own LibreOffice).</summary>
    public int MaxConcurrency { get; set; } = 2;

    /// <summary>Jobs waiting in memory before enqueuing blocks.</summary>
    public int QueueCapacity { get; set; } = 100;

    /// <summary>How long generated files remain downloadable.</summary>
    public int RetentionHours { get; set; } = 24;

    public int CleanupIntervalMinutes { get; set; } = 30;

    /// <summary>Jobs that keep failing because of crashes are not retried forever.</summary>
    public int MaxAttempts { get; set; } = 3;
}
