using System.Globalization;
using System.Text;
using Auktionshuset.Application.Abstraction.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Auktionshuset.Api.Security;

public static class DeviceSecurityExtensions
{
    public const string SchemeName = "DeviceBearer";
    public const string PolicyName = "DeviceOnly";

    public static IServiceCollection AddDeviceSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication()
            .AddJwtBearer(SchemeName, options =>
            {
                string signingKey =
                    configuration["DeviceAuthentication:SigningKey"]
                    ?? throw new InvalidOperationException(
                        "Device signing key is missing.");

                if (Encoding.UTF8.GetByteCount(signingKey) < 32)
                {
                    throw new InvalidOperationException(
                        "Device signing key must contain at least 32 bytes.");
                }

                options.MapInboundClaims = false;

                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer =
                            configuration["Authentication:Issuer"]
                            ?? throw new InvalidOperationException(
                                "Authentication issuer is missing."),

                        ValidateAudience = true,
                        ValidAudience = DeviceJwtTokenService.Audience,

                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(signingKey)),

                        ValidAlgorithms =
                            [SecurityAlgorithms.HmacSha256],

                        RequireSignedTokens = true,
                        RequireExpirationTime = true,
                        ValidateLifetime = true,

                        ClockSkew = TimeSpan.FromSeconds(30)
                    };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateDeviceAsync
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                policy.AddAuthenticationSchemes(SchemeName);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("client_type", "device");
            });
        });

        return services;
    }

    private static async Task ValidateDeviceAsync(
        TokenValidatedContext context)
    {
        string? subject =
            context.Principal?.FindFirst("sub")?.Value;

        string? versionClaim =
            context.Principal?.FindFirst("credential_version")?.Value;

        if (
            !Guid.TryParse(subject, out Guid deviceId) ||
            deviceId == Guid.Empty ||
            !int.TryParse(
                versionClaim,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int version) ||
            version < 1
        )
        {
            context.Fail("Invalid device access.");
            return;
        }

        IDeviceRepository repository =
            context.HttpContext.RequestServices
                .GetRequiredService<IDeviceRepository>();

        var credential = await repository.GetCredentialAsync(
            deviceId,
            context.HttpContext.RequestAborted);

        if (
            credential is null ||
            !credential.IsEnabled ||
            credential.CredentialVersion != version
        )
        {
            context.Fail("Invalid device access.");
        }
    }
}
