using System.Security.Cryptography;
using JOrder.Common.Extensions;
using JOrder.Identity.Models;
using JOrder.Identity.Options;
using JOrder.Identity.Persistence;
using JOrder.Identity.Warmup;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

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
    .AddIdentityCore<User>(options =>
    {
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<Role>()
    .AddEntityFrameworkStores<JOrderIdentityDbContext>();

// Self-validate JWTs issued by this service (static key — no OIDC discovery needed)
var signingConfig = builder.Configuration
    .GetSection(JwtSigningOptions.SectionName)
    .Get<JwtSigningOptions>();

if (signingConfig is null)
    throw new InvalidOperationException("JWT signing configuration is missing");

using var rsa = RSA.Create();
rsa.ImportFromPem(File.ReadAllText(signingConfig.PrivateKeyPath));
var publicKey = new RsaSecurityKey(rsa.ExportParameters(false));
builder.AddJOrderJwtIssuerAuthentication(signingConfig.Issuer, signingConfig.Audience, publicKey);

// Build
await using var app = builder.Build();

// Execute all registered warmup tasks sequentially before accepting traffic
await app.RunWarmupTasksAsync();

// Configure middleware pipeline: HTTPS redirection, auth (if registered), and controller routing
// In Development: also maps OpenAPI and Scalar UI
app.MapDefaultEndpoints();

await app.RunAsync();
