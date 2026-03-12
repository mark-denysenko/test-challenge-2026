using HashChallenge.Domain.Entities;
using HashChallenge.Domain.Interfaces;
using HashChallenge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HashChallenge.Infrastructure.Repositories;

public sealed class HashRepository : IHashRepository
{
    private readonly HashDbContext _dbContext;

    public HashRepository(HashDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task SaveAsync(HashEntry hash, CancellationToken ct = default)
    {
        _dbContext.Hashes.Add(hash);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<HashDailyCount>> GetDailyCountsAsync(CancellationToken ct = default)
    {
        return await _dbContext.Hashes
            .GroupBy(h => h.Date)
            .Select(g => new HashDailyCount
            {
                Date = g.Key,
                Count = g.LongCount(),
            })
            .OrderBy(d => d.Date)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
