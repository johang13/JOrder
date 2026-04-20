using JOrder.Common.Helpers;
using JOrder.Common.Options;
using JOrder.Common.Options.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JOrder.Common.Extensions;

public static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddJwtAuthentication(this IServiceCollection services,
        Action<JwtBearerOptions> configureOptions)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(configureOptions);
        services.AddAuthorization();
        return services;
    }

    internal static IServiceCollection AddJwtValidation(this IServiceCollection services,
        Action<JwtBearerOptions>? configureJwtBearer = null)
    {
        services.AddJOrderOptions<JwtValidationOptions>();
        services.AddJwtAuthentication(_ => { });
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtValidationOptions>>((jwtOpts, validationOpts) =>
            {
                jwtOpts.Authority = validationOpts.Value.Authority;
                jwtOpts.Audience = validationOpts.Value.Audience;
                jwtOpts.RequireHttpsMetadata = validationOpts.Value.RequireHttpsMetadata;
                configureJwtBearer?.Invoke(jwtOpts);
            });
        return services;
    }

    internal static IServiceCollection AddJOrderOpenApi(this IServiceCollection services, string title)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = title;
                return Task.CompletedTask;
            });
            options.AddDocumentTransformer<SecuritySchemeTransformer>();
        });
        return services;
    }

    internal static IServiceCollection AddJOrderOptions<TJOrderOptions>(this IServiceCollection services)
        where TJOrderOptions : class, IJOrderOptions
    {
        services
            .AddOptions<TJOrderOptions>()
            .BindConfiguration(TJOrderOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services;
    }
}

