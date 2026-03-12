# Solution Architecture Document

## 1. Overview

Two .NET 8 applications communicating via RabbitMQ:

- **HashApi** — REST API that generates SHA1 hashes and publishes them to RabbitMQ, and serves aggregated hash counts from MariaDB.
- **HashProcessor** — Background worker that consumes messages from RabbitMQ and persists hashes to MariaDB.

Both run in Docker containers orchestrated via Docker Compose alongside RabbitMQ and MariaDB.

---

## 2. Architecture Decisions

### 2.1 Project Structure

Single solution with clear layer separation following SOLID principles:

```
HashChallenge.sln
│
├── src/
│   ├── HashChallenge.Api/              # ASP.NET Web API (Presentation layer)
│   ├── HashChallenge.Processor/        # Background worker (Console/Worker Service)
│   ├── HashChallenge.Domain/           # Shared domain: entities, interfaces, services
│   └── HashChallenge.Infrastructure/   # EF DbContext, repositories, RabbitMQ client
│
├── tests/
│   ├── HashChallenge.Api.Tests/        # API unit tests
│   ├── HashChallenge.Domain.Tests/     # Domain unit tests
│   ├── HashChallenge.Infrastructure.Tests/  # Repository/messaging unit tests
│   └── HashChallenge.IntegrationTests/ # End-to-end with Testcontainers
│
├── docker-compose.yml
└── README.md
```

**Rationale:** The `Domain` project contains entities, interfaces, and service contracts shared by both apps. The `Infrastructure` project implements data access (EF) and messaging (RabbitMQ). Neither API nor Processor depends on concrete implementations — only on interfaces from Domain. This enforces Dependency Inversion (the D in SOLID).

### 2.2 Layer Responsibilities (3-Layer Architecture)

The solution follows a **3-layer architecture** (API → Domain → Repository) mapped to projects:

| Layer | Project | Responsibility |
|-------|---------|---------------|
| **API (Presentation)** | HashChallenge.Api | Controllers, request/response DTOs, middleware, DI registration |
| **Domain (Business)** | HashChallenge.Domain | Entities, service interfaces (`IHashGenerator`, `IHashPublisher`), repository interfaces (`IHashRepository`) |
| **Repository (Data Access)** | HashChallenge.Infrastructure | EF `HashDbContext`, `HashRepository`, `RabbitMqPublisher`, `RabbitMqConsumer` |
| **Worker (Presentation)** | HashChallenge.Processor | Hosted service that wires up consumer — same architectural layer as API, different entry point |

> **Note:** The project is named `Infrastructure` (common .NET convention) but it **is** the Repository layer. It contains all data access (EF) and external service integration (RabbitMQ). No business logic resides here.

### 2.3 Dependency Graph

```
HashApi ──────► Domain ◄────── HashProcessor
   │                               │
   └──────► Infrastructure ◄───────┘
```

Both apps reference Domain (interfaces) and Infrastructure (implementations registered via DI). No circular dependencies.

### 2.4 SOLID Principles Mapping

| Principle | How it's applied |
|-----------|-----------------|
| **S — Single Responsibility** | Each class has one reason to change: `HashGenerator` only generates, `HashRepository` only persists, `HashesController` only handles HTTP. Publisher and Consumer are separate classes. |
| **O — Open/Closed** | New message formats or storage strategies can be added by implementing existing interfaces (`IHashPublisher`, `IHashRepository`) without modifying consumers of those interfaces. |
| **L — Liskov Substitution** | All interface implementations are fully substitutable — unit tests run against mocks, integration tests run against real implementations, same contract both ways. |
| **I — Interface Segregation** | `IHashRepository` has only Save + GetDailyCounts (not a bloated generic repository). `IHashPublisher` and `IHashConsumer` are separate interfaces — the API only depends on `IHashPublisher`, the Processor only depends on `IHashConsumer`. |
| **D — Dependency Inversion** | API and Processor depend on abstractions (interfaces in Domain), not on concrete classes in Infrastructure. Wiring happens at composition root (`Program.cs`) via DI container. |

---

## 3. Database Design

### 3.1 Schema (Code-First with EF)

