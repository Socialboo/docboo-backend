using System.Globalization;
using System.Text;
using Application.Abstractions;
using Application.Assets;
using Application.Documents;
using Application.Common;
using Domain.Exports;
using DTOs.Exports;

namespace Application.Exports;

/// <summary>Use cases for requesting exports, following their status and downloading the files.</summary>
public sealed class ExportService(
    IWorkRepository works,
    IExportJobRepository jobs,
    IExportQueue queue,
    IFileStorage storage,
    IPdfConverter pdfConverter,
    IAssetRepository assets,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    private const int ListLimit = 20;

    public async Task<Result<ExportJobResponse>> CreateAsync(Guid workId, CreateExportRequest request, CancellationToken ct)
    {
        var work = await works.FindAsync(currentUser.UserId, workId, ct);
        if (work is null) return new ServiceError.NotFound();

        var errors = ExportReadinessValidator.Validate(work.Document).ToList();
        errors.AddRange(await MissingImagesAsync(work.Id, work.Document, ct));
        if (request.Format == ExportFormat.Pdf && !pdfConverter.IsAvailable)
            errors.Add(new ValidationError("format", "export.pdfUnavailable", "A geração de PDF não está disponível neste servidor (LibreOffice ausente)."));
        if (errors.Count > 0) return new ServiceError.Invalid(errors);

        var job = ExportJob.Create(currentUser.UserId, work.Id, work.Version, work.Document, request.Format,
            FileBaseName(work.Title), clock.GetUtcNow());
        jobs.Add(job);
        await jobs.SaveChangesAsync(ct);
        await queue.EnqueueAsync(job.Id, ct);
        return ToResponse(job);
    }

    private async Task<IEnumerable<ValidationError>> MissingImagesAsync(Guid workId, Domain.Documents.DocumentModel doc, CancellationToken ct)
    {
        var figures = DocumentWalker.Figures(doc).ToList();
        if (figures.Count == 0) return [];

        var ids = figures.Select(f => Guid.TryParseExact(f.Figure.AssetId, "N", out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty).Distinct().ToList();
        var existing = (await assets.FindManyAsync(workId, ids, ct)).Select(a => a.DocumentId).ToHashSet(StringComparer.Ordinal);

        return figures
            .Where(f => !existing.Contains(f.Figure.AssetId))
            .Select(f => new ValidationError($"{f.Path}.assetId", "export.imageMissing", "A imagem desta figura não foi enviada."))
            .ToList();
    }

    public async Task<Result<ExportJobResponse>> GetAsync(Guid jobId, CancellationToken ct)
    {
        var job = await jobs.FindAsync(currentUser.UserId, jobId, ct);
        return job is null ? new ServiceError.NotFound() : ToResponse(job);
    }

    public async Task<Result<IReadOnlyList<ExportJobResponse>>> ListAsync(Guid workId, CancellationToken ct)
    {
        if (await works.FindAsync(currentUser.UserId, workId, ct) is null) return new ServiceError.NotFound();
        var list = await jobs.ListByWorkAsync(currentUser.UserId, workId, ListLimit, ct);
        return Result<IReadOnlyList<ExportJobResponse>>.Success(list.Select(ToResponse).ToList());
    }

    public async Task<Result<ExportFile>> OpenFileAsync(Guid jobId, CancellationToken ct)
    {
        var job = await jobs.FindAsync(currentUser.UserId, jobId, ct);
        if (job is null) return new ServiceError.NotFound();

        switch (job.Status)
        {
            case ExportStatus.Expired:
                return new ServiceError.Gone("O arquivo expirou. Gere a exportação novamente.");
            case ExportStatus.Failed:
                return new ServiceError.NotReady($"A exportação falhou: {job.Error}");
            case not ExportStatus.Succeeded:
                return new ServiceError.NotReady("O arquivo ainda está sendo gerado.");
        }

        var stream = await storage.OpenReadAsync(job.FileKey!, ct);
        if (stream is null) return new ServiceError.Gone("O arquivo não está mais disponível. Gere a exportação novamente.");
        return new ExportFile(stream, job.ContentType, job.FileName);
    }

    public static ExportJobResponse ToResponse(ExportJob job) => new(
        job.Id,
        job.WorkId,
        job.Format,
        job.Status,
        job.DocumentVersion,
        job.FileName,
        job.FileSize,
        job.Error,
        job.Warnings,
        job.CreatedAt,
        job.StartedAt,
        job.CompletedAt,
        job.ExpiresAt,
        job.Status == ExportStatus.Succeeded ? $"/api/exports/{job.Id}/file" : null);

    /// <summary>ASCII, lowercase, hyphenated file name from the title (e.g. "formatacao-automatica").</summary>
    public static string FileBaseName(string title)
    {
        var normalized = title.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        var lastHyphen = true;
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
                lastHyphen = false;
            }
            else if (!lastHyphen)
            {
                sb.Append('-');
                lastHyphen = true;
            }
            if (sb.Length >= 60) break;
        }
        var name = sb.ToString().Trim('-');
        return name.Length == 0 ? "trabalho" : name;
    }
}

/// <summary>An export file opened for download. The caller disposes <see cref="Content"/>.</summary>
public sealed record ExportFile(Stream Content, string ContentType, string FileName);
