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

// Wait for DB schema to be ready (migrations are applied by the API service)
using (IServiceScope scope = host.Services.CreateScope())
{
    HashDbContext dbContext = scope.ServiceProvider.GetRequiredService<HashDbContext>();
    int retries = 30;
    while (retries > 0)
    {
        try
        {
            await dbContext.Database.CanConnectAsync().ConfigureAwait(false);
            bool tableExists = await dbContext.Database
                .ExecuteSqlRawAsync("SELECT 1 FROM hashes LIMIT 0")
                .ConfigureAwait(false) >= 0;
            break;
        }
        catch
        {
            retries--;
            if (retries == 0) throw;
            await Task.Delay(2000).ConfigureAwait(false);
        }
    }
}

await host.RunAsync().ConfigureAwait(false);
