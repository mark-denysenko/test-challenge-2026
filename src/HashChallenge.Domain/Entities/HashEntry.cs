namespace HashChallenge.Domain.Entities;

public sealed class HashEntry
{
    public long Id { get; private set; }

    public DateOnly Date { get; set; }

    public string Sha1 { get; set; } = string.Empty;
}
