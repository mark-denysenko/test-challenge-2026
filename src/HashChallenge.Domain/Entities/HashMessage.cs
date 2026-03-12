namespace HashChallenge.Domain.Entities;

public sealed class HashMessage
{
    public string Sha1 { get; set; } = string.Empty;

    public DateOnly Date { get; set; }
}
