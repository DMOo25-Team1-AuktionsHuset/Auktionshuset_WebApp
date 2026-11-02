using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Auktionshuset.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Auktionshuset.Api.Services.DeviceGateway;

public static class DeviceGatewayClientSecurity
{
    public static Uri ValidateHubUrl(string url, bool isDevelopment, bool allowInsecureLoopback)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
             !(isDevelopment && allowInsecureLoopback && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
            uri.AbsolutePath != DeviceGatewayAuthentication.HubPath ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("DeviceGateway:HubUrl must use HTTPS and the exact device gateway hub path, without credentials, query or fragment. HTTP is allowed only for explicit development loopback.");
        return uri;
    }

    public static string CreateToken(IConfiguration configuration)
    {
        // Reuse the hub's validation configuration so key separation and required values cannot drift.
        var options = new JwtBearerOptions();
        DeviceGatewayAuthentication.Configure(options, configuration);
        var parameters = options.TokenValidationParameters;
        var token = new JwtSecurityToken(
            issuer: parameters.ValidIssuer,
            audience: parameters.ValidAudience,
            claims: [new Claim("sub", "internal-device-gateway"),
                new Claim("scope", DeviceGatewayAuthentication.Scope),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(parameters.IssuerSigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
