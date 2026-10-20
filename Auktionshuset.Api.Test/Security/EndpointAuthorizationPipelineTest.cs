using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Auktionshuset.Api.Endpoints.Admin.CreateAuction;
using Auktionshuset.Api.Endpoints.Admin.Employee;
using Auktionshuset.Api.Endpoints.Admin.LotImage;
using Auktionshuset.Api.Endpoints.Admin.Lots;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Api.Security;
using Auktionshuset.Api.Services;
using Auktionshuset.Api.Test;
using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Domain;
using Auktionshuset.Infrastructure.Service;
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

        using HttpResponseMessage auctionHub = await client.PostAsync("/hubs/auction/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, auctionHub.StatusCode);
        using HttpResponseMessage employeeHub = await client.PostAsync("/hubs/employee/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, employeeHub.StatusCode);
        using HttpResponseMessage publicImage = await client.GetAsync("/uploads/lots/not-found.png");
        Assert.Equal(HttpStatusCode.NotFound, publicImage.StatusCode);
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
            (HttpMethod.Get, "/api/employee/", "employee read"),
            (HttpMethod.Get, $"/api/employee/{Guid.NewGuid()}", "employee detail"),
            (HttpMethod.Post, "/api/employee/", "employee write"),
            (HttpMethod.Put, $"/api/lots/{Guid.NewGuid()}", "lot write"),
            (HttpMethod.Get, "/api/lots/", "lot read"),
            (HttpMethod.Post, $"/api/lots/{Guid.NewGuid()}/image", "lot image upload"),
            (HttpMethod.Delete, $"/api/lots/{Guid.NewGuid()}/image", "lot image removal"),
            (HttpMethod.Post, "/api/auctions/", "auction create"),
            (HttpMethod.Get, "/api/auctions/", "auction read"),
            (HttpMethod.Get, "/api/employee/", "employee read"),
            (HttpMethod.Get, "/api/employee/options", "auctioneer options")
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

    [Fact]
    public async Task InvalidJwtChallengesApiAndHubRequestsWith401()
    {
        using IHost host = await StartServerAsync();
        using HttpClient client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

        using HttpResponseMessage api = await client.GetAsync("/api/lots/");
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
        using HttpResponseMessage hub = await client.PostAsync("/hubs/auction/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, hub.StatusCode);
    }

    [Fact]
    public async Task RoleMatrixProtectsAuctionEmployeeOptionAndHubEndpoints()
    {
        using IHost host = await StartServerAsync();
        using HttpClient client = host.GetTestClient();

        string lotAdminToken = CreateToken(host, SecurityRoles.LotAdmin);
        Assert.Equal(HttpStatusCode.OK, await SendAsync(client, lotAdminToken, HttpMethod.Get, "/api/lots/"));
        Assert.Equal(HttpStatusCode.OK, await SendHubNegotiateAsync(client, lotAdminToken, "/hubs/lot"));

        using HttpResponseMessage lotCreatedResponse = await SendJsonAsync(client, lotAdminToken, HttpMethod.Post, "/api/lots/", new
        {
            name = "Permission test lot",
            category = "Test",
            quantity = 1,
            estimatedValue = 10m,
            description = "Created by the authorization integration test",
            tags = new[] { "test" },
            auctionHouseId = AuctionHouseDefaults.DefaultAuctionHouseId
        });
        Assert.Equal(HttpStatusCode.Created, lotCreatedResponse.StatusCode);
        using JsonDocument lotCreated = await JsonDocument.ParseAsync(await lotCreatedResponse.Content.ReadAsStreamAsync());
        Guid lotId = lotCreated.RootElement.GetProperty("lotId").GetGuid();
        using (HttpResponseMessage lotUpdatedResponse = await SendJsonAsync(client, lotAdminToken, HttpMethod.Put, $"/api/lots/{lotId}", new
        {
            name = "Updated permission test lot",
            category = "Test",
            quantity = 2,
            estimatedValue = 11m,
            description = "Updated by the authorization integration test",
            tags = new[] { "test" },
            auctionHouseId = AuctionHouseDefaults.DefaultAuctionHouseId
        }))
        {
            Assert.Equal(HttpStatusCode.OK, lotUpdatedResponse.StatusCode);
        }

        using (var upload = new HttpRequestMessage(HttpMethod.Post, $"/api/lots/{lotId}/image"))
        {
            var form = new MultipartFormDataContent();
            var image = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52]);
            image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(image, "file", "test.png");
            upload.Content = form;
            upload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", lotAdminToken);
            using HttpResponseMessage uploaded = await client.SendAsync(upload);
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
            using JsonDocument uploadResult = await JsonDocument.ParseAsync(await uploaded.Content.ReadAsStreamAsync());
            string imageUrl = uploadResult.RootElement.GetProperty("imageUrl").GetString()!;
            using HttpResponseMessage publicImage = await client.GetAsync(imageUrl);
            Assert.Equal(HttpStatusCode.OK, publicImage.StatusCode);
            Assert.Equal(HttpStatusCode.OK, await SendAsync(client, lotAdminToken, HttpMethod.Delete, $"/api/lots/{lotId}/image"));
        }
        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(client, lotAdminToken, HttpMethod.Delete, $"/api/lots/{lotId}"));

        (HttpMethod Method, string Path)[] lotAdminDeniedRequests =
        [
            (HttpMethod.Get, "/api/auctions/"),
            (HttpMethod.Get, $"/api/auctions/{Guid.NewGuid()}"),
            (HttpMethod.Post, "/api/auctions/"),
            (HttpMethod.Put, $"/api/auctions/{Guid.NewGuid()}"),
            (HttpMethod.Delete, $"/api/auctions/{Guid.NewGuid()}"),
            (HttpMethod.Get, "/api/employee/"),
            (HttpMethod.Get, $"/api/employee/{Guid.NewGuid()}"),
            (HttpMethod.Get, "/api/employee/options"),
            (HttpMethod.Post, "/api/employee/"),
            (HttpMethod.Put, $"/api/employee/{Guid.NewGuid()}"),
            (HttpMethod.Delete, $"/api/employee/{Guid.NewGuid()}")
        ];
        foreach ((HttpMethod method, string path) in lotAdminDeniedRequests)
        {
            Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(client, lotAdminToken, method, path));
        }
        Assert.Equal(HttpStatusCode.Forbidden, await SendHubNegotiateAsync(client, lotAdminToken, "/hubs/auction"));
        Assert.Equal(HttpStatusCode.Forbidden, await SendHubNegotiateAsync(client, lotAdminToken, "/hubs/employee"));

        string auctionAdminToken = CreateToken(host, SecurityRoles.AuctionAdmin);
        Assert.Equal(HttpStatusCode.OK, await SendAsync(client, auctionAdminToken, HttpMethod.Get, "/api/auctions/"));
        using HttpResponseMessage auctionCreatedResponse = await SendJsonAsync(client, auctionAdminToken, HttpMethod.Post, "/api/auctions/", new
        {
            name = "Permission test auction",
            startsAt = DateTime.Now.AddDays(1),
            endsAt = DateTime.Now.AddDays(1).AddHours(1),
            employeeId = (Guid?)null,
            lots = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Created, auctionCreatedResponse.StatusCode);
        using JsonDocument auctionCreated = await JsonDocument.ParseAsync(await auctionCreatedResponse.Content.ReadAsStreamAsync());
        Guid auctionId = auctionCreated.RootElement.GetProperty("auctionId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await SendJsonAsync(client, auctionAdminToken, HttpMethod.Put, $"/api/auctions/{auctionId}", new
        {
            name = "Updated permission test auction",
            startsAt = DateTime.Now.AddDays(1),
            endsAt = DateTime.Now.AddDays(1).AddHours(1),
            employeeId = (Guid?)null,
            lots = Array.Empty<object>()
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(client, auctionAdminToken, HttpMethod.Delete, $"/api/auctions/{auctionId}"));
        Assert.Equal(HttpStatusCode.OK, await SendHubNegotiateAsync(client, auctionAdminToken, "/hubs/auction"));
        Assert.Equal(HttpStatusCode.OK, await SendHubNegotiateAsync(client, auctionAdminToken, "/hubs/lot"));
        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(client, auctionAdminToken, HttpMethod.Get, "/api/employee/"));
        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(client, auctionAdminToken, HttpMethod.Get, $"/api/employee/{Guid.NewGuid()}"));
        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(client, auctionAdminToken, HttpMethod.Post, "/api/employee/"));
        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(client, auctionAdminToken, HttpMethod.Put, $"/api/employee/{Guid.NewGuid()}"));
        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(client, auctionAdminToken, HttpMethod.Delete, $"/api/employee/{Guid.NewGuid()}"));
        Assert.Equal(HttpStatusCode.Forbidden, await SendHubNegotiateAsync(client, auctionAdminToken, "/hubs/employee"));

        using HttpResponseMessage optionsResponse = await SendResponseAsync(
            client, auctionAdminToken, HttpMethod.Get, "/api/employee/options");
        Assert.Equal(HttpStatusCode.OK, optionsResponse.StatusCode);
        using JsonDocument options = await JsonDocument.ParseAsync(await optionsResponse.Content.ReadAsStreamAsync());
        JsonElement[] optionList = options.RootElement.EnumerateArray().ToArray();
        Assert.NotEmpty(optionList);
        foreach (JsonElement option in optionList)
        {
            Assert.Equal(
                ["displayName", "employeeId"],
                option.EnumerateObject().Select(property => property.Name).Order());
        }
        Assert.Contains(optionList, option => option.GetProperty("displayName").GetString() == "Henrik Sørensen");

        string adminToken = CreateToken(host, SecurityRoles.Admin);
        Assert.Equal(HttpStatusCode.OK, await SendAsync(client, adminToken, HttpMethod.Get, "/api/employee/"));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(client, adminToken, HttpMethod.Get, "/api/employee/options"));
        using HttpResponseMessage employeeCreatedResponse = await SendJsonAsync(client, adminToken, HttpMethod.Post, "/api/employee/", new
        {
            firstName = "Permission",
            lastName = "Test",
            birthDate = new DateOnly(1990, 5, 20),
            address = "Testvej 1",
            auctionHouseId = AuctionHouseDefaults.DefaultAuctionHouseId
        });
        Assert.Equal(HttpStatusCode.Created, employeeCreatedResponse.StatusCode);
        using JsonDocument employeeCreated = await JsonDocument.ParseAsync(await employeeCreatedResponse.Content.ReadAsStreamAsync());
        Guid employeeId = employeeCreated.RootElement.GetProperty("employeeId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, await SendAsync(client, adminToken, HttpMethod.Get, $"/api/employee/{employeeId}"));
        Assert.Equal(HttpStatusCode.OK, (await SendJsonAsync(client, adminToken, HttpMethod.Put, $"/api/employee/{employeeId}", new
        {
            firstName = "Permission",
            lastName = "Updated",
            birthDate = new DateOnly(1990, 5, 20),
            address = "Testvej 2",
            auctionHouseId = AuctionHouseDefaults.DefaultAuctionHouseId
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(client, adminToken, HttpMethod.Delete, $"/api/employee/{employeeId}"));
        Assert.Equal(HttpStatusCode.OK, await SendHubNegotiateAsync(client, adminToken, "/hubs/employee"));
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
                    services.AddSignalR();
                    services.AddSingleton<IAuctionRepository, InMemoryAuctionRepository>();
                    services.AddSingleton<IEmployeeRepository, InMemoryEmployeeRepository>();
                    services.AddSingleton<ILotRepository, InMemoryLotRepository>();
                    services.AddSingleton<IIntegrationEventPublisher, RecordingEventPublisher>();
                    services.AddSingleton<IOutboxWriter, RecordingEventPublisher>();
                    services.AddSingleton<IUnitOfWork, RecordingUnitOfWork>();
                    services.AddSingleton<ILotImageStore>(_ => new TestLotImageStore());
                    services.AddSingleton(configuration);
                    services.AddSecurityServices(configuration);
                    services.AddSingleton<IEmployeeRepository, InMemoryEmployeeRepository>();
                    services.AddSingleton<IAuctionRepository, InMemoryAuctionRepository>();
                    services.AddSingleton<ILotRepository, InMemoryLotRepository>();
                    services.AddSingleton<IIntegrationEventPublisher, RecordingEventPublisher>();
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
                        endpoints.MapStoredLotImages();
                        endpoints.MapHub<AuctionHub>("/hubs/auction").RequireAuthorization(SecurityPolicies.CanViewAuctions);
                        endpoints.MapHub<EmployeeHub>("/hubs/employee").RequireAuthorization(SecurityPolicies.CanViewEmployees);
                        endpoints.MapHub<LotHub>("/hubs/lot").RequireAuthorization(SecurityPolicies.CanViewLots);
                    });
                });
            })
            .StartAsync();
    }

    private static string CreateCustomerToken(IHost host)
    {
        IAccessTokenService tokenService = host.Services.GetRequiredService<IAccessTokenService>();
        return CreateToken(host, SecurityRoles.Customer);
    }

    private static string CreateToken(IHost host, string role)
    {
        IAccessTokenService tokenService = host.Services.GetRequiredService<IAccessTokenService>();
        var user = new AuthUser
        {
            UserId = Guid.NewGuid(),
            Email = $"{role.ToLowerInvariant()}@example.test",
            PasswordHash = string.Empty,
            Roles = [role],
            Permissions = SecurityRolePermissions.GetPermissions(role)
        };

        return tokenService.Issue(user).Value;
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client, string token, HttpMethod method, string path, object body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static async Task<HttpStatusCode> SendAsync(
        HttpClient client, string token, HttpMethod method, string path)
    {
        using HttpResponseMessage response = await SendResponseAsync(client, token, method, path);
        return response.StatusCode;
    }

    private static async Task<HttpResponseMessage> SendResponseAsync(
        HttpClient client, string token, HttpMethod method, string path)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private sealed class TestLotImageStore : ILotImageStore
    {
        private readonly Dictionary<string, LotImageContent> images = [];

        public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            string fileName = $"{Guid.NewGuid():N}{extension}";
            string contentType = extension == ".png" ? "image/png" : extension == ".jpg" ? "image/jpeg" : "image/webp";
            images[fileName] = new LotImageContent(buffer.ToArray(), contentType);
            return fileName;
        }

        public Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken) =>
            Task.FromResult(images.Remove(fileName));

        public Task<LotImageContent?> GetAsync(string fileName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            images.TryGetValue(fileName, out LotImageContent? image);
            return Task.FromResult(image);
        }
    }

    private static async Task<HttpStatusCode> SendHubNegotiateAsync(HttpClient client, string token, string hubPath)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{hubPath}/negotiate?negotiateVersion=1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await client.SendAsync(request);
        return response.StatusCode;
    }
}
