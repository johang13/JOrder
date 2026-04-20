# JOrder.Common

`JOrder.Common` is the shared library for cross-cutting concerns used by JOrder services.

It provides:

- Result/error primitives (`Result`, `Result<T>`, `Error`)
- Startup and pipeline extensions (`AddJOrderCommon`, `MapDefaultEndpoints`, JWT setup helpers)
- Attribute-driven service registration and endpoint rate limiting
- EF Core auditing support (`AuditableInterceptor`, auditable base models)
- Request origin logging middleware and common helpers

Target framework: `net10.0`

## Folder Overview

- `Abstractions/Results` - Result pattern types (`Error`, `ErrorType`, `Result`, `Result<T>`)
- `Attributes` - DI lifetime attributes and `RateLimitAttribute`
- `Extensions` - host/app/controller/service-collection extension methods
- `Middleware` - `RequestOriginLoggingMiddleware`
- `Models` - base entities (`Entity`, `AuditableEntity`) and interfaces
- `Options` - strongly-typed configuration options (`ServiceOptions`, `DatabaseOptions`, `JwtValidationOptions`)
- `Persistence` - EF Core `AuditableInterceptor`
- `Services` - current-user abstraction and implementation (`ICurrentUser`, `CurrentUser`)

## Quick Start

Typical usage in a service `Program.cs`:

```csharp
using JOrder.Common.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddJOrderCommon();
builder.AddJOrderRateLimiting();

builder.AddJOrderDatabase<MyDbContext>((_, options, dbOptions) =>
	options.UseNpgsql(dbOptions.ConnectionString));

builder.AddJOrderJwtValidation();
builder.AddJOrderServicesFromAssembly(typeof(Program).Assembly);

var app = builder.Build();
app.MapDefaultEndpoints();
await app.RunAsync();
```

## DI Attributes Demo

`AddJOrderServicesFromAssembly(...)` scans an assembly and registers classes marked with:

- `[ScopedService]`
- `[TransientService]`
- `[SingletonService]`

Each class is registered against its non-framework interfaces. If no such interface exists, it is registered as self.

```csharp
using JOrder.Common.Attributes;
using JOrder.Common.Extensions;

public interface IEmailSender
{
	Task SendAsync(string to, string subject, string body);
}

[ScopedService]
public sealed class SmtpEmailSender : IEmailSender
{
	public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
}

[SingletonService]
public sealed class UtcClock
{
	public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

[TransientService]
public sealed class CorrelationIdFactory
{
	public string Create() => Guid.NewGuid().ToString("N");
}

var builder = WebApplication.CreateBuilder(args);
builder.AddJOrderServicesFromAssembly(typeof(Program).Assembly);
```

In this example:

- `IEmailSender -> SmtpEmailSender` is scoped
- `UtcClock` is registered as singleton (self registration)
- `CorrelationIdFactory` is registered as transient (self registration)

## Warmup Tasks

Warmup tasks let a service do startup work (for example, loading key material, seeding caches, or checking dependencies)
before the app starts accepting traffic.

1. Implement `IJOrderWarmupTask`.
2. Register it with `AddJOrderWarmupTask<TWarmupTask>()`.
3. Run all tasks with `app.RunWarmupTasksAsync()` before `app.RunAsync()`.

```csharp
using JOrder.Common.Extensions;
using JOrder.Common.Services.Interfaces;

public sealed class PrimeCacheWarmupTask : IJOrderWarmupTask
{
	public Task ExecuteAsync(CancellationToken cancellationToken)
	{
		// Load anything expensive here.
		return Task.CompletedTask;
	}
}

var builder = WebApplication.CreateBuilder(args);
builder.AddJOrderWarmupTask<PrimeCacheWarmupTask>();

var app = builder.Build();
await app.RunWarmupTasksAsync();
await app.RunAsync();
```

Behavior notes:

- Warmup tasks are resolved from DI and executed sequentially.
- If no tasks are registered, a single informational log entry is written.
- Duplicate registrations of the same task type are ignored.
- Task execution is timed and logged per task.
- The cancellation token passed to `RunWarmupTasksAsync` is forwarded to each task.

## Common Extension Methods

From `HostApplicationExtensions`:

- `AddJOrderCommon()` - registers logging, cache, `TimeProvider`, `ICurrentUser`, `HttpContextAccessor`, controllers, and OpenAPI
- `AddJOrderRateLimiting()` - enables global per-IP rate limiting driven by `[RateLimit]`
- `AddJOrderOptions<TOptions>()` - binds and validates options on startup
- `AddJOrderDatabase<TDbContext>(...)` - registers DbContext and auditing interceptor
- `AddJOrderJwtValidation(...)` - configures JWT bearer validation from authority/audience settings
- `AddJOrderJwtIssuerAuthentication(...)` - configures JWT bearer validation with static RSA key
- `AddJOrderJwtAuthentication(...)` - full custom JWT bearer configuration
- `AddJOrderServicesFromAssembly(...)` - auto-registers classes marked with lifetime attributes
- `AddJOrderWarmupTask<TWarmupTask>()` - registers startup warmup tasks

From `WebApplicationExtensions`:

- `MapDefaultEndpoints()` - maps controllers, optional OpenAPI/Scalar in development, auth/authorization if registered, health probes, and request-origin logging middleware
- `RunWarmupTasksAsync()` - executes registered warmup tasks before serving traffic

From `ControllerBaseExtensions`:

- `ToActionResult(Error)` - maps domain errors to HTTP responses
- `GetUserIdClaim()` - resolves user id from `sub` or `nameidentifier` claim

## Configuration

`JOrder.Common` options are read from these sections:

- `JOrder:ServiceOptions`
- `JOrder:DatabaseOptions`
- `JOrder:Authentication:JwtValidation`

Example:

```json
{
  "JOrder": {
	"ServiceOptions": {
	  "Name": "Identity",
	  "Version": "1.0.0"
	},
	"DatabaseOptions": {
	  "Provider": "Postgres",
	  "ConnectionString": "Host=localhost;Port=5432;Database=jorder;Username=postgres;Password=postgres"
	},
	"Authentication": {
	  "JwtValidation": {
		"Authority": "https://identity.jorder.localhost",
		"Audience": "jorder-api",
		"RequireHttpsMetadata": false
	  }
	}
  }
}
```

## Rate Limiting

Decorate endpoints or controllers with `[RateLimit]`:

```csharp
using JOrder.Common.Attributes;

[RateLimit(permitLimit: 30, windowSeconds: 60, maxConcurrentRequests: 5)]
public async Task<IActionResult> Login(LoginRequestDto request)
{
	// ...
}
```

## Running Tests

From repository root:

```zsh
dotnet test tests/JOrder.Common.UnitTests/JOrder.Common.UnitTests.csproj
```

Or run all tests in the solution:

```zsh
dotnet test
```

