using HashChallenge.Domain.Entities;

namespace HashChallenge.Domain.Interfaces;

public interface IHashRepository
{
    Task SaveAsync(HashEntry hash, CancellationToken ct = default);

    Task<IReadOnlyList<HashDailyCount>> GetDailyCountsAsync(CancellationToken ct = default);

    Task MigrateAsync(CancellationToken ct = default);
}
