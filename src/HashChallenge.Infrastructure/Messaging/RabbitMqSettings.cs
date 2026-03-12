namespace HashChallenge.Infrastructure.Messaging;

public sealed class RabbitMqSettings
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string QueueName { get; set; } = string.Empty;

    public ushort PrefetchCount { get; set; }

    public int Concurrency { get; set; }

    public int ConfirmTimeoutSeconds { get; set; } = 30;
}
