using System.Text.RegularExpressions;
using HashChallenge.Domain.Entities;
using HashChallenge.Domain.Services;

namespace HashChallenge.Domain.Tests;

public sealed class HashGeneratorTests
{
    private readonly HashGenerator _sut = new();

    [Fact]
    public void Generate_ReturnsCorrectCount()
    {
        IReadOnlyList<HashEntry> result = _sut.Generate(100);

        Assert.Equal(100, result.Count);
    }

    [Fact]
    public void Generate_ProducesValidSha1Strings()
    {
        IReadOnlyList<HashEntry> result = _sut.Generate(10);

        foreach (HashEntry hash in result)
        {
            Assert.Equal(40, hash.Sha1.Length);
            Assert.Matches("^[0-9a-f]{40}$", hash.Sha1);
        }
    }

    [Fact]
    public void Generate_ProducesUniqueHashes()
    {
        IReadOnlyList<HashEntry> result = _sut.Generate(1000);

        HashSet<string> uniqueHashes = new(result.Select(h => h.Sha1));
        Assert.Equal(result.Count, uniqueHashes.Count);
    }

    [Fact]
    public void Generate_SetsDateToToday()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        IReadOnlyList<HashEntry> result = _sut.Generate(5);

        Assert.All(result, h => Assert.Equal(today, h.Date));
    }

    [Fact]
    public void Generate_WithLargeCount_ReturnsAllHashes()
    {
        IReadOnlyList<HashEntry> result = _sut.Generate(40000);

        Assert.Equal(40000, result.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Generate_WithInvalidCount_ThrowsArgumentOutOfRangeException(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.Generate(count));
    }

    [Fact]
    public void Generate_HashesAreLowercase()
    {
        IReadOnlyList<HashEntry> result = _sut.Generate(10);

        Assert.All(result, h => Assert.Equal(h.Sha1, h.Sha1.ToLowerInvariant()));
    }
}
