using HashChallenge.Api.DTOs;
using HashChallenge.Domain.Entities;
using HashChallenge.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HashChallenge.Api.Controllers;

[ApiController]
[Route("hashes")]
public sealed class HashesController : ControllerBase
{
    private readonly IHashService _hashService;

    public HashesController(IHashService hashService)
    {
        _hashService = hashService;
    }

    /// <summary>
    /// Generates random SHA1 hashes and enqueues them for processing.
    /// </summary>
    /// <param name="request">Request body with the number of hashes to generate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>202 Accepted with enqueued count.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(PostHashesResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Post([FromBody] PostHashesRequest request, CancellationToken ct)
    {
        int enqueuedCount = await _hashService.GenerateAndPublishAsync(request.Count, ct).ConfigureAwait(false);

        return Accepted(new PostHashesResponse { EnqueuedCount = enqueuedCount });
    }

    /// <summary>
    /// Returns hash counts grouped by day.
    /// </summary>
    /// <returns>200 OK with daily hash counts.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(GetHashesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        IReadOnlyList<HashDailyCount> dailyCounts = await _hashService.GetDailyCountsAsync(ct).ConfigureAwait(false);

        var response = new GetHashesResponse
        {
            Hashes = dailyCounts.Select(d => new HashDateCount
            {
                Date = d.Date.ToString("yyyy-MM-dd"),
                Count = d.Count,
            }).ToList(),
        };

        return Ok(response);
    }
}
