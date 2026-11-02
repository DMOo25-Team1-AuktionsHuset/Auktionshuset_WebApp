using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Auktionshuset.Api.Endpoints.Device;
using Auktionshuset.Api.Services.DeviceGateway;
using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.Auction;
using Auktionshuset.Application.Devices.AuthenticateDevice;
using Auktionshuset.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Auktionshuset.Api.Test.Security;

public sealed class DeviceWebSocketInProcessTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private const string Secret = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task OrdinaryHttpRequestIsRejected()
    {
        using var server = await Fixture.StartAsync();
        using var response = await server.Host.GetTestClient().GetAsync("/ws/device");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WrongCredentialAndMissingSessionAreRejected()
    {
        using var server = await Fixture.StartAsync();
        using (var socket = await server.ConnectAsync())
        {
            var request = server.Auth("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
            await SendAsync(socket, request);
            var response = await ReceiveTypeAsync(socket, "authResult");
            Assert.False(response["accepted"]!.GetValue<bool>());
            Assert.Equal(request["requestId"]!.ToString(), response["requestId"]!.ToString());
            Assert.Equal("InvalidDeviceCredentialsOrSession", response["errorCode"]!.ToString());
            Assert.Null(response["sessionId"]);
        }
        server.HasSession = false;
        using var other = await server.ConnectAsync();
        await SendAsync(other, server.Auth());
        Assert.False((await ReceiveTypeAsync(other, "authResult"))["accepted"]!.GetValue<bool>());
    }

    [Fact]
    public async Task FragmentedAuthenticationYieldsSessionAndExactState()
    {
        using var server = await Fixture.StartAsync();
        using var socket = await server.ConnectAsync();
        var request = server.Auth();
        var bytes = Encoding.UTF8.GetBytes(request.ToJsonString());
        using var timeout = new CancellationTokenSource(Timeout);
        await socket.SendAsync(bytes.AsMemory(0, 13), WebSocketMessageType.Text, false, timeout.Token);
        await socket.SendAsync(bytes.AsMemory(13), WebSocketMessageType.Text, true, timeout.Token);
        var auth = await ReceiveTypeAsync(socket, "authResult");
        Assert.True(auth["accepted"]!.GetValue<bool>());
        Assert.Equal(request["requestId"]!.ToString(), auth["requestId"]!.ToString());
        Assert.Equal(server.Session.ToString(), auth["sessionId"]!.ToString());
                Assert.Equal("7", auth["sessionRevision"]!.ToString());
        var state = await ReceiveTypeAsync(socket, "state");
        Assert.Equal(server.Customer.ToString(), state["customerId"]!.ToString());
        Assert.Equal(server.Auction.ToString(), state["auctionId"]!.ToString());
        Assert.Equal(server.Lot.ToString(), state["auctionLotId"]!.ToString());
        Assert.Equal("12", state["auctionRevision"]!.ToString());
                Assert.Equal("7", state["sessionRevision"]!.ToString());
        Assert.Equal("123", state["startingPriceOre"]!.ToString());
        Assert.Equal("250", state["highestBidOre"]!.ToString());
        Assert.Equal("201", state["customerBidOre"]!.ToString());
        Assert.Equal("2", state["bidSequence"]!.ToString());
        Assert.False(state.ContainsKey("draftOre"));
    }

    [Theory]
    [InlineData("{invalid")]
    [InlineData("{\"protocolVersion\":2,\"type\":\"authenticate\"}")]
    [InlineData("{\"protocolVersion\":1,\"protocolVersion\":1,\"type\":\"authenticate\"}")]
    public async Task InvalidMessagesProduceProtocolError(string text)
    {
        using var server = await Fixture.StartAsync();
        using var socket = await server.ConnectAsync();
        await SendBytesAsync(socket, Encoding.UTF8.GetBytes(text));
        Assert.Equal("InvalidMessage", (await ReceiveTypeAsync(socket, "error"))["errorCode"]!.ToString());
    }

    [Theory]
    [InlineData(4096, true)]
    [InlineData(4097, false)]
    public async Task MessageSizeLimitCountsUtf8Bytes(int size, bool valid)
    {
        using var server = await Fixture.StartAsync();
        using var socket = await server.ConnectAsync();
        var json = server.Auth().ToJsonString();
        var text = json + new string(' ', size - Encoding.UTF8.GetByteCount(json));
        await SendBytesAsync(socket, Encoding.UTF8.GetBytes(text));
        var response = await ReceiveTypeAsync(socket, valid ? "authResult" : "error");
        if (valid) Assert.True(response["accepted"]!.GetValue<bool>());
        else Assert.Equal("InvalidMessage", response["errorCode"]!.ToString());
    }

    [Fact]
    public async Task BidIsForwardedWithSessionAndRetryPreservesRequestId()
    {
        using var server = await Fixture.StartAsync(connected: true);
        using var socket = await server.ConnectAsync();
        await AuthenticateAsync(server, socket);
        var request = server.Bid();
        await SendAsync(socket, request);
        var first = await ReceiveTypeAsync(socket, "bidReceipt");
        Assert.True(first["accepted"]!.GetValue<bool>());
        Assert.False(first["duplicate"]!.GetValue<bool>());
        Assert.Equal(request["requestId"]!.ToString(), first["requestId"]!.ToString());
                Assert.Equal("13", first["auctionRevision"]!.ToString());
        await SendAsync(socket, request);
        var retry = await ReceiveTypeAsync(socket, "bidReceipt");
        Assert.True(retry["duplicate"]!.GetValue<bool>());
        Assert.Equal(first["bidId"]!.ToString(), retry["bidId"]!.ToString());
        Assert.Equal(2, server.Bids.Commands.Count);
        var command = server.Bids.Commands[0];
        Assert.Equal(server.Session, command.DeviceSessionId);
        Assert.Equal(server.Customer, command.CustomerId);
        Assert.Equal(server.Device, command.DeviceId);
        Assert.Equal(12, command.AuctionRevision);
        Assert.Equal(3.45m, command.Amount);
        Assert.Equal(command.RequestId, server.Bids.Commands[1].RequestId);
    }

    [Fact]
    public async Task StaleSessionRevisionIsRejectedWithoutForwardingBid()
    {
        using var server = await Fixture.StartAsync(connected: true);
        using var socket = await server.ConnectAsync();
        await AuthenticateAsync(server, socket);
        var request = server.Bid();
        request["sessionRevision"] = "6";
        await SendAsync(socket, request);
        Assert.Equal("InvalidMessage", (await ReceiveTypeAsync(socket, "error"))["errorCode"]!.ToString());
        Assert.Empty(server.Bids.Commands);
    }

    [Fact]
    public async Task GatewayLossBlocksBidsAndResynchronizationRestoresThem()
    {
        using var server = await Fixture.StartAsync(connected: true);
        using var socket = await server.ConnectAsync();
        await AuthenticateAsync(server, socket);
        server.SetConnected(false);
        await server.Gateway.SetUnavailableAsync(default);
        Assert.Equal("unavailable", (await ReceiveTypeAsync(socket, "connectionStatus"))["status"]!.ToString());
        await SendAsync(socket, server.Bid());
        var rejected = await ReceiveTypeAsync(socket, "bidReceipt");
        Assert.False(rejected["accepted"]!.GetValue<bool>());
        Assert.Equal("Unavailable", rejected["errorCode"]!.ToString());
        Assert.Empty(server.Bids.Commands);
        server.SetConnected(true);
        await server.Gateway.ResynchronizeAsync(default);
        await ReceiveTypeAsync(socket, "state");
        await ReceiveTypeAsync(socket, "connectionStatus");
        await SendAsync(socket, server.Bid());
        Assert.True((await ReceiveTypeAsync(socket, "bidReceipt"))["accepted"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ChangedSessionIsRejectedAndReleasedSessionIsAnnounced()
    {
        using var server = await Fixture.StartAsync(connected: true);
        using (var socket = await server.ConnectAsync())
        {
            await AuthenticateAsync(server, socket);
            var request = server.Bid();
            request["sessionId"] = Guid.NewGuid().ToString();
            await SendAsync(socket, request);
            Assert.Equal("InvalidMessage", (await ReceiveTypeAsync(socket, "error"))["errorCode"]!.ToString());
            Assert.Empty(server.Bids.Commands);
        }
        // Wait until the first socket's server-side cleanup unregisters it.
        await server.WaitForUnregisterAsync();
        using var second = await server.ConnectAsync();
        await AuthenticateAsync(server, second);
        server.HasSession = false;
        await server.Gateway.AssignmentChangedAsync(Guid.NewGuid(), server.Device, server.Session, default);
        var status = await ReceiveTypeAsync(second, "connectionStatus");
        while (status["status"]!.ToString() is "unavailable" or "synchronizing") status = await ReceiveTypeAsync(second, "connectionStatus");
        Assert.Equal("released", status["status"]!.ToString());
    }

    [Fact]
    public async Task AbruptDisconnectUnregistersAndAllowsReconnect()
    {
        using var server = await Fixture.StartAsync();
        using var first = await server.ConnectAsync();
        await AuthenticateAsync(server, first);
        first.Abort();
        await server.WaitForUnregisterAsync();
        using var second = await server.ConnectAsync();
        await AuthenticateAsync(server, second);
    }

    private static async Task AuthenticateAsync(Fixture fixture, WebSocket socket)
    {
        await SendAsync(socket, fixture.Auth());
        Assert.True((await ReceiveTypeAsync(socket, "authResult"))["accepted"]!.GetValue<bool>());
        await ReceiveTypeAsync(socket, "state");
        await ReceiveTypeAsync(socket, "connectionStatus");
    }

    private static Task SendAsync(WebSocket socket, JsonObject message) => SendBytesAsync(socket, Encoding.UTF8.GetBytes(message.ToJsonString()));
    private static async Task SendBytesAsync(WebSocket socket, byte[] bytes)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        await socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
    }

    private static async Task<JsonObject> ReceiveTypeAsync(WebSocket socket, string expected)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        var buffer = new byte[8192];
        while (true)
        {
            using var stream = new MemoryStream();
            ValueWebSocketReceiveResult part;
            do
            {
                part = await socket.ReceiveAsync(buffer.AsMemory(), timeout.Token);
                Assert.Equal(WebSocketMessageType.Text, part.MessageType);
                stream.Write(buffer, 0, part.Count);
                Assert.True(stream.Length <= 65536);
            } while (!part.EndOfMessage);
            var json = JsonNode.Parse(stream.ToArray())!.AsObject();
            if (json["type"]!.ToString() == expected) return json;
            Assert.Contains(json["type"]!.ToString(), new[] { "connectionStatus", "state", "ping" });
        }
    }

    private sealed class Fixture : IDisposable, IDeviceRepository, IDeviceSecretService, IDeviceCurrentStateReader
    {
        public Guid Device { get; } = Guid.NewGuid();
        public Guid Session { get; } = Guid.NewGuid();
        public Guid Customer { get; } = Guid.NewGuid();
        public Guid Auction { get; } = Guid.NewGuid();
        public Guid Lot { get; } = Guid.NewGuid();
        public bool HasSession { get; set; } = true;
        public DeviceGatewayProcessor Gateway { get; } = new();
        public BidStore Bids { get; } = new();
        public IHost Host { get; private set; } = null!;

        public static async Task<Fixture> StartAsync(bool connected = false)
        {
            var f = new Fixture();
            f.SetConnected(connected);
            f.Host = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton(f.Gateway);
                    services.AddSingleton<IDeviceRepository>(f);
                    services.AddSingleton<IDeviceSecretService>(f);
                    services.AddSingleton<IDeviceCurrentStateReader>(f);
                    services.AddSingleton<IPlaceBidStore>(f.Bids);
                    services.AddSingleton<AuthenticateDeviceHandler>();
                    services.AddSingleton<PlaceBidHandler>();
                    services.AddSingleton<PlaceDeviceBidHandler>();
                })
                .Configure(app =>
                {
                    app.UseWebSockets();
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapDeviceWebSocket());
                })).StartAsync();
            return f;
        }

        public void SetConnected(bool value) => typeof(DeviceGatewayProcessor).GetMethod("SetConnected", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Gateway, [value]);
        public async Task<WebSocket> ConnectAsync()
        {
            using var timeout = new CancellationTokenSource(Timeout);
            return await Host.GetTestServer().CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/ws/device"), timeout.Token);
        }
        public JsonObject Auth(string secret = Secret) => new() { ["protocolVersion"] = 1, ["type"] = "authenticate", ["requestId"] = Guid.NewGuid().ToString(), ["deviceId"] = Device.ToString(), ["credential"] = secret };
        public JsonObject Bid() => new() { ["protocolVersion"] = 1, ["type"] = "placeBid", ["requestId"] = Guid.NewGuid().ToString(), ["deviceId"] = Device.ToString(), ["sessionId"] = Session.ToString(), ["sessionRevision"] = "7", ["auctionId"] = Auction.ToString(), ["auctionLotId"] = Lot.ToString(), ["auctionRevision"] = "12", ["amountOre"] = "345" };
        public async Task WaitForUnregisterAsync()
        {
            using var timeout = new CancellationTokenSource(Timeout);
            var probe = new Probe(Device);
            while (!Gateway.Register(probe)) await Task.Delay(10, timeout.Token);
            Assert.True(Gateway.Unregister(probe));
        }
        public Task AddAsync(Device device, DeviceCredential credential, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DeviceCredential?> GetCredentialAsync(Guid deviceId, CancellationToken cancellationToken) => Task.FromResult(deviceId == Device ? new DeviceCredential { DeviceId = Device, Device = new Device { DeviceId = Device, DeviceNumber = 1, AuctionHouseId = Guid.NewGuid(), Status = "Active" }, SecretHash = Secret, IsEnabled = true, CredentialVersion = 1, SessionRevision = 7 } : null);
        public Task<ActiveDeviceSession?> GetActiveSessionAsync(Guid deviceId, Guid? sessionId, CancellationToken cancellationToken) => Task.FromResult(HasSession && deviceId == Device && (sessionId is null || sessionId == Session) ? new ActiveDeviceSession(Device, Session, Customer, Auction, 7) : null);
        public string GenerateSecret() => Secret;
        public string HashSecret(string secret) => secret;
        public bool VerifySecret(string? secret, string? storedHash) => secret == storedHash;
        public Task<DeviceCurrentState?> ReadAsync(Guid deviceId, Guid sessionId, CancellationToken cancellationToken) => Task.FromResult(HasSession && deviceId == Device && sessionId == Session ? new DeviceCurrentState(Device, Session, Customer, Auction, 12, Lot, true, 1.23m, 2.50m, 2.01m, 2, 7) : null);
        public void Dispose() => Host.Dispose();
    }

    private sealed class BidStore : IPlaceBidStore
    {
        public List<PlaceBidCommand> Commands { get; } = [];
        private readonly Guid bidId = Guid.NewGuid();
        public Task<PlaceBidResult> PlaceBidAsync(PlaceBidCommand command, CancellationToken cancellationToken)
        {
            var duplicate = Commands.Any(previous => previous.RequestId == command.RequestId);
            Commands.Add(command);
            return Task.FromResult(new PlaceBidResult(true, duplicate, bidId, 3, command.Amount, null, 13));
        }
    }

    private sealed class Probe(Guid device) : IConnectedGatewayDevice
    {
        public Guid DeviceId => device;
        public Guid AuctionId => Guid.Empty;
        public Task SetUnavailableAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResynchronizeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AuctionChangedAsync(Guid eventId, Guid auctionId, long auctionRevision, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AssignmentChangedAsync(Guid eventId, Guid deviceId, Guid sessionId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
