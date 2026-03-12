using HashChallenge.Domain.Entities;
using HashChallenge.Domain.Interfaces;
using HashChallenge.Domain.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace HashChallenge.Domain.Tests;

public sealed class HashServiceTests
{
    private readonly IHashGenerator _hashGenerator = Substitute.For<IHashGenerator>();
    private readonly IHashPublisher _hashPublisher = Substitute.For<IHashPublisher>();
    private readonly IHashRepository _hashRepository = Substitute.For<IHashRepository>();
    private readonly HashService _sut;

    public HashServiceTests()
    {
        _sut = new HashService(_hashGenerator, _hashPublisher, _hashRepository);
    }

    [Fact]
    public async Task GenerateAndPublishAsync_GeneratesAndPublishes()
    {
        var hashes = new List<HashEntry>
        {
            new() { Date = DateOnly.FromDateTime(DateTime.UtcNow), Sha1 = "a".PadRight(40, 'a') },
            new() { Date = DateOnly.FromDateTime(DateTime.UtcNow), Sha1 = "b".PadRight(40, 'b') },
        };

        _hashGenerator.Generate(2).Returns(hashes);
        _hashPublisher.PublishAsync(hashes, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        int result = await _sut.GenerateAndPublishAsync(2);

        Assert.Equal(2, result);
        _hashGenerator.Received(1).Generate(2);
        await _hashPublisher.Received(1).PublishAsync(hashes, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAndPublishAsync_CallsGenerateWithCorrectCount()
    {
        _hashGenerator.Generate(40000).Returns(new List<HashEntry>());

        await _sut.GenerateAndPublishAsync(40000);

        _hashGenerator.Received(1).Generate(40000);
    }

    [Fact]
    public async Task GenerateAndPublishAsync_WhenPublisherFails_ThrowsException()
    {
        _hashGenerator.Generate(Arg.Any<int>()).Returns(new List<HashEntry>());
        _hashPublisher.PublishAsync(Arg.Any<IReadOnlyList<HashEntry>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Broker down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.GenerateAndPublishAsync(10));
    }

    [Fact]
    public async Task GetDailyCountsAsync_DelegatesToRepository()
    {
        var expected = new List<HashDailyCount>
        {
            new() { Date = new DateOnly(2026, 3, 9), Count = 100 },
        };

        _hashRepository.GetDailyCountsAsync(Arg.Any<CancellationToken>()).Returns(expected);

        IReadOnlyList<HashDailyCount> result = await _sut.GetDailyCountsAsync();

        Assert.Equal(expected, result);
        await _hashRepository.Received(1).GetDailyCountsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetDailyCountsAsync_WhenEmpty_ReturnsEmptyList()
    {
        _hashRepository.GetDailyCountsAsync(Arg.Any<CancellationToken>()).Returns(new List<HashDailyCount>());

        IReadOnlyList<HashDailyCount> result = await _sut.GetDailyCountsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public void Constructor_ThrowsOnNullGenerator()
    {
        Assert.Throws<ArgumentNullException>(() => new HashService(null!, _hashPublisher, _hashRepository));
    }

    [Fact]
    public void Constructor_ThrowsOnNullPublisher()
    {
        Assert.Throws<ArgumentNullException>(() => new HashService(_hashGenerator, null!, _hashRepository));
    }

    [Fact]
    public void Constructor_ThrowsOnNullRepository()
    {
        Assert.Throws<ArgumentNullException>(() => new HashService(_hashGenerator, _hashPublisher, null!));
    }
}
