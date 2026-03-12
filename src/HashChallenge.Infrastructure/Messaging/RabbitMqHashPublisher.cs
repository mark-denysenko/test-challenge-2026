using System.Text;
using System.Text.Json;
using HashChallenge.Domain.Entities;
using HashChallenge.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace HashChallenge.Infrastructure.Messaging;

public sealed class RabbitMqHashPublisher : IHashPublisher, IDisposable
{
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqHashPublisher> _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public RabbitMqHashPublisher(IOptions<RabbitMqSettings> settings, ILogger<RabbitMqHashPublisher> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var factory = new ConnectionFactory
        {
            HostName = _settings.Host,
            Port = _settings.Port,
            UserName = _settings.Username,
            Password = _settings.Password,
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: _settings.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _channel.ConfirmSelect();
    }

    public Task PublishAsync(IReadOnlyList<HashEntry> hashes, CancellationToken ct = default)
    {
        IBasicProperties properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";

        foreach (HashEntry hash in hashes)
        {
            ct.ThrowIfCancellationRequested();

            var message = new HashMessage
            {
                Sha1 = hash.Sha1,
                Date = hash.Date,
            };

            byte[] body = JsonSerializer.SerializeToUtf8Bytes(message);

            _channel.BasicPublish(
                exchange: string.Empty,
                routingKey: _settings.QueueName,
                basicProperties: properties,
                body: body);
        }

        _channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(30));

        _logger.LogInformation("Published {Count} hashes to queue {Queue}", hashes.Count, _settings.QueueName);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
