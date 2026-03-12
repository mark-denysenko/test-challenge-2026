namespace HashChallenge.Infrastructure.Messaging;

public sealed class RabbitMqSettings
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string Username { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string QueueName { get; set; } = "hash-queue";

    public ushort PrefetchCount { get; set; } = 4;

    public int Concurrency { get; set; } = 4;
}
