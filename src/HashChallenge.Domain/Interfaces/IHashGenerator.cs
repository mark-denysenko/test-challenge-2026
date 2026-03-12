using HashChallenge.Domain.Entities;

namespace HashChallenge.Domain.Interfaces;

public interface IHashGenerator
{
    IReadOnlyList<HashEntry> Generate(int count);
}
