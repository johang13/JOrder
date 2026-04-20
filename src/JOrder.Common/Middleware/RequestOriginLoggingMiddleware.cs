using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JOrder.Common.Middleware;

public sealed class RequestOriginLoggingMiddleware(RequestDelegate next, ILogger<RequestOriginLoggingMiddleware> logger)
{
    private static readonly HashSet<string> HealthProbePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/startupz",
        "/readyz",
        "/livez"
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsHealthProbePath(context.Request.Path))
        {
            await next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            var request = context.Request;
            var remoteIp = context.Connection.RemoteIpAddress?.ToString();
            var forwardedFor = request.Headers["X-Forwarded-For"].ToString();
            var forwardedProto = request.Headers["X-Forwarded-Proto"].ToString();
            var origin = request.Headers.Origin.ToString();
            var userAgent = request.Headers.UserAgent.ToString();

            logger.LogInformation(
                "Request origin: {Method} {Path} -> {StatusCode} ({ElapsedMs} ms). TraceId={TraceId}, RemoteIp={RemoteIp}, ForwardedFor={ForwardedFor}, ForwardedProto={ForwardedProto}, Origin={Origin}, Host={Host}, UserAgent={UserAgent}",
                request.Method,
                request.Path,
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds,
                context.TraceIdentifier,
                remoteIp,
                string.IsNullOrWhiteSpace(forwardedFor) ? null : forwardedFor,
                string.IsNullOrWhiteSpace(forwardedProto) ? null : forwardedProto,
                string.IsNullOrWhiteSpace(origin) ? null : origin,
                request.Host.Value,
                string.IsNullOrWhiteSpace(userAgent) ? null : userAgent);
        }
    }

    private static bool IsHealthProbePath(PathString path)
    {
        var pathValue = path.Value;
        return pathValue is not null && HealthProbePaths.Contains(pathValue);
    }
}
