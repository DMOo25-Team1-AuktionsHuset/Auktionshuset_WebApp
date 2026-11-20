using System.Net;
using System.Text;
using Auktionshuset.Api.Events.Admin;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Api.Security;
using Auktionshuset.Api.Services;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.Devices.AuthenticateDevice;
using Auktionshuset.Application.EventHandling;
using Microsoft.AspNetCore.Builder;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Auktionshuset.Api.Test.Security;

public class DeviceGatewayHubConnectionTests
{
    private const string UserKey = "test-user-signing-key-at-least-32-bytes";
    private const string GatewayKey = "test-gateway-signing-key-at-least-32-bytes";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ActualHubConnectionRejectsAnonymousAndValidStaffButAcceptsGateway()
    {
        using var host = await StartServerAsync();
        var configured = host.Services.GetRequiredService<ConfiguredAuthUserStore>();
        var staff = await configured.FindByEmailAsync("admin@example.test", CancellationToken.None);
        Assert.NotNull(staff);
        var staffToken = host.Services.GetRequiredService<IAccessTokenService>().Issue(staff).Value;

        await using var staffHub = Connect(host, "/hubs/auction", staffToken);
        await staffHub.StartAsync().WaitAsync(Timeout);
        Assert.Equal(HubConnectionState.Connected, staffHub.State);

        await using var anonymous = Connect(host, DeviceGatewayAuthentication.HubPath, null);
        var anonymousError = await Assert.ThrowsAsync<HttpRequestException>(() => anonymous.StartAsync().WaitAsync(Timeout));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousError.StatusCode);

        await using var staffGateway = Connect(host, DeviceGatewayAuthentication.HubPath, staffToken);
        var staffError = await Assert.ThrowsAsync<HttpRequestException>(() => staffGateway.StartAsync().WaitAsync(Timeout));
        Assert.Equal(HttpStatusCode.Unauthorized, staffError.StatusCode);

        await using var gateway = Connect(host, DeviceGatewayAuthentication.HubPath, GatewayToken());
        await gateway.StartAsync().WaitAsync(Timeout);
        Assert.Equal(HubConnectionState.Connected, gateway.State);
    }

    [Fact]
    public async Task RegisteredInvalidationsReachGatewayAndContinueAfterReconnect()
    {
        using var host = await StartServerAsync();
        await using var connection = Connect(host, DeviceGatewayAuthentication.HubPath, GatewayToken());
        var auctions = System.Threading.Channels.Channel.CreateUnbounded<(Guid EventId, Guid AuctionId, long Revision)>();
        var assignments = System.Threading.Channels.Channel.CreateUnbounded<(Guid EventId, Guid DeviceId, Guid SessionId)>();
        connection.On<Guid, Guid, long>(nameof(IDeviceGatewayClient.AuctionChanged),
            (eventId, auctionId, revision) => auctions.Writer.TryWrite((eventId, auctionId, revision)));
        connection.On<Guid, Guid, Guid>(nameof(IDeviceGatewayClient.AssignmentChanged),
            (eventId, deviceId, sessionId) => assignments.Writer.TryWrite((eventId, deviceId, sessionId)));

        await connection.StartAsync().WaitAsync(Timeout);
        var originalConnectionId = connection.ConnectionId;
        Guid auctionId = Guid.NewGuid(), deviceId = Guid.NewGuid(), sessionId = Guid.NewGuid();
        var auction = new AuctionRevisionChangedIntegrationEvent(Guid.NewGuid(), auctionId, Guid.NewGuid(), 42, DateTime.UtcNow);
        var issued = new DeviceIssuedIntegrationEvent(Guid.NewGuid(), deviceId, sessionId, auctionId, Guid.NewGuid(), DateTime.UtcNow);
        await DispatchAsync(host, auction);
        await DispatchAsync(host, issued);
        Assert.Equal((auction.EventId, auctionId, 42L), await auctions.Reader.ReadAsync().AsTask().WaitAsync(Timeout));
        Assert.Equal((issued.EventId, deviceId, sessionId), await assignments.Reader.ReadAsync().AsTask().WaitAsync(Timeout));

        await connection.StopAsync().WaitAsync(Timeout);
        await connection.StartAsync().WaitAsync(Timeout);
        Assert.Equal(HubConnectionState.Connected, connection.State);
        Assert.NotEqual(originalConnectionId, connection.ConnectionId);
        var released = new DeviceReleasedIntegrationEvent(Guid.NewGuid(), deviceId, sessionId, auctionId, Guid.NewGuid(), DateTime.UtcNow);
        var nextAuction = new AuctionRevisionChangedIntegrationEvent(Guid.NewGuid(), auctionId, null, 43, DateTime.UtcNow);
        await DispatchAsync(host, released);
        await DispatchAsync(host, nextAuction);
        Assert.Equal((released.EventId, deviceId, sessionId), await assignments.Reader.ReadAsync().AsTask().WaitAsync(Timeout));
        Assert.Equal((nextAuction.EventId, auctionId, 43L), await auctions.Reader.ReadAsync().AsTask().WaitAsync(Timeout));
    }

    private static async Task DispatchAsync<T>(IHost host, T message) where T : IIntegrationEvent
    {
        using var scope = host.Services.CreateScope();
        var handler = Assert.Single(scope.ServiceProvider.GetServices<IIntegrationEventHandler<T>>(),
            handler => handler is DeviceGatewayInvalidationHandler);
        await handler.HandleAsync(message, CancellationToken.None);
    }

    private static HubConnection Connect(IHost host, string path, string? token) =>
        new HubConnectionBuilder().WithUrl("http://localhost" + path, options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.HttpMessageHandlerFactory = _ => host.GetTestServer().CreateHandler();
            options.AccessTokenProvider = () => Task.FromResult(token);
        }).Build();

    private static string GatewayToken()
    {
        var token = new JwtSecurityToken("gateway-issuer", "gateway-audience",
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("scope", DeviceGatewayAuthentication.Scope)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GatewayKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<IHost> StartServerAsync()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:SigningKey"] = UserKey,
            ["Authentication:Issuer"] = "user-issuer",
            ["Authentication:Audience"] = "user-audience",
            ["Authentication:Users:0:Email"] = "admin@example.test",
            ["Authentication:Users:0:Password"] = "test-password",
            ["Authentication:Users:0:Role"] = "Admin",
            ["DeviceGateway:Authentication:SigningKey"] = GatewayKey,
            ["DeviceGateway:Authentication:Issuer"] = "gateway-issuer",
            ["DeviceGateway:Authentication:Audience"] = "gateway-audience"
        }).Build();
        return await new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(configuration);
                services.AddRouting();
                services.AddSignalR();
                services.AddSecurityServices(configuration);
                services.AddScoped<IAuthUserStore>(sp => sp.GetRequiredService<ConfiguredAuthUserStore>());
                services.AddApiServices();
                // The hub methods are not invoked here; the real handlers need database services.
                services.AddScoped(_ => new AuthenticateDeviceHandler(null!, null!));
                services.AddScoped(_ => new PlaceDeviceBidHandler(null!, null!));
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapHub<DeviceGatewayHub>(DeviceGatewayAuthentication.HubPath, options => options.CloseOnAuthenticationExpiration = true)
                        .RequireAuthorization(SecurityPolicies.DeviceGateway);
                    endpoints.MapHub<AuctionHub>("/hubs/auction").RequireAuthorization(SecurityPolicies.CanViewAuctions);
                });
            })).StartAsync();
    }
}
