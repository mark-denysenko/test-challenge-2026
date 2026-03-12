namespace HashChallenge.Domain.Entities;

public sealed class HashDailyCount
{
    public DateOnly Date { get; set; }

    public long Count { get; set; }
}
