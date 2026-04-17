using JOrder.Common.Extensions;
using JOrder.Identity.Models;
using JOrder.Identity.Options;
using JOrder.Identity.Persistence;
using JOrder.Identity.Warmup;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add logging, memory cache, TimeProvider, HttpContextAccessor, controllers, and OpenAPI
builder.AddJOrderCommon();

// Global per-IP rate limiting driven by [RateLimit] endpoint attributes
builder.AddJOrderRateLimiting();

// Bind, validate, and register JwtSigningOptions from configuration
builder.AddJOrderOptions<JwtSigningOptions>();

// Scan assembly and register classes marked with [ScopedService], [TransientService], or [SingletonService]
builder.AddJOrderServicesFromAssembly(typeof(Program).Assembly);

// Register warmup task to run before the app starts accepting requests
builder.AddJOrderWarmupTask<SigningKeyMaterialServiceWarmup>();

// Add database
builder.AddJOrderDatabase<JOrderIdentityDbContext>((_, options, dbOptions) =>
    options.UseNpgsql(dbOptions.ConnectionString));

// Add ASP.NET Identity
builder.Services
    .AddIdentityCore<User>()
    .AddRoles<Role>()
    .AddEntityFrameworkStores<JOrderIdentityDbContext>();

// Build
await using var app = builder.Build();

// Execute all registered warmup tasks sequentially before accepting traffic
await app.RunWarmupTasksAsync();

// Configure middleware pipeline: HTTPS redirection, auth (if registered), and controller routing
// In Development: also maps OpenAPI and Scalar UI
app.MapDefaultEndpoints();

await app.RunAsync();
