using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Auktionshuset.Application.Admin.Devices.AuthenticateDevice;
using Microsoft.IdentityModel.Tokens;

namespace Auktionshuset.Api.Security;

public sealed class DeviceJwtTokenService(
    IConfiguration configuration,
    TimeProvider timeProvider)
{
    public const string Audience = "Auktionshuset.Device";

    private const int LifetimeMinutes = 10;

    public IssuedDeviceToken Issue(
        AuthenticateDeviceResult device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (
            device.DeviceId == Guid.Empty ||
            device.CredentialVersion < 1
        )
        {
            throw new ArgumentException(
                "Invalid authenticated device.",
                nameof(device));
        }

        string issuer = GetRequiredValue(
            "Authentication:Issuer");

        string signingKey = GetRequiredValue(
            "DeviceAuthentication:SigningKey");

        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException(
                "Device signing key must contain at least 32 bytes.");
        }

        if (string.Equals(
            configuration["Authentication:Audience"],
            Audience,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Device and employee audiences must differ.");
        }

        DateTimeOffset issuedAt = timeProvider.GetUtcNow();
        DateTimeOffset expiresAt =
            issuedAt.AddMinutes(LifetimeMinutes);

        Claim[] claims =
        [
            new Claim(
                JwtRegisteredClaimNames.Sub,
                device.DeviceId.ToString()),

            new Claim(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString()),

            new Claim(
                "client_type",
                "device"),

            new Claim(
                "device_number",
                device.DeviceNumber.ToString(
                    CultureInfo.InvariantCulture)),

            new Claim(
                "credential_version",
                device.CredentialVersion.ToString(
                    CultureInfo.InvariantCulture))
        ];

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedDeviceToken
        {
            AccessToken =
                new JwtSecurityTokenHandler().WriteToken(token),

            ExpiresAtUtc = expiresAt
        };
    }

    private string GetRequiredValue(string key)
    {
        string? value = configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Missing configuration value '{key}'.");
        }

        return value;
    }
}
