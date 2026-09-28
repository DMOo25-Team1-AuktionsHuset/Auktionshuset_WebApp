using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Auktionshuset.Api.Security;

public sealed record IssuedAccessToken(string Value, DateTimeOffset ExpiresAtUtc);

public interface IAccessTokenService
{
    IssuedAccessToken Issue(AuthUser user);
}
public sealed class JwtTokenService(IConfiguration configuration, TimeProvider timeProvider) : IAccessTokenService

{
    private const int DefaultLifetimeMinutes = 60;

    public IssuedAccessToken Issue(AuthUser user)
    {
        var issuer = GetRequiredValue("Authentication:Issuer");
        var audience = GetRequiredValue("Authentication:Audience");
        var signingKey = GetRequiredValue("Authentication:SigningKey");

        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException(
                 "Configuration value 'Authentication:SigningKey' must contain at least 32 bytes.");
        }

        var lifetimeMinutes = configuration.GetValue(
        "Authentication:AccessTokenLifetimeMinutes",
            DefaultLifetimeMinutes);

        if (lifetimeMinutes <= 0)
        {
            throw new InvalidOperationException(
                "Configuration value 'Authentication:AccessTokenLifetimeMinutes' must be greater than zero");
        }

        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt.AddMinutes(lifetimeMinutes);
        var claims = CreateClaims(user);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            issuedAt.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);

        return new IssuedAccessToken(
        new JwtSecurityTokenHandler().WriteToken(token),expiresAt);
    }
    
    private static IEnumerable<Claim> CreateClaims(AuthUser user)
    {
        yield return new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString());
        yield return new Claim(JwtRegisteredClaimNames.Email, user.Email);
        yield return new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString());

        foreach (var role in user.Roles.Distinct(StringComparer.Ordinal))
        {
            yield return new Claim(ClaimTypes.Role, role);
        }

        foreach (var permission in user.Permissions.Distinct(StringComparer.Ordinal))
        {
            yield return new Claim(SecurityPermissions.ClaimType, permission);
        }
    }

    private string GetRequiredValue(string key) =>
        configuration[key]
        ?? throw new InvalidOperationException($"Missing configuration value '{key}'.");
}
