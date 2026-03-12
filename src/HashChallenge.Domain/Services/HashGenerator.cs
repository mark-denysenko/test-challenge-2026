using System.Security.Cryptography;
using HashChallenge.Domain.Entities;
using HashChallenge.Domain.Interfaces;

namespace HashChallenge.Domain.Services;

public sealed class HashGenerator : IHashGenerator
{
    public IReadOnlyList<HashEntry> Generate(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than zero.");
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        var hashes = new List<HashEntry>(count);

        for (int i = 0; i < count; i++)
        {
            byte[] guidBytes = Guid.NewGuid().ToByteArray();
            byte[] hashBytes = SHA1.HashData(guidBytes);
            string sha1Hex = Convert.ToHexString(hashBytes).ToLowerInvariant();

            hashes.Add(new HashEntry
            {
                Date = today,
                Sha1 = sha1Hex,
            });
        }

        return hashes;
    }
}
