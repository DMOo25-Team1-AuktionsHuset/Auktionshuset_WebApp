using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Auktionshuset.Api.Test.Security;

public class DeviceGatewayAuthorizationTests
{
    private const string UserKey = "user-signing-key-at-least-32-bytes-long";
    private const string GatewayKey = "gateway-signing-key-at-least-32-bytes-long";

    [Fact]
    public async Task GatewayRequiresItsOwnSignedScopedTokenAndCannotAccessStaffHub()
    {
        using var host = await StartAsync();
        using var client = host.GetTestClient();
        async Task<HttpStatusCode> Negotiate(string path, string? token = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path + "/negotiate?negotiateVersion=1");
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }

        var gateway = Token(GatewayKey, "gateway-issuer", "gateway-audience", DeviceGatewayAuthentication.Scope);
        Assert.Equal(HttpStatusCode.Unauthorized, await Negotiate(DeviceGatewayAuthentication.HubPath));
        Assert.Equal(HttpStatusCode.Unauthorized, await Negotiate(DeviceGatewayAuthentication.HubPath, Token(UserKey, "user-issuer", "user-audience", DeviceGatewayAuthentication.Scope)));
        Assert.Equal(HttpStatusCode.Forbidden, await Negotiate(DeviceGatewayAuthentication.HubPath, Token(GatewayKey, "gateway-issuer", "gateway-audience", "other")));
        Assert.Equal(HttpStatusCode.Unauthorized, await Negotiate(DeviceGatewayAuthentication.HubPath, Token(GatewayKey, "wrong", "gateway-audience", DeviceGatewayAuthentication.Scope)));
        Assert.Equal(HttpStatusCode.Unauthorized, await Negotiate(DeviceGatewayAuthentication.HubPath, Token(GatewayKey, "gateway-issuer", "wrong", DeviceGatewayAuthentication.Scope)));
        Assert.Equal(HttpStatusCode.OK, await Negotiate(DeviceGatewayAuthentication.HubPath, gateway));
        using var queryResponse = await client.PostAsync(DeviceGatewayAuthentication.HubPath + "/negotiate?negotiateVersion=1&access_token=" + gateway, null);
        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await Negotiate("/hubs/auction", gateway));
    }

    private static string Token(string key, string issuer, string audience, string scope)
    {
        var token = new JwtSecurityToken(issuer, audience,
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("scope", scope)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<IHost> StartAsync()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:SigningKey"] = UserKey,
            ["Authentication:Issuer"] = "user-issuer",
            ["Authentication:Audience"] = "user-audience",
            ["DeviceGateway:Authentication:SigningKey"] = GatewayKey,
            ["DeviceGateway:Authentication:Issuer"] = "gateway-issuer",
            ["DeviceGateway:Authentication:Audience"] = "gateway-audience"
        }).Build();
        return await new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddSignalR();
                services.AddSecurityServices(configuration);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapHub<DeviceGatewayHub>(DeviceGatewayAuthentication.HubPath).RequireAuthorization(SecurityPolicies.DeviceGateway);
                    endpoints.MapHub<AuctionHub>("/hubs/auction").RequireAuthorization(SecurityPolicies.CanViewAuctions);
                });
            })).StartAsync();
    }
}
