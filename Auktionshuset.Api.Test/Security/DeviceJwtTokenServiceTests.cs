using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Auktionshuset.Api.Security;
using Auktionshuset.Application.Admin.Devices.AuthenticateDevice;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Auktionshuset.Api.Test.Security;

public sealed class DeviceJwtTokenServiceTests
{
    // Offentlig testnøgle. Må ikke bruges i det rigtige system.
    private const string SigningKey =
        "public-test-signing-key-not-for-real-devices-12345";

    private const string Issuer = "Auktionshuset.DeviceTests";
    private const string EmployeeAudience = "Auktionshuset.WebTests";

    private static readonly Guid DeviceId = Guid.NewGuid();

    [Fact]
    public void IssuedToken_HasValidSignatureIdentityAndLifetime()
    {
        DateTimeOffset now = GetNow();
        IssuedDeviceToken issued = CreateToken(now);

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };

        ClaimsPrincipal principal = handler.ValidateToken(
            issued.AccessToken,
            CreateValidationParameters(),
            out SecurityToken validatedToken);

        Assert.Equal(
            DeviceId.ToString(),
            principal.FindFirst("sub")?.Value);

        Assert.Equal(
            "device",
            principal.FindFirst("client_type")?.Value);

        Assert.Equal(
            "42",
            principal.FindFirst("device_number")?.Value);

        Assert.Equal(
            "3",
            principal.FindFirst("credential_version")?.Value);

        Assert.NotNull(principal.FindFirst("jti"));

        Assert.Equal(
            now.AddMinutes(10),
            issued.ExpiresAtUtc);

        Assert.Equal(
            now.AddMinutes(10).UtcDateTime,
            validatedToken.ValidTo);

        Assert.DoesNotContain(
            principal.Claims,
            claim =>
                claim.Type == "role" ||
                claim.Type == ClaimTypes.Role ||
                claim.Type == "permission");
    }

    [Fact]
    public void ModifiedToken_IsRejected()
    {
        IssuedDeviceToken issued = CreateToken(GetNow());

        string[] parts = issued.AccessToken.Split('.');

        string payloadJson = Encoding.UTF8.GetString(
            Base64UrlEncoder.DecodeBytes(parts[1]));

        JsonNode payload = JsonNode.Parse(payloadJson)!;

        // Angriberen ændrer indholdet og beholder signaturen.
        payload["client_type"] = "admin";

        parts[1] = Base64UrlEncoder.Encode(
            payload.ToJsonString());

        string modifiedToken = string.Join(".", parts);

        var handler = new JwtSecurityTokenHandler();

        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(
                modifiedToken,
                CreateValidationParameters(),
                out _));
    }

    [Fact]
    public void ExpiredToken_IsRejected()
    {
        IssuedDeviceToken issued =
            CreateToken(GetNow().AddHours(-1));

        var handler = new JwtSecurityTokenHandler();

        Assert.Throws<SecurityTokenExpiredException>(() =>
            handler.ValidateToken(
                issued.AccessToken,
                CreateValidationParameters(),
                out _));
    }

    [Fact]
    public void DeviceToken_IsRejectedForEmployeeAudience()
    {
        IssuedDeviceToken issued = CreateToken(GetNow());

        TokenValidationParameters validation =
            CreateValidationParameters();

        validation.ValidAudience = EmployeeAudience;

        var handler = new JwtSecurityTokenHandler();

        Assert.Throws<SecurityTokenInvalidAudienceException>(() =>
            handler.ValidateToken(
                issued.AccessToken,
                validation,
                out _));
    }

    private static IssuedDeviceToken CreateToken(
        DateTimeOffset issuedAt)
    {
        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Authentication:Issuer"] = Issuer,
                        ["Authentication:Audience"] = EmployeeAudience,
                        ["DeviceAuthentication:SigningKey"] = SigningKey
                    })
                .Build();

        var service = new DeviceJwtTokenService(
            configuration,
            new FixedTimeProvider(issuedAt));

        return service.Issue(
            new AuthenticateDeviceResult(
                DeviceId,
                42,
                3));
    }

    private static TokenValidationParameters
        CreateValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,

            ValidateAudience = true,
            ValidAudience = DeviceJwtTokenService.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(SigningKey)),

            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,

            ClockSkew = TimeSpan.Zero
        };
    }

    private static DateTimeOffset GetNow()
    {
        // JWT-tider har sekundpræcision.
        return DateTimeOffset.FromUnixTimeSeconds(
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
