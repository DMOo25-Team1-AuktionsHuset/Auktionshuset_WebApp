using System.IdentityModel.Tokens.Jwt;
using Auktionshuset.Api.Security;
using Auktionshuset.Api.Services.DeviceGateway;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Auktionshuset.Api.Test.Security;

public class DeviceGatewayClientSecurityTests
{
    private static IConfiguration Configuration(string gatewayKey = "gateway-signing-key-at-least-32-bytes-long") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:SigningKey"] = "user-signing-key-at-least-32-bytes-long",
            ["DeviceGateway:Authentication:SigningKey"] = gatewayKey,
            ["DeviceGateway:Authentication:Issuer"] = "gateway-issuer",
            ["DeviceGateway:Authentication:Audience"] = "gateway-audience"
        }).Build();

    [Fact]
    public void TokensAreFreshScopedAndAcceptedByGatewayValidation()
    {
        var configuration = Configuration();
        var first = DeviceGatewayClientSecurity.CreateToken(configuration);
        var second = DeviceGatewayClientSecurity.CreateToken(configuration);
        Assert.NotEqual(first, second);
        var options = new JwtBearerOptions();
        DeviceGatewayAuthentication.Configure(options, configuration);
        var principal = new JwtSecurityTokenHandler().ValidateToken(second, options.TokenValidationParameters, out var validated);
        Assert.Equal(DeviceGatewayAuthentication.Scope, principal.FindFirst("scope")?.Value);
        Assert.InRange(validated.ValidTo - DateTime.UtcNow, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void RejectsSharedOrShortSigningKey()
    {
        Assert.Throws<InvalidOperationException>(() => DeviceGatewayClientSecurity.CreateToken(Configuration("short")));
        Assert.Throws<InvalidOperationException>(() => DeviceGatewayClientSecurity.CreateToken(Configuration("user-signing-key-at-least-32-bytes-long")));
    }

    [Theory]
    [InlineData("https://localhost/hubs/device-gateway", false, false, true)]
    [InlineData("http://localhost/hubs/device-gateway", true, true, true)]
    [InlineData("http://localhost/hubs/device-gateway", false, true, false)]
    [InlineData("http://localhost/hubs/device-gateway", true, false, false)]
    [InlineData("http://example.com/hubs/device-gateway", true, true, false)]
    [InlineData("https://localhost/hubs/auction", true, true, false)]
    [InlineData("https://user:password@localhost/hubs/device-gateway", true, true, false)]
    [InlineData("https://localhost/hubs/device-gateway?x=1", true, true, false)]
    [InlineData("https://localhost/hubs/device-gateway#fragment", true, true, false)]
    public void RestrictsUrl(string url, bool development, bool insecureLoopback, bool valid)
    {
        if (valid)
            Assert.NotNull(DeviceGatewayClientSecurity.ValidateHubUrl(url, development, insecureLoopback));
        else
            Assert.Throws<InvalidOperationException>(() => DeviceGatewayClientSecurity.ValidateHubUrl(url, development, insecureLoopback));
    }
}