**Table: `hashes`**

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | BIGINT | PK, Auto-increment |
| `Date` | DATE | NOT NULL, Indexed |
| `Sha1` | CHAR(40) | NOT NULL |

**Index:** `IX_hashes_Date` on `Date` column — optimizes the GROUP BY query.

This schema is in 3NF: no partial dependencies (single non-composite PK), no transitive dependencies.

### 3.2 Indexing Strategy & Write/Read Trade-off

**Workload profile:** Write-heavy (40k inserts per POST via 4 concurrent threads), read-infrequent (GROUP BY on demand).

**`Id` (PK, clustered index):** Auto-increment BIGINT is optimal for InnoDB. Inserts always append to the end of the clustered B-tree — no page splits, no random I/O. This is the best-case scenario for write throughput.

**`IX_hashes_Date` (secondary index on `Date`):** DATE is 3 bytes — a very small index entry. The `GROUP BY Date` query benefits from an **index-only scan** (covering index): InnoDB reads only the index pages to count entries per date, never touching the table data pages. Write overhead is ~10-15% per INSERT for maintaining one additional B-tree. On 40k inserts this translates to roughly 1-2 extra seconds — an acceptable cost for turning the GROUP BY from a full table scan into an index scan.

**No index on `Sha1`:** No query filters or groups by Sha1. CHAR(40) would create a large secondary index (40 bytes per entry + 8 bytes PK pointer), significantly increasing write cost for zero read benefit.

**MariaDB tuning for Docker (docker-compose environment vars):**
- `innodb_buffer_pool_size=256M` — keeps index pages and hot data in memory, reduces disk I/O for both reads and writes
- `innodb_change_buffering=all` — defers secondary index updates during bulk inserts, batches them for efficiency
- `innodb_flush_log_at_trx_commit=2` — trades durability guarantee for write throughput (acceptable for non-financial data in a challenge context; flushes to OS cache per commit, disk sync once per second)

**Conclusion:** One secondary index on `Date` gives us efficient reads without meaningfully degrading the write-heavy workload. Adding more indexes would shift the balance negatively with no query benefit.

### 3.3 GET /hashes Query Strategy

**Chosen approach:** Simple `GROUP BY Date` query.

```sql
SELECT Date, COUNT(*) as Count FROM hashes GROUP BY Date ORDER BY Date;
```

**Rationale:** For the scope of this challenge (40k hashes per POST call), this query performs well with the date index. It keeps the implementation simple and avoids consistency issues between a summary table and the source data.

**Advanced alternative (documented, not implemented):** A pre-aggregated `hash_daily_counts` table (`Date` PK, `Count` BIGINT) updated atomically by the processor via `INSERT ... ON DUPLICATE KEY UPDATE Count = Count + 1`. The GET endpoint would read directly from this table, reducing query time from O(n) to O(days). This would be necessary if the table grows to hundreds of millions of rows.

---

## 4. API Design

### 4.1 POST /hashes

- Generates 40,000 random SHA1 hashes
- Publishes each hash as an individual message to RabbitMQ
- Returns **202 Accepted** immediately after all messages are enqueued
- Response body: `{ "enqueuedCount": 40000 }`

**Hash generation:** Use `SHA1.HashData()` on `Guid.NewGuid().ToByteArray()` for randomness. Convert to lowercase hex string (40 chars).

**Publishing:** Sequential publish with publisher confirms enabled. Each message is a JSON body `{ "sha1": "...", "date": "2026-03-09" }` — date is set at generation time, not processing time (see Section 5.2 for details).

### 4.2 GET /hashes

- Queries MariaDB with GROUP BY Date
- Returns JSON:

```json
{
  "hashes": [
    { "date": "2022-06-25", "count": 471255631 },
    { "date": "2022-06-26", "count": 822365413 }
  ]
}
```

### 4.3 Error Handling

- Global exception handling middleware
- Structured error responses: `{ "error": "message", "statusCode": 500 }`
- 503 Service Unavailable if RabbitMQ connection is down during POST

### 4.4 OpenAPI

Swagger/OpenAPI enabled via `Swashbuckle.AspNetCore`. Available at `/swagger` in development.

---

## 5. RabbitMQ Messaging

