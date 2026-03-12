using HashChallenge.Domain.Interfaces;
using HashChallenge.Infrastructure.Data;
using HashChallenge.Infrastructure.Messaging;
using HashChallenge.Infrastructure.Repositories;
using HashChallenge.Processor;
using Microsoft.EntityFrameworkCore;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Configuration
        services.Configure<RabbitMqSettings>(context.Configuration.GetSection("RabbitMQ"));

        // Database
        string connectionString = context.Configuration.GetConnectionString("HashDb")
            ?? throw new InvalidOperationException("Connection string 'HashDb' is not configured.");

        services.AddDbContext<HashDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        // Infrastructure services
        services.AddScoped<IHashRepository, HashRepository>();
        services.AddSingleton<IHashConsumer, RabbitMqHashConsumer>();

        // Worker
        services.AddHostedService<HashProcessorWorker>();
    })
    .Build();

await host.RunAsync().ConfigureAwait(false);
