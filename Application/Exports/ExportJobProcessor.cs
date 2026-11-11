using Domain.Exports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Exports;

/// <summary>A failure whose message can be shown to the student as-is.</summary>
public sealed class ExportFailedException(string userMessage, Exception? inner = null) : Exception(userMessage, inner);

/// <summary>
/// Executes one export job: .docx generation → optional PDF conversion → storage. Independent of how
/// jobs are scheduled (Channel worker today, Hangfire later).
/// </summary>
public sealed class ExportJobProcessor(
    IExportJobRepository jobs,
    IDocumentGenerator generator,
    IPdfConverter pdfConverter,
    IFileStorage storage,
    IOptions<ExportOptions> options,
    TimeProvider clock,
    ILogger<ExportJobProcessor> logger)
{
    private readonly ExportOptions _options = options.Value;

    public async Task ProcessAsync(Guid jobId, CancellationToken ct)
    {
        var job = await jobs.FindForProcessingAsync(jobId, ct);
        if (job is null || job.Status != ExportStatus.Queued) return;

        if (job.Attempts >= _options.MaxAttempts)
        {
            job.Fail("A exportação foi interrompida várias vezes. Tente novamente.", clock.GetUtcNow());
            await jobs.SaveChangesAsync(CancellationToken.None);
            return;
        }

        job.Start(clock.GetUtcNow());
        await jobs.SaveChangesAsync(ct);
        logger.LogInformation("Export {JobId} ({Format}) started, attempt {Attempt}", job.Id, job.Format, job.Attempts);

        try
        {
            var generated = generator.GenerateDocx(job.Document);
            var content = job.Format == ExportFormat.Pdf
                ? await pdfConverter.ConvertAsync(generated.Content, ct)
                : generated.Content;

            var key = $"{job.Id:N}.{(job.Format == ExportFormat.Pdf ? "pdf" : "docx")}";
            await storage.SaveAsync(key, content, ct);

            job.Succeed(key, content.LongLength, generated.Warnings, clock.GetUtcNow(), TimeSpan.FromHours(_options.RetentionHours));
            await jobs.SaveChangesAsync(CancellationToken.None);
            logger.LogInformation("Export {JobId} succeeded ({Bytes} bytes)", job.Id, content.LongLength);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Server shutting down: leave it for the next start.
            job.Requeue();
            await jobs.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Export {JobId} failed", job.Id);
            job.Fail(ex is ExportFailedException ? ex.Message : "Erro inesperado ao gerar o arquivo.", clock.GetUtcNow());
            await jobs.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>Deletes files past their retention and marks the jobs as expired.</summary>
    public async Task<int> CleanupExpiredAsync(CancellationToken ct)
    {
        var expired = await jobs.ListExpiredAsync(clock.GetUtcNow(), take: 200, ct);
        foreach (var job in expired)
        {
            if (job.FileKey is not null) await storage.DeleteAsync(job.FileKey, ct);
            job.Expire();
        }
        if (expired.Count > 0) await jobs.SaveChangesAsync(ct);
        return expired.Count;
    }
}
