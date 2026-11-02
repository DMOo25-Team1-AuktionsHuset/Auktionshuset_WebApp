using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Auktionshuset.Contracts.Dto.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Auktionshuset.Tests.Auth;

public class DeviceAdminPageTests
{
    [Theory]
    [InlineData("Admin")]
    [InlineData("AuctionAdmin")]
    [InlineData("LotAdmin")]
    [InlineData("Employee")]
    public async Task StaffSeeDeviceListsFromActualEndpoints(string role)
    {
        using var factory = new Factory(role);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client);
        using var response = await client.GetAsync("/admin/devices");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Anna Kunde", html);
        Assert.Contains("Testauktion", html);
        Assert.Contains("Device 12", html);
        Assert.Contains("Device 13", html);
        Assert.Contains(factory.SessionId.ToString(), html);
        Assert.Contains("Frigiv device", html);
        Assert.Contains("Auktionshus-id", html);
        Assert.Contains("/api/staff/devices/customers", factory.Paths);
        Assert.Contains("/api/staff/devices/auctions", factory.Paths);
        Assert.Contains("/api/staff/devices/available", factory.Paths);
        Assert.Contains("/api/staff/devices/sessions", factory.Paths);
        Assert.DoesNotContain("/api/staff/devices/overview", factory.Paths);
    }

    [Fact]
    public async Task CustomerIsDeniedWithoutFetchingDeviceData()
    {
        using var factory = new Factory("Customer");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client);
        using var response = await client.GetAsync("/admin/devices");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain(factory.Paths, p => p.StartsWith("/api/staff/devices", StringComparison.Ordinal));
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var page = await client.GetStringAsync("/login");
        var token = WebUtility.HtmlDecode(Regex.Match(page, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value);
        using var response = await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = token, ["email"] = "test@example.test", ["password"] = "correct" }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private sealed class Factory(string role) : WebApplicationFactory<Program>
    {
        private readonly Guid house = Guid.NewGuid(), customer = Guid.NewGuid(), auction = Guid.NewGuid();
        public Guid SessionId { get; } = Guid.NewGuid();
        public ConcurrentBag<string> Paths { get; } = [];
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["Api:BaseUrl"] = "https://backend.test/" }));
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient("BackendAuthentication").ConfigurePrimaryHttpMessageHandler(() => new Handler(_ =>
                    new LoginResponse("test-token", "Bearer", DateTimeOffset.UtcNow.AddMinutes(30),
                        new AuthenticatedUserResponse(Guid.NewGuid(), "test@example.test", [role], []))));
                services.AddHttpClient("BackendApi").ConfigurePrimaryHttpMessageHandler(() => new Handler(request =>
                {
                    string path = request.RequestUri!.AbsolutePath;
                    Paths.Add(path);
                    return path switch
                    {
                        "/api/staff/devices/customers" => new[] { new { customerId = customer, firstName = "Anna", lastName = "Kunde", email = "anna@example.test" } },
                        "/api/staff/devices/auctions" => new[] { new { auctionId = auction, auctionHouseId = house, auctionNumber = 1, name = "Testauktion", startsAt = DateTime.UtcNow, auctionStatus = "Live" } },
                        "/api/staff/devices/available" => new[] { new { deviceId = Guid.NewGuid(), auctionHouseId = house, deviceNumber = 12 } },
                        "/api/staff/devices/sessions" => new[] { new { sessionId = SessionId, deviceId = Guid.NewGuid(), deviceNumber = 13, auctionId = auction, customerId = customer, startedAt = DateTime.UtcNow, endedAt = (DateTime?)null } },
                        _ => Array.Empty<object>()
                    };
                }));
            });
        }
    }
    private sealed class Handler(Func<HttpRequestMessage, object> body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body(request)) });
    }
}
