using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HashChallenge.Infrastructure.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HashDbContext>
{
    public HashDbContext CreateDbContext(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        string connectionString = configuration.GetConnectionString("HashDb")
            ?? throw new InvalidOperationException(
                "Connection string 'HashDb' is not configured. " +
                "Set it via appsettings.json or ConnectionStrings__HashDb environment variable.");

        DbContextOptionsBuilder<HashDbContext> optionsBuilder = new();
        optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

        return new HashDbContext(optionsBuilder.Options);
    }
}
