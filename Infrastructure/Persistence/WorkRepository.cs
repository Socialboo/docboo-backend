using Application.Abstractions;
using Domain.Works;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class WorkRepository(AppDbContext db) : IWorkRepository
{
    public async Task<IReadOnlyList<Work>> ListAsync(Guid userId, CancellationToken ct) =>
        await db.Works.AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.UpdatedAt)
            .ToListAsync(ct);

    public Task<Work?> FindAsync(Guid userId, Guid workId, CancellationToken ct) =>
        db.Works.FirstOrDefaultAsync(w => w.Id == workId && w.UserId == userId, ct);

    public void Add(Work work) => db.Works.Add(work);

    public void Remove(Work work) => db.Works.Remove(work);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries) entry.State = EntityState.Detached;
            throw new ConcurrencyConflictException(ex);
        }
    }
}
