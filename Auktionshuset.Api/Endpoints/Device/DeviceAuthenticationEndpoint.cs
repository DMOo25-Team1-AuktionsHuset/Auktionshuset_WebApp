using Auktionshuset.Api.Security;
using Auktionshuset.Application.Admin.Devices.AuthenticateDevice;
using Auktionshuset.Application.Devices.AuthenticateDevice;

namespace Auktionshuset.Api.Endpoints.Device;

public static class DeviceAuthenticationEndpoint
{
    public static IEndpointRouteBuilder MapDeviceAuthentication(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/device/authenticate",
                HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimitingExtensions.Login)
            .WithTags("Device")
            .WithName("AuthenticateDevice")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        endpoints.MapPost("/api/device/verify-setup", VerifySetupAsync)
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimitingExtensions.DeviceSetup)
            .WithTags("Device")
            .WithName("VerifyDeviceSetup")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        return endpoints;
    }

    private static async Task<IResult> VerifySetupAsync(
        AuthenticateDeviceCommand command, AuthenticateDeviceHandler handler,
        HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        if (!context.Request.IsHttps) return Results.BadRequest(new { error = "HTTPS is required." });
        var device = await handler.VerifySetupAsync(command, cancellationToken);
        return device is null ? Results.Unauthorized()
            : Results.Ok(new { verified = true, device.DeviceId, device.DeviceNumber });
    }

    private static async Task<IResult> HandleAsync(
        AuthenticateDeviceCommand command,
        AuthenticateDeviceHandler handler,
        DeviceJwtTokenService tokens,
        HttpContext context,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";

        if (!context.Request.IsHttps)
        {
            return Results.BadRequest(new
            {
                error = "HTTPS is required."
            });
        }

        ILogger logger =
            loggerFactory.CreateLogger("DeviceAuthentication");

        AuthenticateDeviceResult? device =
            await handler.HandleAsync(
                command,
                cancellationToken);

        if (device is null)
        {
            logger.LogWarning(
                "Device authentication rejected for {DeviceId}",
                command.DeviceId);

            return Results.Unauthorized();
        }

        IssuedDeviceToken issued = tokens.Issue(device);

        logger.LogInformation(
            "Device authenticated: {DeviceId}",
            device.DeviceId);

        return Results.Ok(new
        {
            accessToken = issued.AccessToken,
            tokenType = "Bearer",
            expiresAtUtc = issued.ExpiresAtUtc
        });
    }
}
