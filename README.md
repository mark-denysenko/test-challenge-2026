# Hash Challenge

Two .NET 8 applications that generate and process SHA1 hashes via RabbitMQ, persisting them to MariaDB.

## Architecture

- **HashChallenge.Api** — REST API (POST /hashes, GET /hashes)
- **HashChallenge.Processor** — Background worker consuming from RabbitMQ with 4 parallel threads
- **HashChallenge.Domain** — Entities, interfaces, business logic
- **HashChallenge.Infrastructure** — EF Core (MariaDB), RabbitMQ publisher/consumer

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/get-started) & Docker Compose

## Quick Start (Docker Compose)

Run all services with a single command:

```bash
docker-compose up --build
```

This starts:
- **MariaDB** on port 3306
- **RabbitMQ** on port 5672 (management UI: http://localhost:15672, guest/guest)
- **Hash API** on http://localhost:8080
- **Hash Processor** (background worker)

### Test the API

```bash
# Generate and enqueue 40,000 hashes
curl -X POST http://localhost:8080/hashes

# Get hash counts grouped by day
curl http://localhost:8080/hashes
```

### OpenAPI / Swagger

Available at: http://localhost:8080/swagger

## Local Development (without Docker)

### 1. Start dependencies

```bash
# Start RabbitMQ and MariaDB only
docker-compose up rabbitmq mariadb
```

### 2. Run the API

```bash
cd src/HashChallenge.Api
dotnet run
```

### 3. Run the Processor

```bash
cd src/HashChallenge.Processor
dotnet run
```

## Build

```bash
dotnet build HashChallenge.sln
```

## Run Tests

```bash
# All tests
dotnet test HashChallenge.sln

# Unit tests only
dotnet test tests/HashChallenge.Domain.Tests
dotnet test tests/HashChallenge.Api.Tests
dotnet test tests/HashChallenge.Infrastructure.Tests

# Integration tests
dotnet test tests/HashChallenge.IntegrationTests
```

## Project Structure

```
HashChallenge.sln
├── src/
│   ├── HashChallenge.Api/              # REST API (ASP.NET Core)
│   ├── HashChallenge.Processor/        # Background worker
│   ├── HashChallenge.Domain/           # Entities, interfaces, services
│   └── HashChallenge.Infrastructure/   # EF DbContext, repositories, RabbitMQ
├── tests/
│   ├── HashChallenge.Domain.Tests/     # Domain unit tests (9 tests)
│   ├── HashChallenge.Api.Tests/        # Controller unit tests (7 tests)
│   ├── HashChallenge.Infrastructure.Tests/  # Repository unit tests (5 tests)
│   └── HashChallenge.IntegrationTests/ # API integration tests (3 tests)
├── docker-compose.yml
└── README.md
```

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `ConnectionStrings__HashDb` | (see appsettings.json) | MariaDB connection string |
| `RabbitMQ__Host` | `localhost` | RabbitMQ hostname |
| `RabbitMQ__Port` | `5672` | RabbitMQ port |
| `RabbitMQ__Username` | `guest` | RabbitMQ username |
| `RabbitMQ__Password` | `guest` | RabbitMQ password |
| `RabbitMQ__QueueName` | `hash-queue` | Queue name |
| `RabbitMQ__PrefetchCount` | `4` | Consumer prefetch count |
| `RabbitMQ__Concurrency` | `4` | Consumer thread count |
| `Hash__GenerateCount` | `40000` | Hashes generated per POST |

## Database

- **Engine:** MariaDB 11
- **Table:** `hashes` (id BIGINT PK, date DATE, sha1 CHAR(40))
- **Index:** `IX_hashes_Date` on `date` column for efficient GROUP BY
- **Migrations:** Applied automatically on application startup

## Technology Stack

- .NET 8, C#
- MariaDB (via Pomelo EF Core provider)
- RabbitMQ (via RabbitMQ.Client 6.x)
- Entity Framework Core 8
- xUnit + NSubstitute
- Docker & Docker Compose
- Swagger/OpenAPI (Swashbuckle)
