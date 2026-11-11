using System.Text.Json.Serialization;
using Domain.Documents;

namespace Domain.Exports;

[JsonConverter(typeof(JsonStringEnumConverter<ExportFormat>))]
public enum ExportFormat
{
    [JsonStringEnumMemberName("docx")] Docx,
    [JsonStringEnumMemberName("pdf")] Pdf,
}

[JsonConverter(typeof(JsonStringEnumConverter<ExportStatus>))]
public enum ExportStatus
{
    [JsonStringEnumMemberName("queued")] Queued,
    [JsonStringEnumMemberName("running")] Running,
    [JsonStringEnumMemberName("succeeded")] Succeeded,
    [JsonStringEnumMemberName("failed")] Failed,

    /// <summary>The file was removed after its retention period.</summary>
    [JsonStringEnumMemberName("expired")] Expired,
}

/// <summary>
/// One request to export a work. It keeps a snapshot of the document as it was when requested, so
/// later edits never change a file that is already queued or generated.
/// </summary>
public sealed class ExportJob
{
    private ExportJob() { } // EF Core

    public Guid Id { get; private set; }
    public Guid WorkId { get; private set; }
    public Guid UserId { get; private set; }
    public ExportFormat Format { get; private set; }
    public ExportStatus Status { get; private set; }

    /// <summary>Work version the snapshot was taken from.</summary>
    public int DocumentVersion { get; private set; }

    public DocumentModel Document { get; private set; } = null!;

    /// <summary>Suggested download name (without path), e.g. "formatacao-automatica.pdf".</summary>
    public string FileName { get; private set; } = "";

    /// <summary>Storage key of the generated file (set when succeeded).</summary>
    public string? FileKey { get; private set; }

    public long? FileSize { get; private set; }

    /// <summary>User-facing reason when <see cref="Status"/> is failed.</summary>
    public string? Error { get; private set; }

    /// <summary>Content that could not be rendered (user-facing).</summary>
    public IReadOnlyList<string> Warnings { get; private set; } = [];

    public int Attempts { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>When the file stops being available.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public string ContentType => Format == ExportFormat.Pdf
        ? "application/pdf"
        : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public static ExportJob Create(Guid userId, Guid workId, int documentVersion, DocumentModel document,
        ExportFormat format, string baseFileName, DateTimeOffset now) => new()
        {
            Id = Guid.CreateVersion7(now),
            UserId = userId,
            WorkId = workId,
            DocumentVersion = documentVersion,
            Document = document,
            Format = format,
            FileName = $"{baseFileName}.{(format == ExportFormat.Pdf ? "pdf" : "docx")}",
            Status = ExportStatus.Queued,
            CreatedAt = now,
        };

    public void Start(DateTimeOffset now)
    {
        Status = ExportStatus.Running;
        StartedAt = now;
        Attempts++;
    }

    /// <summary>Back to the queue (e.g. the server stopped while the job was running).</summary>
    public void Requeue()
    {
        if (Status is ExportStatus.Running or ExportStatus.Queued) Status = ExportStatus.Queued;
    }

    public void Succeed(string fileKey, long fileSize, IReadOnlyList<string> warnings, DateTimeOffset now, TimeSpan retention)
    {
        Status = ExportStatus.Succeeded;
        FileKey = fileKey;
        FileSize = fileSize;
        Warnings = warnings;
        CompletedAt = now;
        ExpiresAt = now + retention;
        Error = null;
    }

    public void Fail(string error, DateTimeOffset now)
    {
        Status = ExportStatus.Failed;
        Error = error;
        CompletedAt = now;
    }

    public void Expire()
    {
        Status = ExportStatus.Expired;
        FileKey = null;
    }
}