### 5.1 Configuration

- **Queue:** `hash-queue` (durable, non-exclusive, non-auto-delete)
- **Exchange:** Default exchange (direct routing by queue name)
- **Publisher confirms:** Enabled for reliability
- **Message persistence:** Persistent delivery mode

### 5.2 Message Format

- Body: JSON object serialized with `System.Text.Json`
  ```json
  { "sha1": "a94a8fe5ccb19ba61c4c0873d391e987982fbbd3", "date": "2026-03-09" }
  ```
- Content-Type: `application/json`
- Delivery mode: Persistent (survives broker restart)

**Rationale for including date in message body:** The date should reflect when the hash was *generated*, not when it was *processed*. If the consumer has a backlog, the dates would drift otherwise. Keeping both fields in the JSON body avoids RabbitMQ custom header byte-array complexity.

### 5.3 Consumer (Processor)

- `AsyncEventingBasicConsumer` with `PrefetchCount = 4`
- Processing: parse hash + date from message, save to DB, acknowledge
- 4 parallel threads achieved via `ConsumerDispatchConcurrency = 4` on the connection factory
- Manual acknowledgement (`autoAck: false`) — ack after successful DB write, nack + requeue on failure

### 5.4 Optional: Batch Publishing

**Documented but not implemented in base version:**

Split 40k hashes into configurable batches (e.g., 1000 per batch). Publish batches in parallel using `Parallel.ForEachAsync` with separate RabbitMQ channels per task. This reduces total publish time from ~seconds to sub-second.

---

## 6. Processor Design

The Processor is a .NET Worker Service (`IHostedService`) that:

1. Connects to RabbitMQ on startup
2. Declares the queue (idempotent — ensures queue exists)
3. Registers an async consumer with 4 concurrent threads
4. On each message: deserializes, creates `Hash` entity, saves via `IHashRepository`
5. Acknowledges the message after successful DB write
6. Handles graceful shutdown (stop consuming, wait for in-flight messages)

---

## 7. Interfaces (Contract-First)

### Domain Layer

```csharp
// Entity (persisted to DB)
public class HashEntry
{
    public long Id { get; private set; }  // DB-generated auto-increment; private setter signals DB ownership
    public DateOnly Date { get; set; }
    public string Sha1 { get; set; } = string.Empty;
}

// DTO / projection (not a DB entity — result of GROUP BY query)
public class HashDailyCount
{
    public DateOnly Date { get; set; }
    public long Count { get; set; }
}

// Message DTO (serialized to/from RabbitMQ JSON body)
public class HashMessage
{
    public string Sha1 { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
}

// Repository interface (Data Access layer contract)
public interface IHashRepository
{
    Task SaveAsync(HashEntry hash, CancellationToken ct = default);
    Task<IReadOnlyList<HashDailyCount>> GetDailyCountsAsync(CancellationToken ct = default);
}

// Service interfaces
public interface IHashGenerator
{
    IReadOnlyList<HashEntry> Generate(int count);
}

public interface IHashPublisher
{
    Task PublishAsync(IReadOnlyList<HashEntry> hashes, CancellationToken ct = default);
}

public interface IHashConsumer
{
    Task StartConsumingAsync(CancellationToken ct = default);
    Task StopConsumingAsync(CancellationToken ct = default);
}
```

### Infrastructure Layer

```csharp
public class HashRepository : IHashRepository { ... }
public class RabbitMqHashPublisher : IHashPublisher { ... }
public class RabbitMqHashConsumer : IHashConsumer { ... }
```

### API Layer

```csharp
[ApiController]
[Route("hashes")]
public class HashesController : ControllerBase
{
    // POST /hashes → 202 Accepted
    // GET /hashes → 200 OK with daily counts
}
```

---

## 8. Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `RABBITMQ__HOST` | `localhost` | RabbitMQ hostname |
| `RABBITMQ__PORT` | `5672` | RabbitMQ port |
| `RABBITMQ__USERNAME` | `guest` | RabbitMQ username |
| `RABBITMQ__PASSWORD` | `guest` | RabbitMQ password |
| `RABBITMQ__QUEUENAME` | `hash-queue` | Queue name |
| `RABBITMQ__PREFETCHCOUNT` | `4` | Consumer prefetch count |
| `RABBITMQ__CONCURRENCY` | `4` | Consumer thread count |
| `CONNECTIONSTRINGS__HASHDB` | — | MariaDB connection string |
| `HASH__GENERATECOUNT` | `40000` | Number of hashes to generate per POST |

