using HashChallenge.Domain.Entities;
using HashChallenge.Infrastructure.Data;
using HashChallenge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HashChallenge.Infrastructure.Tests;

public sealed class HashRepositoryTests : IDisposable
{
    private readonly HashDbContext _dbContext;
    private readonly HashRepository _sut;

    public HashRepositoryTests()
    {
        DbContextOptions<HashDbContext> options = new DbContextOptionsBuilder<HashDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new HashDbContext(options);
        _sut = new HashRepository(_dbContext);
    }

    [Fact]
    public async Task SaveAsync_PersistsHashEntry()
    {
        var hash = new HashEntry
        {
            Date = new DateOnly(2026, 3, 9),
            Sha1 = "a94a8fe5ccb19ba61c4c0873d391e987982fbbd3",
        };

        await _sut.SaveAsync(hash);

        HashEntry? saved = await _dbContext.Hashes.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal(hash.Sha1, saved.Sha1);
        Assert.Equal(hash.Date, saved.Date);
    }

    [Fact]
    public async Task SaveAsync_GeneratesId()
    {
        var hash = new HashEntry
        {
            Date = new DateOnly(2026, 3, 9),
            Sha1 = "a94a8fe5ccb19ba61c4c0873d391e987982fbbd3",
        };

        await _sut.SaveAsync(hash);

        HashEntry? saved = await _dbContext.Hashes.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.True(saved.Id > 0);
    }

    [Fact]
    public async Task GetDailyCountsAsync_ReturnsGroupedByDate()
    {
        DateOnly date1 = new(2026, 3, 9);
        DateOnly date2 = new(2026, 3, 10);

        _dbContext.Hashes.AddRange(
            new HashEntry { Date = date1, Sha1 = "a".PadRight(40, 'a') },
            new HashEntry { Date = date1, Sha1 = "b".PadRight(40, 'b') },
            new HashEntry { Date = date2, Sha1 = "c".PadRight(40, 'c') });

        await _dbContext.SaveChangesAsync();

        IReadOnlyList<HashDailyCount> result = await _sut.GetDailyCountsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal(date1, result[0].Date);
        Assert.Equal(2, result[0].Count);
        Assert.Equal(date2, result[1].Date);
        Assert.Equal(1, result[1].Count);
    }

    [Fact]
    public async Task GetDailyCountsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        IReadOnlyList<HashDailyCount> result = await _sut.GetDailyCountsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDailyCountsAsync_OrdersByDateAscending()
    {
        DateOnly earlier = new(2026, 1, 1);
        DateOnly later = new(2026, 6, 15);

        _dbContext.Hashes.AddRange(
            new HashEntry { Date = later, Sha1 = "a".PadRight(40, 'a') },
            new HashEntry { Date = earlier, Sha1 = "b".PadRight(40, 'b') });

        await _dbContext.SaveChangesAsync();

        IReadOnlyList<HashDailyCount> result = await _sut.GetDailyCountsAsync();

        Assert.Equal(earlier, result[0].Date);
        Assert.Equal(later, result[1].Date);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
