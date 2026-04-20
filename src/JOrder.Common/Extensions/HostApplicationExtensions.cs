using System.Reflection;
using System.Security.Claims;
using System.Threading.RateLimiting;
using JOrder.Common.Attributes;
using JOrder.Common.Helpers;
using JOrder.Common.Options;
using JOrder.Common.Options.Interfaces;
using JOrder.Common.Persistence;
using JOrder.Common.Services;
using JOrder.Common.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace JOrder.Common.Extensions;

public static class HostApplicationExtensions
{
    /// <summary>
    /// Registers common JOrder infrastructure: service options, logging, memory cache,
    /// <see cref="TimeProvider"/>, and HTTP context accessor.
    /// When running as a <see cref="Microsoft.AspNetCore.Builder.WebApplicationBuilder"/>,
    /// also registers controllers and the OpenAPI document with the service name as its title.
    /// </summary>
    public static IHostApplicationBuilder AddJOrderCommon(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        
        services.AddJOrderOptions<ServiceOptions>();
        
        var serviceOptions = builder.Configuration
            .GetSection(ServiceOptions.SectionName)
            .Get<ServiceOptions>();

        services.AddLogging(logging =>
        {
            logging.AddSimpleConsole(options =>
            {
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
                options.UseUtcTimestamp = true;
            });
        });
        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<ICurrentUser, CurrentUser>();
        
        if (builder is WebApplicationBuilder)
        {
            services.AddHttpContextAccessor();
            services.AddControllers();
            services.AddJOrderOpenApi(serviceOptions?.Name ?? builder.Environment.ApplicationName);
        }
        
        return builder;
    }