---

## 9. Docker Setup

### Dockerfiles

Both apps use multi-stage builds:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# ... restore, build, publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
# ... copy published output, set entrypoint
```

### Docker Compose

```yaml
services:
  rabbitmq:
    image: rabbitmq:3-management
    healthcheck: rabbitmq-diagnostics -q ping

  mariadb:
    image: mariadb:11
    healthcheck: mariadb-admin ping

  hashapi:
    build: ./src/HashChallenge.Api
    depends_on:
      rabbitmq: { condition: service_healthy }
      mariadb: { condition: service_healthy }

  hashprocessor:
    build: ./src/HashChallenge.Processor
    depends_on:
      rabbitmq: { condition: service_healthy }
      mariadb: { condition: service_healthy }
```

---

## 10. Testing Strategy

### 10.1 Unit Tests (xUnit + Moq/NSubstitute)

**Domain Tests:**
- `HashGenerator` produces correct count of valid SHA1 strings (40 hex chars)
- `HashGenerator` produces unique hashes
- `HashGenerator` sets correct date

**API Tests:**
- `HashesController.Post` returns 202 and calls `IHashPublisher`
- `HashesController.Get` returns correct JSON structure from `IHashRepository`
- Error handling: returns 503 when publisher throws connection exception

**Infrastructure Tests:**
- `RabbitMqHashPublisher` calls BasicPublish for each hash (mock IModel)
- `HashRepository` maps entities correctly

### 10.2 Integration Tests (Testcontainers)

- Spin up real MariaDB + RabbitMQ in Docker
- POST /hashes → verify messages appear in queue
- Full flow: POST → Processor consumes → GET returns correct counts
- Verify EF migrations apply cleanly to fresh DB

### 10.3 Edge Cases to Cover

- RabbitMQ connection failure during publish (graceful error)
- Malformed message in queue (consumer skips, logs, nacks without requeue)
- Empty database (GET returns empty array, not error)
- Concurrent POST requests (thread safety)
- Processor startup before queue exists (should declare queue)
- Database connection timeout during processing

---

## 11. Advanced Techniques (Documented for Reference)

These are **not implemented** in the base solution but represent production-ready enhancements:

1. **Pre-aggregated summary table** — `hash_daily_counts` updated atomically on insert. Eliminates GROUP BY scan.
2. **Batch publishing** — split hashes into batches, publish via parallel channels. Reduces publish latency.
3. **Bulk DB inserts** — `AddRange` + `SaveChangesAsync` every N messages instead of one-by-one. 10-50x throughput improvement.
4. **Outbox pattern** — write messages to DB outbox table in same transaction as business data, publish via background job. Guarantees exactly-once delivery.
5. **Polly resilience** — retry policies with exponential backoff for RabbitMQ and MariaDB operations.
6. **Health checks** — `IHealthCheck` implementations for RabbitMQ and MariaDB, exposed at `/health`.
7. **Structured logging (Serilog)** — JSON structured logs with correlation IDs across API → Queue → Processor.
8. **Channel pooling** — `ObjectPool<IModel>` for RabbitMQ channels to reduce creation overhead.

---

## 12. Code Quality & Linter Configuration

### .editorconfig

Root `.editorconfig` enforcing C# conventions:

- `dotnet_naming_rule` — PascalCase for public members, camelCase with `_` prefix for private fields
- `csharp_style_var_for_built_in_types = false` — explicit types for clarity
- `dotnet_diagnostic.CA1062.severity = warning` — null parameter checks
- `dotnet_diagnostic.CA2007.severity = warning` — ConfigureAwait usage
- Nullable reference types enabled (`<Nullable>enable</Nullable>`) in all projects

### Analyzers

NuGet packages added to `Directory.Build.props` (applied to all projects):

- `Microsoft.CodeAnalysis.NetAnalyzers` — built-in Roslyn analyzers
- `StyleCop.Analyzers` — consistent formatting and naming
- `SonarAnalyzer.CSharp` — additional code smell detection

### Code conventions

- Async methods suffixed with `Async`
- Interfaces prefixed with `I`
- One class per file
- `sealed` on classes not designed for inheritance
- `CancellationToken` propagated through all async chains

---

## 13. Dependency Injection & Service Lifetimes

| Service | Lifetime | Reason |
|---------|----------|--------|
| `HashDbContext` | **Scoped** | EF DbContext is not thread-safe; one per request/message |
| `IHashRepository` | **Scoped** | Depends on scoped DbContext |
| `IHashGenerator` | **Singleton** | Stateless, thread-safe |
| `IHashPublisher` | **Singleton** | Holds long-lived RabbitMQ connection |
| `IHashConsumer` | **Singleton** | Runs as hosted service for app lifetime |

**Important for Processor:** Since the consumer handles messages on 4 concurrent threads, each message handler must create its own `IServiceScope` to get a fresh `DbContext`. This prevents cross-thread DbContext access:

```csharp
// Inside consumer message handler
using var scope = _serviceProvider.CreateScope();
var repository = scope.ServiceProvider.GetRequiredService<IHashRepository>();
await repository.SaveAsync(hashEntry, ct);
```

### Registration Pattern (Program.cs)

```csharp
// Domain services
builder.Services.AddSingleton<IHashGenerator, HashGenerator>();

