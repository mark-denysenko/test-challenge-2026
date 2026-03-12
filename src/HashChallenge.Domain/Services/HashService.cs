using HashChallenge.Domain.Entities;
using HashChallenge.Domain.Interfaces;

namespace HashChallenge.Domain.Services;

public sealed class HashService : IHashService
{
    private readonly IHashGenerator _hashGenerator;
    private readonly IHashPublisher _hashPublisher;
    private readonly IHashRepository _hashRepository;

    public HashService(
        IHashGenerator hashGenerator,
        IHashPublisher hashPublisher,
        IHashRepository hashRepository)
    {
        _hashGenerator = hashGenerator ?? throw new ArgumentNullException(nameof(hashGenerator));
        _hashPublisher = hashPublisher ?? throw new ArgumentNullException(nameof(hashPublisher));
        _hashRepository = hashRepository ?? throw new ArgumentNullException(nameof(hashRepository));
    }

    public async Task<int> GenerateAndPublishAsync(int count, CancellationToken ct = default)
    {
        IReadOnlyList<HashEntry> hashes = _hashGenerator.Generate(count);
        await _hashPublisher.PublishAsync(hashes, ct).ConfigureAwait(false);
        return hashes.Count;
    }

    public async Task<IReadOnlyList<HashDailyCount>> GetDailyCountsAsync(CancellationToken ct = default)
    {
        return await _hashRepository.GetDailyCountsAsync(ct).ConfigureAwait(false);
    }
}
