namespace HashChallenge.Domain.Interfaces;

public interface IHashConsumer
{
    Task StartConsumingAsync(CancellationToken ct = default);

    Task StopConsumingAsync(CancellationToken ct = default);
}