// Infrastructure
builder.Services.AddDbContext<HashDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddScoped<IHashRepository, HashRepository>();
builder.Services.AddSingleton<IHashPublisher, RabbitMqHashPublisher>();
```

---

## 14. Message Handling Details

### Serialization

Message body is a simple JSON object:

```json
{ "sha1": "a94a8fe5ccb19ba61c4c0873d391e987982fbbd3", "date": "2026-03-09" }
```

Using `System.Text.Json` for serialization/deserialization. This keeps both fields in the body (avoiding RabbitMQ custom header byte-array complexity).

### Poison Message Handling

If a message cannot be deserialized or fails DB write after the consumer scope completes:

1. Log the error with message body for debugging
2. `BasicNack` with `requeue: false` — message goes to the dead-letter queue (if configured) or is discarded
3. No infinite retry loop — a failing message should not block the queue

### Dead-Letter Queue (Optional Enhancement)

Configure `hash-queue` with `x-dead-letter-exchange` and `x-dead-letter-routing-key` pointing to `hash-queue-dlq`. Failed messages land there for manual inspection.

---

## 15. Database Migrations

- Migrations stored in `HashChallenge.Infrastructure/Migrations/`
- Applied automatically on application startup via `context.Database.MigrateAsync()` in both API and Processor
- For production: consider a separate migration runner or `dotnet ef database update` in CI/CD pipeline
- Rollback: generate down migration with `dotnet ef migrations remove`

---

## 16. Implementation Order (TDD Approach)

1. Create solution structure and all projects with references
2. Set up linter: `.editorconfig`, `Directory.Build.props` with analyzers, enable nullable
3. Define all interfaces in Domain layer (entities + contracts)
4. Write unit tests against interfaces (red phase — tests compile but fail)
5. Implement `HashGenerator` in Domain → run tests (green)
6. Implement `HashDbContext` + `HashRepository` in Infrastructure → run tests (green)
7. Implement `RabbitMqHashPublisher` in Infrastructure → run tests (green)
8. Implement `RabbitMqHashConsumer` in Infrastructure → run tests (green)
9. Implement `HashesController` in API → run tests (green)
10. Implement `HashProcessorWorker` hosted service in Processor
11. Add EF migrations, verify against MariaDB
12. Set up Dockerfiles + Docker Compose with health checks
13. Write integration tests with Testcontainers
14. Add OpenAPI/Swagger with proper DTOs and examples
15. Write README with build/run/test instructions

---

## 17. Build & Run Instructions (To Be Written After Implementation)

Detailed instructions covering prerequisites, `dotnet build`, `docker-compose up`, running tests, and troubleshooting will be added to `README.md` after implementation is complete.