    /// <summary>
    /// Registers a global per-IP rate limiter driven by <see cref="RateLimitAttribute"/>.
    /// Endpoints decorated with that attribute are automatically limited; all others are unrestricted.
    /// </summary>
    /// <remarks>
    /// Two limits are chained per endpoint:
    /// <list type="bullet">
    ///   <item><term>Fixed window</term><description>
    ///     Enforces <see cref="RateLimitAttribute.PermitLimit"/> requests per
    ///     <see cref="RateLimitAttribute.WindowSeconds"/> window, partitioned by remote IP.
    ///   </description></item>
    ///   <item><term>Concurrency</term><description>
    ///     Caps simultaneous in-flight requests to
    ///     <see cref="RateLimitAttribute.MaxConcurrentRequests"/> per IP.
    ///     Disabled when the value is <c>0</c>.
    ///   </description></item>
    /// </list>
    /// Call <c>app.MapDefaultEndpoints()</c> as normal — <c>UseRateLimiter()</c> is inserted
    /// automatically when this method has been called.
    /// </remarks>
    /// <param name="builder">The application builder.</param>
    public static IHostApplicationBuilder AddJOrderRateLimiting(this IHostApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

                return ValueTask.CompletedTask;
            };

            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(

                // ── Fixed window: permit limit per time window ────────────────────
                PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    var endpoint = httpContext.GetEndpoint();
                    var attr = endpoint?.Metadata.GetMetadata<RateLimitAttribute>();

                    if (attr is null)
                        return RateLimitPartition.GetNoLimiter<string>("fw:none");

                    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    var route = endpoint!.DisplayName ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: $"fw:{route}:{ip}",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = attr.PermitLimit,
                            Window = TimeSpan.FromSeconds(attr.WindowSeconds),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        });
                }),

                // ── Concurrency: max simultaneous requests per IP ─────────────────
                PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    var endpoint = httpContext.GetEndpoint();
                    var attr = endpoint?.Metadata.GetMetadata<RateLimitAttribute>();

                    if (attr is null || attr.MaxConcurrentRequests <= 0)
                        return RateLimitPartition.GetNoLimiter<string>("cc:none");

                    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    var route = endpoint!.DisplayName ?? "unknown";
                    return RateLimitPartition.GetConcurrencyLimiter(
                        partitionKey: $"cc:{route}:{ip}",
                        factory: _ => new ConcurrencyLimiterOptions
                        {
                            PermitLimit = attr.MaxConcurrentRequests,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        });
                })
            );
        });

        return builder;
    }

    /// <summary>
    /// Registers a warmup task that will be executed by <see cref="WebApplicationExtensions.RunWarmupTasksAsync"/>
    /// before the application starts accepting traffic. Duplicate registrations of the same type are silently ignored.
    /// </summary>
    /// <typeparam name="TWarmupTask">The warmup task implementation to register.</typeparam>
    public static IHostApplicationBuilder AddJOrderWarmupTask<TWarmupTask>(this IHostApplicationBuilder builder)
        where TWarmupTask : class, IJOrderWarmupTask
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IJOrderWarmupTask, TWarmupTask>());
        return builder;
    }
    
    /// <summary>
    /// Binds <typeparamref name="TJOrderOptions"/> from the configuration section defined by
    /// <see cref="IJOrderOptions.SectionName"/>, enforces data annotations, and validates on startup.
    /// </summary>
    /// <typeparam name="TJOrderOptions">The options type to bind and register.</typeparam>
    public static IHostApplicationBuilder AddJOrderOptions<TJOrderOptions>(this IHostApplicationBuilder builder)
        where TJOrderOptions : class, IJOrderOptions
    {
        builder.Services.AddJOrderOptions<TJOrderOptions>();
        return builder;
    }
    
    /// <summary>
    /// Registers <typeparamref name="TDbContext"/> and binds <see cref="DatabaseOptions"/> from configuration.
    /// The <paramref name="configureProvider"/> delegate receives the resolved <see cref="DatabaseOptions"/>
    /// so the caller can choose and configure the EF Core database provider.
    /// </summary>
    /// <typeparam name="TDbContext">The <see cref="DbContext"/> type to register.</typeparam>
    /// <param name="builder">The application builder.</param>
    /// <param name="configureProvider">
    /// A delegate that configures the EF Core <see cref="DbContextOptionsBuilder"/> using the resolved
    /// <see cref="DatabaseOptions"/> (e.g. to select Npgsql, SQLite, etc.).
    /// </param>
    public static IHostApplicationBuilder AddJOrderDatabase<TDbContext>(
        this IHostApplicationBuilder builder,
        Action<IServiceProvider, DbContextOptionsBuilder, DatabaseOptions> configureProvider) 
        where TDbContext : DbContext
    {
        builder.AddJOrderOptions<DatabaseOptions>();

        builder.Services.AddScoped<AuditableInterceptor>();
        
        builder.Services.AddDbContext<TDbContext>((sp, options) =>
        {
            var databaseOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>();
            configureProvider(sp, options, databaseOptions.Value);
            options.AddInterceptors(sp.GetRequiredService<AuditableInterceptor>());
        });

        return builder;
    }
    
    /// <summary>
    /// Configures JWT Bearer authentication using OIDC authority discovery.
    /// Binds and validates <see cref="JwtValidationOptions"/> from configuration,
    /// then applies <c>Authority</c>, <c>Audience</c>, and <c>RequireHttpsMetadata</c>
    /// to <see cref="JwtBearerOptions"/>. An optional <paramref name="configure"/> delegate
    /// allows further customisation after the bound values are applied.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="configure">Optional delegate to further customise <see cref="JwtBearerOptions"/>.</param>
    public static IHostApplicationBuilder AddJOrderJwtValidation(
        this IHostApplicationBuilder builder,
        Action<JwtBearerOptions>? configure = null)
    {
        builder.Services.AddJwtValidation(configure);
        return builder;
    }
    
    /// <summary>
    /// Configures JWT Bearer authentication using a static RSA public key for offline token validation.
    /// Use this for service-to-service scenarios where OIDC discovery is unavailable.
    /// For authority-based discovery, prefer <see cref="AddJOrderJwtValidation"/>.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="issuer">The expected token issuer (<c>iss</c> claim).</param>
    /// <param name="audience">The expected token audience (<c>aud</c> claim).</param>
    /// <param name="publicKey">The RSA public key used to verify token signatures.</param>
    public static IHostApplicationBuilder AddJOrderJwtIssuerAuthentication(
        this IHostApplicationBuilder builder,
        string issuer,
        string audience,
        RsaSecurityKey publicKey)
    {
        builder.Services.AddJwtAuthentication(jwtOpts =>
        {
            jwtOpts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = publicKey,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role
            };
        });

        return builder;
    }
    
    /// <summary>
    /// Configures JWT Bearer authentication with a fully custom <see cref="JwtBearerOptions"/> delegate.
    /// Use when neither <see cref="AddJOrderJwtValidation"/> nor <see cref="AddJOrderJwtIssuerAuthentication"/>
    /// cover your scenario.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="configure">Delegate to configure <see cref="JwtBearerOptions"/>.</param>
    public static IHostApplicationBuilder AddJOrderJwtAuthentication(
        this IHostApplicationBuilder builder,
        Action<JwtBearerOptions> configure)
    {
        builder.Services.AddJwtAuthentication(configure);
        return builder;
    }

    /// <summary>
    /// Scans <paramref name="assembly"/> for classes marked with <see cref="Attributes.ScopedServiceAttribute"/>,
    /// <see cref="Attributes.TransientServiceAttribute"/>, or <see cref="Attributes.SingletonServiceAttribute"/>
    /// and registers them against their non-framework interfaces. Classes with no matching interface
    /// are registered as self. Existing registrations are not overwritten (<c>TryAdd</c> semantics).
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="assembly">The assembly to scan.</param>
    public static IHostApplicationBuilder AddJOrderServicesFromAssembly(
        this IHostApplicationBuilder builder,
        Assembly assembly)
    {
        // Framework namespaces to exclude when mapping interfaces
        static bool IsUserInterface(Type t) =>
            t.IsInterface && t.Namespace is not null &&
            !t.Namespace.StartsWith("System") &&
            !t.Namespace.StartsWith("Microsoft");

        foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            var lifetime = GetLifetime(type);
            if (lifetime is null) continue;

            var interfaces = type.GetInterfaces().Where(IsUserInterface).ToArray();

            if (interfaces.Length == 0)
            {
                // No interface — register as self
                builder.Services.TryAdd(new ServiceDescriptor(type, type, lifetime.Value));
            }
            else
            {
                foreach (var iface in interfaces)
                    builder.Services.TryAdd(new ServiceDescriptor(iface, type, lifetime.Value));
            }
        }

        return builder;
    }

    private static ServiceLifetime? GetLifetime(Type type)
    {
        if (type.GetCustomAttribute<ScopedServiceAttribute>() is not null)    return ServiceLifetime.Scoped;
        if (type.GetCustomAttribute<TransientServiceAttribute>() is not null)  return ServiceLifetime.Transient;
        if (type.GetCustomAttribute<SingletonServiceAttribute>() is not null)  return ServiceLifetime.Singleton;
        return null;
    }

    /// <summary>
    /// Registers <see cref="BearerTokenForwardingHandler"/> as a transient service.
    /// After calling this, attach the handler to any typed <c>HttpClient</c> registration:
    /// <code>
    /// builder.Services.AddHttpClient&lt;IOrderClient, OrderClient&gt;()
    ///     .AddHttpMessageHandler&lt;BearerTokenForwardingHandler&gt;();
    /// </code>
    /// <c>IHttpContextAccessor</c> is registered automatically by <see cref="AddJOrderCommon"/>;
    /// this method does not need to be called separately when that method is used.
    /// </summary>
    public static IHostApplicationBuilder AddJOrderBearerForwarding(this IHostApplicationBuilder builder)
    {
        builder.Services.AddTransient<BearerTokenForwardingHandler>();
        return builder;
    }
}

