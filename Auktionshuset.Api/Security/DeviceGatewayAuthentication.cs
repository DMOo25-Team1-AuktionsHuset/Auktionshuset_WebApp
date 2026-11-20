using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Auktionshuset.Api.Security;

public static class DeviceGatewayAuthentication
{
    public const string Scheme = "DeviceGateway";
    public const string Scope = "device.gateway";
    public const string HubPath = "/hubs/device-gateway";

    public static void Configure(JwtBearerOptions options, IConfiguration configuration)
    {
        string Required(string key) => !string.IsNullOrWhiteSpace(configuration[key])
            ? configuration[key]!
            : throw new InvalidOperationException($"Missing configuration value '{key}'.");

        string key = Required("DeviceGateway:Authentication:SigningKey");
        if (Encoding.UTF8.GetByteCount(key) < 32 || key == configuration["Authentication:SigningKey"])
            throw new InvalidOperationException("Device gateway signing key must be at least 32 bytes and distinct from the user signing key.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Required("DeviceGateway:Authentication:Issuer"),
            ValidateAudience = true,
            ValidAudience = Required("DeviceGateway:Authentication:Audience"),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Path.StartsWithSegments(HubPath) &&
                    !string.IsNullOrEmpty(context.Request.Query["access_token"]))
                    context.Token = context.Request.Query["access_token"];
                return Task.CompletedTask;
            }
        };
    }
}
