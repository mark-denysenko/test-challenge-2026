using HashChallenge.Domain.Entities;

namespace HashChallenge.Domain.Interfaces;

public interface IHashPublisher
{
    Task PublishAsync(IReadOnlyList<HashEntry> hashes, CancellationToken ct = default);
}
