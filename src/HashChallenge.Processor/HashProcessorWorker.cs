using HashChallenge.Domain.Interfaces;

namespace HashChallenge.Processor;

public sealed class HashProcessorWorker : BackgroundService
{
    private readonly IHashConsumer _hashConsumer;
    private readonly ILogger<HashProcessorWorker> _logger;

    public HashProcessorWorker(IHashConsumer hashConsumer, ILogger<HashProcessorWorker> logger)
    {
        _hashConsumer = hashConsumer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Hash Processor Worker starting");

        await _hashConsumer.StartConsumingAsync(stoppingToken).ConfigureAwait(false);

        // Keep running until cancellation is requested
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Hash Processor Worker stopping");
        await _hashConsumer.StopConsumingAsync(cancellationToken).ConfigureAwait(false);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
