namespace HashChallenge.Api.DTOs;

public sealed class GetHashesResponse
{
    public IReadOnlyList<HashDateCount> Hashes { get; set; } = Array.Empty<HashDateCount>();
}

public sealed class HashDateCount
{
    public string Date { get; set; } = string.Empty;

    public long Count { get; set; }
}
