using HashChallenge.Api.Controllers;
using HashChallenge.Api.DTOs;
using HashChallenge.Domain.Entities;
using HashChallenge.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RabbitMQ.Client.Exceptions;

namespace HashChallenge.Api.Tests;

public sealed class HashesControllerTests
{
    private readonly IHashService _hashService = Substitute.For<IHashService>();
    private readonly HashesController _sut;

    public HashesControllerTests()
    {
        _sut = new HashesController(_hashService);
    }

    [Fact]
    public async Task Post_Returns202Accepted_WithEnqueuedCount()
    {
        _hashService.GenerateAndPublishAsync(100, Arg.Any<CancellationToken>()).Returns(100);

        var request = new PostHashesRequest { Count = 100 };
        IActionResult result = await _sut.Post(request, CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var response = Assert.IsType<PostHashesResponse>(accepted.Value);
        Assert.Equal(100, response.EnqueuedCount);
    }

    [Fact]
    public async Task Post_CallsServiceWithRequestCount()
    {
        _hashService.GenerateAndPublishAsync(5000, Arg.Any<CancellationToken>()).Returns(5000);

        var request = new PostHashesRequest { Count = 5000 };
        await _sut.Post(request, CancellationToken.None);

        await _hashService.Received(1).GenerateAndPublishAsync(5000, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Post_WhenServiceThrows_ExceptionPropagates()
    {
        _hashService.GenerateAndPublishAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BrokerUnreachableException(new Exception("Connection failed")));

        var request = new PostHashesRequest { Count = 100 };
        await Assert.ThrowsAsync<BrokerUnreachableException>(() => _sut.Post(request, CancellationToken.None));
    }

    [Fact]
    public async Task Get_ReturnsOk_WithDailyCounts()
    {
        var dailyCounts = new List<HashDailyCount>
        {
            new() { Date = new DateOnly(2026, 3, 9), Count = 40000 },
            new() { Date = new DateOnly(2026, 3, 10), Count = 80000 },
        };

        _hashService.GetDailyCountsAsync(Arg.Any<CancellationToken>()).Returns(dailyCounts);

        IActionResult result = await _sut.Get(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GetHashesResponse>(okResult.Value);
        Assert.Equal(2, response.Hashes.Count);
        Assert.Equal("2026-03-09", response.Hashes[0].Date);
        Assert.Equal(40000, response.Hashes[0].Count);
    }

    [Fact]
    public async Task Get_WhenNoDailyData_ReturnsEmptyArray()
    {
        _hashService.GetDailyCountsAsync(Arg.Any<CancellationToken>()).Returns(new List<HashDailyCount>());

        IActionResult result = await _sut.Get(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GetHashesResponse>(okResult.Value);
        Assert.Empty(response.Hashes);
    }

    [Fact]
    public async Task Get_DateFormat_IsCorrect()
    {
        var dailyCounts = new List<HashDailyCount>
        {
            new() { Date = new DateOnly(2022, 6, 25), Count = 100 },
        };

        _hashService.GetDailyCountsAsync(Arg.Any<CancellationToken>()).Returns(dailyCounts);

        IActionResult result = await _sut.Get(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GetHashesResponse>(okResult.Value);
        Assert.Equal("2022-06-25", response.Hashes[0].Date);
    }
}
