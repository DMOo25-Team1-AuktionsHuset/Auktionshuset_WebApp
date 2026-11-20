using System.Collections.Concurrent;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Api.Security;
using Auktionshuset.Api.Services.DeviceGateway;
using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.Devices.AuthenticateDevice;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Auktionshuset.Api.Test.Security;

public class DeviceGatewayClientConnectionTests
{
    [Fact]
    public async Task RealHubKeepsHealthyDevicesConnectedAfterDeviceFailure()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:SigningKey"] = "user-signing-key-at-least-32-bytes-long",
            ["Authentication:Issuer"] = "user-issuer",
            ["Authentication:Audience"] = "user-audience",
            ["DeviceGateway:Authentication:SigningKey"] = "gateway-signing-key-at-least-32-bytes-long",
            ["DeviceGateway:Authentication:Issuer"] = "gateway-issuer",
            ["DeviceGateway:Authentication:Audience"] = "gateway-audience",
            ["DeviceGateway:AllowInsecureLoopback"] = "true"
        });
        builder.Services.AddSignalR();
        builder.Services.AddSecurityServices(builder.Configuration);
        // Hub methods are not called in this transport test.
        builder.Services.AddSingleton(new AuthenticateDeviceHandler(null!, null!));
        builder.Services.AddSingleton(new PlaceDeviceBidHandler(null!, null!));
        await using var app = builder.Build();
        var tokenIds = new ConcurrentBag<string>();
        app.UseAuthentication();
        app.Use(async (context, next) =>
        {
            var authentication = await context.AuthenticateAsync(DeviceGatewayAuthentication.Scheme);
            var id = authentication.Principal?.FindFirst("jti")?.Value;
            if (id is not null) tokenIds.Add(id);
            await next();
        });
        app.UseAuthorization();
        app.MapHub<DeviceGatewayHub>(DeviceGatewayAuthentication.HubPath, options => options.CloseOnAuthenticationExpiration = true)
            .RequireAuthorization(SecurityPolicies.DeviceGateway);
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        builder.Configuration["DeviceGateway:HubUrl"] = address + DeviceGatewayAuthentication.HubPath;

        var processor = new DeviceGatewayProcessor();
        var device = new TestDevice();
        processor.Register(device);
        var healthy = new TestDevice();
        processor.Register(healthy);
        using var service = new DeviceGatewayHubService(builder.Configuration, app.Environment, processor, NullLogger<DeviceGatewayHubService>.Instance);
        try
        {
            await service.StartAsync(default);
            await WaitUntilAsync(() => processor.IsConnected && device.Resyncs >= 1);
            var hub = app.Services.GetRequiredService<IHubContext<DeviceGatewayHub, IDeviceGatewayClient>>();
            await hub.Clients.All.AuctionChanged(Guid.NewGuid(), device.AuctionId, 1);
            await WaitUntilAsync(() => device.Failures == 1);
            await WaitUntilAsync(() => device.Aborted);
            Assert.True(processor.IsConnected);
            await hub.Clients.All.AuctionChanged(Guid.NewGuid(), healthy.AuctionId, 2);
            await healthy.Received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(1, device.Resyncs);
            Assert.Single(tokenIds.Distinct());
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
            await app.StopAsync();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!condition()) await Task.Delay(25, timeout.Token);
    }

    private sealed class TestDevice : IConnectedGatewayDevice
    {
        private int resyncs, failures, unavailable, aborted;
        public Guid DeviceId { get; } = Guid.NewGuid();
        public Guid AuctionId { get; } = Guid.NewGuid();
        public int Resyncs => Volatile.Read(ref resyncs);
        public int Failures => Volatile.Read(ref failures);
        public int Unavailable => Volatile.Read(ref unavailable);
        public bool Aborted => Volatile.Read(ref aborted) != 0;
        public void Abort() => Interlocked.Exchange(ref aborted, 1);
        public TaskCompletionSource Received { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task SetUnavailableAsync(CancellationToken cancellationToken) { Interlocked.Increment(ref unavailable); return Task.CompletedTask; }
        public Task ResynchronizeAsync(CancellationToken cancellationToken) { Interlocked.Increment(ref resyncs); return Task.CompletedTask; }
        public Task AuctionChangedAsync(Guid eventId, Guid auctionId, long revision, CancellationToken cancellationToken)
        {
            if (revision == 1) { Interlocked.Increment(ref failures); throw new InvalidOperationException("Test failure"); }
            Received.TrySetResult();
            return Task.CompletedTask;
        }
        public Task AssignmentChangedAsync(Guid eventId, Guid deviceId, Guid sessionId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
