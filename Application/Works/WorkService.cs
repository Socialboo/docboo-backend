using Application.Abstractions;
using Application.Common;
using Application.Documents;
using Application.Exports;
using Domain.Documents;
using Domain.Profiles;
using Domain.Works;
using DTOs.Works;

namespace Application.Works;

/// <summary>Use cases for academic works (CRUD + document save with optimistic concurrency).</summary>
public sealed class WorkService(
    IWorkRepository repository,
    ICurrentUser currentUser,
    INormProfileCatalog profiles,
    DocumentValidator validator,
    IExportJobRepository exportJobs,
    IFileStorage storage,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<WorkSummaryResponse>> ListAsync(CancellationToken ct)
    {
        var works = await repository.ListAsync(currentUser.UserId, ct);
        return works.Select(ToSummary).ToList();
    }

    public async Task<Result<WorkResponse>> GetAsync(Guid id, CancellationToken ct)
    {
        var work = await repository.FindAsync(currentUser.UserId, id, ct);
        return work is null ? new ServiceError.NotFound() : Result<WorkResponse>.Success(ToResponse(work));
    }

    public async Task<Result<WorkResponse>> CreateAsync(CreateWorkRequest request, CancellationToken ct)
    {
        if (profiles.Find(request.ProfileId) is null)
            return new ServiceError.Invalid([new ValidationError("profileId", "profile.unknown", "Perfil de norma desconhecido.")]);

        var title = request.Title is null ? null : DocumentSanitizer.Clean(request.Title, allowNewlines: false).Trim();
        var work = Work.Create(currentUser.UserId, request.ProfileId, title, clock.GetUtcNow());
        repository.Add(work);
        await repository.SaveChangesAsync(ct);
        return ToResponse(work);
    }

    public async Task<Result<SaveDocumentResponse>> SaveDocumentAsync(Guid id, SaveDocumentRequest request, CancellationToken ct)
    {
        var work = await repository.FindAsync(currentUser.UserId, id, ct);
        if (work is null) return new ServiceError.NotFound();
        if (work.Version != request.Version) return new ServiceError.Conflict(work.Version);

        var document = DocumentSanitizer.Sanitize(request.Document);
        var errors = validator.Validate(document, work.ProfileId);
        if (errors.Count > 0) return new ServiceError.Invalid(errors);

        work.ReplaceDocument(document, clock.GetUtcNow());
        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            var current = await repository.FindAsync(currentUser.UserId, id, ct);
            return current is null ? new ServiceError.NotFound() : new ServiceError.Conflict(current.Version);
        }

        return new SaveDocumentResponse(work.Version, work.UpdatedAt, work.Title);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var work = await repository.FindAsync(currentUser.UserId, id, ct);
        if (work is null) return new ServiceError.NotFound();

        // Export jobs are removed by cascade; their files must be removed explicitly.
        var jobs = await exportJobs.ListByWorkAsync(currentUser.UserId, id, int.MaxValue, ct);
        repository.Remove(work);
        await repository.SaveChangesAsync(ct);
        foreach (var key in jobs.Select(j => j.FileKey).OfType<string>())
            await storage.DeleteAsync(key, ct);
        return Result<bool>.Success(true);
    }

    private static WorkSummaryResponse ToSummary(Work w) =>
        new(w.Id, w.Title, w.ProfileId, w.Version, w.CreatedAt, w.UpdatedAt);

    private static WorkResponse ToResponse(Work w) =>
        new(w.Id, w.Title, w.ProfileId, w.Version, w.CreatedAt, w.UpdatedAt, w.Document);
}
