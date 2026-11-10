using Domain.Works;

namespace Application.Abstractions;

/// <summary>The user on whose behalf the request runs. MVP: a single configured local user.</summary>
public interface ICurrentUser
{
    Guid UserId { get; }
}

/// <summary>Persistence for <see cref="Work"/>. All queries are scoped to an owner.</summary>
public interface IWorkRepository
{
    Task<IReadOnlyList<Work>> ListAsync(Guid userId, CancellationToken ct);

    Task<Work?> FindAsync(Guid userId, Guid workId, CancellationToken ct);

    void Add(Work work);

    void Remove(Work work);

    /// <exception cref="ConcurrencyConflictException">The row changed since it was loaded.</exception>
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Raised when an optimistic-concurrency check fails during persistence.</summary>
public sealed class ConcurrencyConflictException(Exception? inner = null)
    : Exception("The resource was modified by another request.", inner);
