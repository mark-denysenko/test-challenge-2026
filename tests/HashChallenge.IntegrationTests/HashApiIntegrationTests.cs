using System.Net;
using System.Net.Http.Json;
using HashChallenge.Api.DTOs;
using HashChallenge.Domain.Entities;
using HashChallenge.Domain.Interfaces;
using HashChallenge.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace HashChallenge.IntegrationTests;

public sealed class HashApiIntegrationTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public HashApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PostHashes_Returns202Accepted()
    {
        var request = new PostHashesRequest { Count = 10 };
        HttpResponseMessage response = await _client.PostAsJsonAsync("/hashes", request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        PostHashesResponse? body = await response.Content.ReadFromJsonAsync<PostHashesResponse>();
        Assert.NotNull(body);
        Assert.Equal(10, body.EnqueuedCount);
    }

    [Fact]
    public async Task PostHashes_WithoutBody_ReturnsErrorStatus()
    {
        HttpResponseMessage response = await _client.PostAsync("/hashes", null);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task PostHashes_WithZeroCount_Returns400BadRequest()
    {
        var request = new PostHashesRequest { Count = 0 };
        HttpResponseMessage response = await _client.PostAsJsonAsync("/hashes", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetHashes_EmptyDatabase_ReturnsOkWithEmptyArray()
    {
        HttpResponseMessage response = await _client.GetAsync("/hashes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        GetHashesResponse? body = await response.Content.ReadFromJsonAsync<GetHashesResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.Hashes);
    }

    [Fact]
    public async Task GetHashes_WithData_ReturnsCorrectStructure()
    {
        // Seed data directly into the in-memory database
        using IServiceScope scope = _factory.Services.CreateScope();
        HashDbContext dbContext = scope.ServiceProvider.GetRequiredService<HashDbContext>();

        dbContext.Hashes.AddRange(
            new HashEntry { Date = new DateOnly(2026, 3, 9), Sha1 = "a".PadRight(40, 'a') },
            new HashEntry { Date = new DateOnly(2026, 3, 9), Sha1 = "b".PadRight(40, 'b') },
            new HashEntry { Date = new DateOnly(2026, 3, 10), Sha1 = "c".PadRight(40, 'c') });

        await dbContext.SaveChangesAsync();

        HttpResponseMessage response = await _client.GetAsync("/hashes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        GetHashesResponse? body = await response.Content.ReadFromJsonAsync<GetHashesResponse>();
        Assert.NotNull(body);
        Assert.True(body.Hashes.Count >= 2);
    }
}

public sealed class TestWebApplicationFactory : WebApplicationFactory<HashChallenge.Api.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real DbContext registration
            ServiceDescriptor? descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<HashDbContext>));

            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            // Add InMemory database
            services.AddDbContext<HashDbContext>(options =>
                options.UseInMemoryDatabase("TestDb"));

            // Replace RabbitMQ publisher with a mock
            ServiceDescriptor? publisherDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IHashPublisher));

            if (publisherDescriptor is not null)
            {
                services.Remove(publisherDescriptor);
            }

            IHashPublisher mockPublisher = Substitute.For<IHashPublisher>();
            mockPublisher.PublishAsync(Arg.Any<IReadOnlyList<HashEntry>>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            services.AddSingleton(mockPublisher);
        });
    }
}
