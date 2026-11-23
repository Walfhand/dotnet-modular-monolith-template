# Backend Template

A .NET backend starter template to bootstrap a modern API with DDD foundations and Vertical Slice feature organization.

## Template goals

- Provide a clean base for Minimal APIs
- Use DDD to keep the domain model explicit and protected
- Use Vertical Slice to keep each use case cohesive and easy to change
- Include quality tooling: logging, OpenAPI, migrations, CQRS

## High-level architecture

### Overview

```
src/
├── Api/                         # API host (entry point)
├── Engine/                      # Shared core/business layer
```

### Key areas

- **Api**: configuration, routes, OpenAPI, integrations, DI, web pipeline.
- **Engine**: domain/core, infrastructure, events, logging, persistence, CQRS, etc.

## DDD (Domain-Driven Design)

The domain model lives under `Api/Domain/` and is organized around aggregates and domain concepts.

Typical structure:

```
Api/Domain/
├── <BoundedContext>/
│   ├── AggregateRoots/
│   ├── AggregateRoots/Entities/
│   ├── AggregateRoots/ValueObjects/
│   └── Services/
```

Example:

```
Api/Domain/Orders/
├── AggregateRoots/
├── AggregateRoots/Entities/
└── AggregateRoots/ValueObjects/
```

Guidelines:

- Keep invariants inside aggregate roots
- Use value objects for strongly-typed concepts
- Keep domain services focused on domain logic only

## Vertical Slice Architecture

Features are grouped by module and use case so each slice contains everything it needs:

```
Api/Modules/
├── Sales/
│   └── Features/
│       └── Orders/
│           └── CreateOrderRepr/
│               ├── CreateOrder.cs
│               └── CreateOrderOutcomes.cs
```

A slice typically contains:

- Request and outcome contracts
- Endpoint definition (Minimal API)
- Wolverine HTTP endpoint and message handler for the use case

This avoids deep horizontal layers and makes each use case easy to find and change.

## Project structure

```
src/backend/
├── src/
│   ├── Api/
│   │   ├── Configuration/       # Configuration extensions (OpenAPI, logging...)
│   │   ├── Domain/              # DDD domain model
│   │   ├── Modules/             # Vertical slices by module/feature
│   │   ├── Infrastructure/      # API infrastructure
│   │   ├── Program.cs           # Entry point
│   │   └── ...
│   └── Engine/
│       ├── Core/                # Domain core
│       ├── Logging/             # Logging
│       ├── ProblemDetails/      # HTTP error details
│       ├── Outcomes/            # Business operation outcomes
│       ├── Wolverine/           # CQRS / messaging
│       └── ...
├── tests/                       # Tests
└── README.md
```

## OpenAPI / Scalar

The template exposes OpenAPI docs and Scalar UI.

- OpenAPI: `/openapi/{documentName}.json`
- Scalar UI: `/scalar`

## Local usage (Docker)

From the repo root:

```
make up-backend
```

## Tests

With the .NET 11 SDK and Docker running, execute `dotnet test BackendTemplate.slnx` from `src/backend`.

The unit test project includes architecture rules for domain dependencies and module cycles. The integration suite uses PostgreSQL to exercise transaction rollback and the outbox.

## Migrations

A helper Makefile is available in `src/backend/src/Api`:

```
make add-migration name=YourMigrationName
```

This runs:

```
dotnet ef migrations add YourMigrationName --project ./ -o ./Infrastructure/Persistence/Migrations
```

## Docker details

The API container is built from:

- build context: `src/backend`
- dockerfile: `src/Api/Dockerfile`

The database is PostgreSQL and is configured via `.env`.

## Environment variables

Compose reads the database connection and published ports from the root `.env`. Copy `.env.example` before starting the stack.

## Architecture conventions

### 1. Feature-based modules (Vertical Slice)
Each feature lives in its own folder under `Api/Modules/<Module>/Features/...`.

### 2. CQRS and events

Inject `AppDbContext` into a Wolverine command handler to use the EF Core transaction and outbox. The same pipeline applies when a caller invokes the command with `IMessageBus.InvokeAsync` outside HTTP.

Define synchronous domain reactions as public classes implementing `ITransactionalDomainEventHandler` in the API assembly. The template discovers them at startup and registers them as scoped services. Reactions can inject scoped dependencies in their constructors and receive the active `DbContext` when invoked. Wolverine's EF Core event hook drains any further events they raise before commit. Map events intended for asynchronous delivery to an `IIntegrationEvent` with `IEventMapper`.

For writes outside a Wolverine handler, use the scoped `IDbContextOutbox<AppDbContext>`. Stage changes on `outbox.DbContext`, queue any follow-up commands with `outbox.SendAsync` and call `outbox.SaveChangesAndFlushMessagesAsync`. The same event hook runs and queued commands are delivered after commit.

Each `InvokeAsync` starts its own Wolverine handler execution. To run two operations atomically, call their application methods in one handler and pass them the same `AppDbContext`.

### 3. Centralized web pipeline
`ApplicationConfiguration` centralizes app setup:
- services
- middleware
- OpenAPI
- migrations

Business operations return the C# 15 union `Outcome<TSuccess, TError>`. Each use case declares its success type and error union in an `*Outcomes.cs` file. `CreateOrderOutcomes.cs` declares `OrderCreated` and `CreateOrderError`. The endpoint returns each typed error as JSON with a stable `code` and any relevant values. Missing lines return `400 Bad Request`, invalid values return `422 Unprocessable Content`, and a successful command returns `201 Created`. The API build treats non-exhaustive switch expressions as errors.

## Start the full stack

From the repo root:

```
make up-all
```
