using Application.Assets;
using Domain.Assets;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class AssetRepository(AppDbContext db) : IAssetRepository
{
    public void Add(Asset asset) => db.Assets.Add(asset);

    public Task<Asset?> FindAsync(Guid userId, Guid assetId, CancellationToken ct) =>
        db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == assetId && a.UserId == userId, ct);

    public async Task<IReadOnlyList<Asset>> ListByWorkAsync(Guid userId, Guid workId, CancellationToken ct) =>
        await db.Assets.AsNoTracking()
            .Where(a => a.UserId == userId && a.WorkId == workId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Asset>> FindManyAsync(Guid workId, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Assets.AsNoTracking()
            .Where(a => a.WorkId == workId && ids.Contains(a.Id))
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
