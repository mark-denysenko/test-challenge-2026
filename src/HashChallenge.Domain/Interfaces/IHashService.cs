using HashChallenge.Domain.Entities;

namespace HashChallenge.Domain.Interfaces;

public interface IHashService
{
    Task<int> GenerateAndPublishAsync(int count, CancellationToken ct = default);

    Task<IReadOnlyList<HashDailyCount>> GetDailyCountsAsync(CancellationToken ct = default);
}
