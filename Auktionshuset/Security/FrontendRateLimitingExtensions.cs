using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

namespace Auktionshuset.Security;

public static class FrontendRateLimitingExtensions
{
    public const string Register = "frontend-register-ip";

    public static IServiceCollection AddFrontendRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Register, context => RateLimitPartition.GetSlidingWindowLimiter(
                GetClientIp(context), _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 5, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6,
                    QueueLimit = 0, AutoReplenishment = true
                }));
        });
        return services;
    }

    private static string GetClientIp(HttpContext context)
    {
        IPAddress? ip = context.Connection.RemoteIpAddress;
        return (ip?.IsIPv4MappedToIPv6 == true ? ip.MapToIPv4() : ip)?.ToString() ?? "unknown";
    }
}
