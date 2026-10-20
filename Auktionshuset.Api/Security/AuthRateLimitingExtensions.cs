using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

namespace Auktionshuset.Api.Security;

public static class AuthRateLimitingExtensions
{
    public const string Login = "auth-login-ip";
    public const string Refresh = "auth-refresh-ip";
    public const string Logout = "auth-logout-ip";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            // Never trust an arbitrary caller's X-Forwarded-For.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (string address in configuration.GetSection("Authentication:KnownProxies").Get<string[]>() ?? [])
                options.KnownProxies.Add(IPAddress.Parse(address));
            // An empty trust list in the middleware trusts everyone; disable forwarding instead.
            if (options.KnownProxies.Count == 0) options.ForwardedHeaders = ForwardedHeaders.None;
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = context.Lease.TryGetMetadata(
                    MetadataName.RetryAfter, out TimeSpan retryAfter)
                    ? Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture) : "60";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Too many authentication requests. Try again later."
                }, cancellationToken);
            };
            foreach (var (name, limit) in new[] { (Login, 5), (Refresh, 30), (Logout, 30) })
                options.AddPolicy(name, context => RateLimitPartition.GetSlidingWindowLimiter(
                    GetClientIp(context), _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = limit, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6,
                        QueueLimit = 0, AutoReplenishment = true
                    }));
        });
        return services;
    }

    public static string GetClientIp(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress;
        return (ip?.IsIPv4MappedToIPv6 == true ? ip.MapToIPv4() : ip)?.ToString() ?? "unknown";
    }
}
