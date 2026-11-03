using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Auktionshuset.Api.Endpoints.Device;
using Auktionshuset.Api.Security;
using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.Devices.AuthenticateDevice;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Auktionshuset.Api.Test.Devices;

public sealed class DeviceAuthenticationEndpointTests
{
    private static readonly Guid DeviceId = Guid.NewGuid();
    private static readonly string Secret = new string('A', 64);

    [Fact]
    public async Task CorrectCredentials_ReturnToken()
    {
        using IHost host = await StartServerAsync();
        using HttpClient client = CreateClient(host);

        using HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/api/device/authenticate",
                new
                {
                    deviceId = DeviceId,
                    secret = Secret
                });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);

        using JsonDocument body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.False(string.IsNullOrWhiteSpace(
            body.RootElement
                .GetProperty("accessToken")
                .GetString()));

        Assert.Equal(
            "Bearer",
            body.RootElement
                .GetProperty("tokenType")
                .GetString());

        Assert.False(
            body.RootElement.TryGetProperty("secret", out _));
    }

    [Fact]
    public async Task IncorrectCredentials_ReturnUnauthorized()
    {
        using IHost host = await StartServerAsync();
        using HttpClient client = CreateClient(host);

        using HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/api/device/authenticate",
                new
                {
                    deviceId = DeviceId,
                    secret = new string('B', 64)
                });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        Assert.True(response.Headers.CacheControl?.NoStore == true);
    }

    [Fact]
    public async Task TooManyAttempts_ReturnTooManyRequests()
    {
        using IHost host = await StartServerAsync();
        using HttpClient client = CreateClient(host);

        for (int attempt = 1; attempt <= 6; attempt++)
        {
            using HttpResponseMessage response =
                await client.PostAsJsonAsync(
                    "/api/device/authenticate",
                    new
                    {
                        deviceId = DeviceId,
                        secret = new string('B', 64)
                    });

            HttpStatusCode expected = attempt <= 5
                ? HttpStatusCode.Unauthorized
                : HttpStatusCode.TooManyRequests;

            Assert.Equal(expected, response.StatusCode);
        }
    }

    private static HttpClient CreateClient(IHost host)
    {
        HttpClient client = host.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");

        return client;
    }

    private static async Task<IHost> StartServerAsync()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Authentication:Issuer"] =
                        "Auktionshuset.Api.Test",

                    ["Authentication:Audience"] =
                        "Auktionshuset.Web.Test",

                    ["Authentication:SigningKey"] =
                        "public-employee-test-key-at-least-32-characters",

                    ["DeviceAuthentication:SigningKey"] =
                        "public-device-test-key-at-least-32-characters"
                })
            .Build();

        var secretService = new DeviceSecretService();

        var device = new Device
        {
            DeviceId = DeviceId,
            AuctionHouseId = Guid.NewGuid(),
            DeviceNumber = 1,
            Status = "Available"
        };

        var credential = new DeviceCredential
        {
            DeviceId = DeviceId,
            Device = device,
            SecretHash = secretService.HashSecret(Secret),
            IsEnabled = true,
            CredentialVersion = 1
        };

        return await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();

                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddSingleton(configuration);

                    services.AddSecurityServices(configuration);

                    services.AddSingleton<IDeviceSecretService>(
                        secretService);

                    services.AddSingleton<IDeviceRepository>(
                        new TestDeviceRepository(credential));

                    services.AddScoped<AuthenticateDeviceHandler>();
                    services.AddSingleton<DeviceJwtTokenService>();
                });

                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseAuthorization();

                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapDeviceAuthentication();
                    });
                });
            })
            .StartAsync();
    }

    private sealed class TestDeviceRepository(
        DeviceCredential credential) : IDeviceRepository
    {
        public Task AddAsync(
            Device device,
            DeviceCredential newCredential,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DeviceCredential?> GetCredentialAsync(
            Guid deviceId,
            CancellationToken cancellationToken)
        {
            DeviceCredential? result =
                deviceId == credential.DeviceId
                    ? credential
                    : null;

            return Task.FromResult(result);
        }
    }
}
