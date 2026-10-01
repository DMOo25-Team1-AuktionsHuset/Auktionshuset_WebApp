using System.Net;
using System.Net.Http.Headers;
using Auktionshuset.Api.Endpoints.Admin.CreateAuction;
using Auktionshuset.Api.Endpoints.Admin.Employee;
using Auktionshuset.Api.Endpoints.Admin.Lots;
using Auktionshuset.Api.Security;
using Auktionshuset.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Auktionshuset.Api.Test.Security;

public class EndpointAuthorizationPipelineTest
{
    [Fact]
    public async Task MappedApiEndpoints_ChallengeAnonymousRequests()
    {
        using IHost host = await StartServerAsync();
        using HttpClient client = host.GetTestClient();

        using HttpResponseMessage response = await client.GetAsync("/api/employee/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MappedApiEndpoints_ForbidAuthenticatedUserWithoutRequiredRolesOrPermissions()
    {
        using IHost host = await StartServerAsync();
        using HttpClient client = host.GetTestClient();
        string token = CreateCustomerToken(host);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        (HttpMethod Method, string Path, string Endpoint)[] requests =
        [
            (HttpMethod.Post, "/api/employee/", "employee write"),
            (HttpMethod.Put, $"/api/lots/{Guid.NewGuid()}", "lot write"),
            (HttpMethod.Get, "/api/lots/", "lot read"),
            (HttpMethod.Post, $"/api/lots/{Guid.NewGuid()}/image", "lot image upload"),
            (HttpMethod.Delete, $"/api/lots/{Guid.NewGuid()}/image", "lot image removal"),
            (HttpMethod.Post, "/api/auctions/", "auction create")
        ];

        foreach ((HttpMethod method, string path, string endpoint) in requests)
        {
            using var request = new HttpRequestMessage(method, path);
            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.True(
                response.StatusCode == HttpStatusCode.Forbidden,
                $"Expected {endpoint} to return 403 but got {(int)response.StatusCode}.");
        }
    }

    private static async Task<IHost> StartServerAsync()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:EnforceAuthorization"] = "false",
                ["Authentication:SigningKey"] = "test-signing-key-with-at-least-32-characters",
                ["Authentication:Issuer"] = "Auktionshuset.Api.Test",
                ["Authentication:Audience"] = "Auktionshuset.Client.Test"
            })
            .Build();

        return await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddApiServices();
                    services.AddSingleton(configuration);
                    services.AddSecurityServices(configuration);
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapEmployeeEndpoints();
                        endpoints.MapLotEndpoints();
                        endpoints.MapAuctionEndpoints();
                    });
                });
            })
            .StartAsync();
    }

    private static string CreateCustomerToken(IHost host)
    {
        IAccessTokenService tokenService = host.Services.GetRequiredService<IAccessTokenService>();
        var user = new AuthUser
        {
            UserId = Guid.NewGuid(),
            Email = "customer@example.test",
            PasswordHash = string.Empty,
            Roles = [SecurityRoles.Customer],
            Permissions = []
        };

        return tokenService.Issue(user).Value;
    }
}
