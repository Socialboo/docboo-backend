using Application.Exports;
using Domain.Exports;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class ExportJobRepository(AppDbContext db) : IExportJobRepository
{
    public void Add(ExportJob job) => db.ExportJobs.Add(job);

    public Task<ExportJob?> FindAsync(Guid userId, Guid jobId, CancellationToken ct) =>
        db.ExportJobs.FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, ct);

    public Task<ExportJob?> FindForProcessingAsync(Guid jobId, CancellationToken ct) =>
        db.ExportJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);

    public async Task<IReadOnlyList<ExportJob>> ListByWorkAsync(Guid userId, Guid workId, int take, CancellationToken ct) =>
        await db.ExportJobs.AsNoTracking()
            .Where(j => j.UserId == userId && j.WorkId == workId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ExportJob>> ListUnfinishedAsync(CancellationToken ct) =>
        await db.ExportJobs
            .Where(j => j.Status == ExportStatus.Queued || j.Status == ExportStatus.Running)
            .OrderBy(j => j.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ExportJob>> ListExpiredAsync(DateTimeOffset now, int take, CancellationToken ct) =>
        await db.ExportJobs
            .Where(j => j.Status == ExportStatus.Succeeded && j.ExpiresAt < now)
            .OrderBy(j => j.ExpiresAt)
            .Take(take)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
