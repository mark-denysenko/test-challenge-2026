using System.Text;
using System.Text.Json;
using HashChallenge.Domain.Entities;
using HashChallenge.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace HashChallenge.Infrastructure.Messaging;

public sealed class RabbitMqHashConsumer : IHashConsumer, IDisposable
{
    private readonly RabbitMqSettings _settings;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMqHashConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqHashConsumer(
        IOptions<RabbitMqSettings> settings,
        IServiceProvider serviceProvider,
        ILogger<RabbitMqHashConsumer> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartConsumingAsync(CancellationToken ct = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.Host,
            Port = _settings.Port,
            UserName = _settings.Username,
            Password = _settings.Password,
            DispatchConsumersAsync = true,
            ConsumerDispatchConcurrency = _settings.Concurrency,
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: _settings.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _channel.BasicQos(prefetchSize: 0, prefetchCount: _settings.PrefetchCount, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnMessageReceivedAsync;

        _channel.BasicConsume(
            queue: _settings.QueueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation(
            "Started consuming from queue {Queue} with concurrency {Concurrency}",
            _settings.QueueName,
            _settings.Concurrency);

        return Task.CompletedTask;
    }

    public Task StopConsumingAsync(CancellationToken ct = default)
    {
        _channel?.Close();
        _connection?.Close();

        _logger.LogInformation("Stopped consuming from queue {Queue}", _settings.QueueName);

        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            string body = Encoding.UTF8.GetString(ea.Body.Span);
            HashMessage? message = JsonSerializer.Deserialize<HashMessage>(body);

            if (message is null || string.IsNullOrWhiteSpace(message.Sha1))
            {
                _logger.LogWarning("Received malformed message, discarding. Body: {Body}", body);
                _channel?.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            var hashEntry = new HashEntry
            {
                Date = message.Date,
                Sha1 = message.Sha1,
            };

            using IServiceScope scope = _serviceProvider.CreateScope();
            IHashRepository repository = scope.ServiceProvider.GetRequiredService<IHashRepository>();

            await repository.SaveAsync(hashEntry).ConfigureAwait(false);

            _channel?.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize message, discarding. DeliveryTag: {Tag}", ea.DeliveryTag);
            _channel?.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message. DeliveryTag: {Tag}", ea.DeliveryTag);
            _channel?.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
