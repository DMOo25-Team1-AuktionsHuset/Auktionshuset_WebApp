using System.Net;

namespace Auktionshuset.Api.Extensions;

public static class DevelopmentHttpsRedirectionExtensions
{
    public static IApplicationBuilder UseDevelopmentAwareHttpsRedirection(
        this IApplicationBuilder app, bool isDevelopment)
    {
        return app.UseWhen(context =>
            !isDevelopment ||
            (context.Request.Path != "/ws/device/test" &&
             !(context.Request.Path == "/ws/device" &&
               IsLoopback(context.Connection.RemoteIpAddress) &&
               IsLoopback(context.Connection.LocalIpAddress))),
            branch => branch.UseHttpsRedirection());
    }

    private static bool IsLoopback(IPAddress? address) =>
        address is not null && IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address);
}
