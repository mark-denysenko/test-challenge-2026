using HashChallenge.Api.Middleware;
using HashChallenge.Domain.Interfaces;
using HashChallenge.Domain.Services;
using HashChallenge.Infrastructure.Data;
using HashChallenge.Infrastructure.Messaging;
using HashChallenge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMQ"));

// Database
string connectionString = builder.Configuration.GetConnectionString("HashDb")
    ?? throw new InvalidOperationException("Connection string 'HashDb' is not configured.");

builder.Services.AddDbContext<HashDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Domain services
builder.Services.AddSingleton<IHashGenerator, HashGenerator>();
builder.Services.AddScoped<IHashService, HashService>();

// Infrastructure services
builder.Services.AddScoped<IHashRepository, HashRepository>();
builder.Services.AddSingleton<IHashPublisher, RabbitMqHashPublisher>();

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Hash Challenge API",
        Version = "v1",
        Description = "API for generating and querying SHA1 hashes",
    });
});

WebApplication app = builder.Build();

// Apply pending migrations on startup (skip for InMemory provider used in tests)
using (IServiceScope scope = app.Services.CreateScope())
{
    HashDbContext dbContext = scope.ServiceProvider.GetRequiredService<HashDbContext>();
    if (dbContext.Database.IsRelational())
    {
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }
    else
    {
        await dbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);
    }
}

// Middleware
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

await app.RunAsync().ConfigureAwait(false);

// Required for WebApplicationFactory in integration tests
namespace HashChallenge.Api
{
    public partial class Program
    {
    }
}
