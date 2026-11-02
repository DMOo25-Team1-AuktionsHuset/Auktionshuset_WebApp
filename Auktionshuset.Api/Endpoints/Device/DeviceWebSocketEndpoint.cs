using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Auktionshuset.Api.Services.DeviceGateway;
using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.Admin.Devices.AuthenticateDevice;
using Auktionshuset.Application.Devices.AuthenticateDevice;

namespace Auktionshuset.Api.Endpoints.Device;

public static class DeviceWebSocketEndpoint
{
    public static IEndpointRouteBuilder MapDeviceWebSocket(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/ws/device", HandleAsync).AllowAnonymous();
        return endpoints;
    }

    private static async Task HandleAsync(HttpContext context, DeviceGatewayProcessor gateway,
        IDeviceCurrentStateReader reader, AuthenticateDeviceHandler authenticate, PlaceDeviceBidHandler bids)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        var connection = new Transport(socket, gateway, lifetime);
        var sender = connection.SendLoopAsync();
        DeviceSnapshotConnection? snapshots = null;
        try
        {
            using var authTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            authTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var authMessage = await ReadAsync(socket, authTimeout.Token);
            var auth = authMessage.RootElement;
            ValidateEnvelope(auth, "authenticate", "protocolVersion", "type", "requestId", "deviceId", "credential");
            var requestId = Id(auth, "requestId");
            var deviceId = Id(auth, "deviceId");
            var secret = Text(auth, "credential");
            if (secret.Length != 64) throw new ProtocolException();
            var result = await authenticate.HandleAsync(new AuthenticateDeviceCommand { DeviceId = deviceId, Secret = secret }, authTimeout.Token);
            if (result?.Session is null)
            {
                connection.Enqueue(new { protocolVersion = 1, type = "authResult", requestId, deviceId, accepted = false,
                    errorCode = "InvalidDeviceCredentialsOrSession", sessionId = (Guid?)null, sessionRevision = (string?)null });
                await connection.CompleteAsync(sender);
                return;
            }

            var session = result.Session;
            connection.Initialize(deviceId, session.SessionId, session.SessionRevision);
            connection.Enqueue(new { protocolVersion = 1, type = "authResult", requestId, deviceId, accepted = true,
                errorCode = (string?)null, sessionId = session.SessionId, sessionRevision = Number(session.SessionRevision) });
            snapshots = new DeviceSnapshotConnection(deviceId, session.SessionId, gateway, reader, connection);
            connection.Bind(snapshots);
            if (!await snapshots.RegisterAsync(lifetime.Token)) return;
            using var heartbeatLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            var heartbeat = connection.HeartbeatAsync(snapshots, heartbeatLifetime.Token);
            try
            {
                while (!lifetime.IsCancellationRequested)
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(60));
                    using var message = await ReadAsync(socket, timeout.Token);
                    var root = message.RootElement;
                    var type = Text(root, "type");
                    if (type is "ping" or "pong")
                    {
                        ValidateEnvelope(root, type, "protocolVersion", "type", "deviceId", "sessionId");
                        if (Id(root, "deviceId") != deviceId || Id(root, "sessionId") != session.SessionId) throw new ProtocolException();
                        if (type == "ping") connection.Enqueue(new { protocolVersion = 1, type = "pong", deviceId, sessionId = session.SessionId });
                        continue;
                    }
                    ValidateEnvelope(root, "placeBid", "protocolVersion", "type", "requestId", "deviceId", "sessionId",
                        "sessionRevision", "auctionId", "auctionLotId", "auctionRevision", "amountOre");
                    var bidRequest = Id(root, "requestId");
                    var bidDevice = Id(root, "deviceId");
                    var bidSession = Id(root, "sessionId");
                    var auctionId = Id(root, "auctionId");
                    var lotId = Id(root, "auctionLotId");
                    var revision = Digits(root, "auctionRevision", long.MaxValue);
                    var ore = Digits(root, "amountOre", long.MaxValue);
                    var sessionRevision = Digits(root, "sessionRevision", long.MaxValue);
                    if (sessionRevision != (ulong)session.SessionRevision ||
                        bidDevice != deviceId || bidSession != session.SessionId) throw new ProtocolException();
                    DeviceBidReceipt? receipt = null;
                    if (connection.CanBid)
                        receipt = await bids.HandleAsync(new PlaceDeviceBidCommand(deviceId, secret, session.SessionId,
                            auctionId, lotId, (long)revision, bidRequest, ore / 100m, (long)sessionRevision), lifetime.Token);
                    connection.Enqueue(new { protocolVersion = 1, type = "bidReceipt", requestId = bidRequest, deviceId,
                        sessionId = session.SessionId, sessionRevision = Number(session.SessionRevision), auctionId, auctionLotId = lotId,
                        accepted = receipt?.Accepted ?? false, duplicate = receipt?.Duplicate ?? false, bidId = receipt?.BidId,
                        bidSequence = receipt?.SequenceNumber is int sequence ? Number(sequence) : null,
                        auctionRevision = receipt?.AuctionRevision is long receiptRevision ? Number(receiptRevision) : null,
                        errorCode = receipt?.ErrorCode ?? (receipt is null ? "Unavailable" : null) });
                }
            }
            finally
            {
                heartbeatLifetime.Cancel();
                try { await heartbeat; } catch (OperationCanceledException) { }
            }
        }
        catch (Exception ex) when (ex is ProtocolException or JsonException)
        {
            if (!lifetime.IsCancellationRequested)
            {
                connection.Enqueue(new { protocolVersion = 1, type = "error", errorCode = "InvalidMessage" });
                try { await connection.CompleteAsync(sender); }
                catch (Exception sendError) when (sendError is WebSocketException or OperationCanceledException) { }
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            // Timeouts and disconnects terminate only this device.
        }
        finally
        {
            snapshots?.Unregister();
            lifetime.Cancel();
            socket.Abort();
            try { await sender; } catch (OperationCanceledException) { } catch (WebSocketException) { }
        }
    }

    private static async Task<JsonDocument> ReadAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[4097];
        var count = 0;
        var started = false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer.AsMemory(count), deadline.Token);
            if (result.MessageType != WebSocketMessageType.Text) throw new ProtocolException();
            if (!started) { started = true; deadline.CancelAfter(TimeSpan.FromSeconds(15)); }
            count += result.Count;
            if (count > 4096) throw new ProtocolException();
            if (result.EndOfMessage) return JsonDocument.Parse(buffer.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 4 });
        }
    }

    private static void ValidateEnvelope(JsonElement root, string type, params string[] allowed)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new ProtocolException();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
            if (!allowed.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name)) throw new ProtocolException();
        if (!root.TryGetProperty("protocolVersion", out var version) || version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out var number) || number != 1 || Text(root, "type") != type) throw new ProtocolException();
    }

    private static string Text(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()! : throw new ProtocolException();

    private static Guid Id(JsonElement root, string name)
    {
        var text = Text(root, name);
        if (!Guid.TryParseExact(text, "D", out var id) || id == Guid.Empty || id.ToString("D") != text) throw new ProtocolException();
        return id;
    }

    private static ulong Digits(JsonElement root, string name, ulong max)
    {
        var value = Text(root, name);
        if (value.Length == 0 || value.Length > 20 || value.Any(c => c < '0' || c > '9') ||
            !ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed > max)
            throw new ProtocolException();
        return parsed;
    }

    private static string Number(long value) => value >= 0 ? value.ToString(CultureInfo.InvariantCulture) : throw new InvalidOperationException("Negative revision or sequence");

    private sealed class ProtocolException : Exception;

    private sealed class Transport(WebSocket socket, DeviceGatewayProcessor gateway, CancellationTokenSource lifetime) : IDeviceSnapshotSink
    {
        private readonly Channel<byte[]> outgoing = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(32)
        {
            SingleReader = true, FullMode = BoundedChannelFullMode.Wait
        });
        private readonly object sync = new();
        private Guid deviceId;
        private Guid sessionId;
        private long sessionRevision;
        private long snapshotStamp;
        private DeviceSnapshotConnection? snapshots;
        private bool hasSnapshot;
        private bool released;
        private bool announced;

        public void Initialize(Guid device, Guid session, long revision) { deviceId = device; sessionId = session; sessionRevision = revision; }
        public void Bind(DeviceSnapshotConnection connection) => snapshots = connection;
        private bool SnapshotIsCurrent => hasSnapshot && (snapshotStamp & 1) != 0 && snapshotStamp == gateway.ConnectionStamp;
        public bool CanBid { get { lock (sync) return announced && SnapshotIsCurrent && snapshots is not null && gateway.IsDeviceReady(snapshots) && !released && !lifetime.IsCancellationRequested; } }

        public void Enqueue(object message)
        {
            if (!outgoing.Writer.TryWrite(JsonSerializer.SerializeToUtf8Bytes(message)))
            {
                lifetime.Cancel();
                socket.Abort();
            }
        }

        public async Task SendLoopAsync()
        {
            try
            {
                await foreach (var message in outgoing.Reader.ReadAllAsync(lifetime.Token))
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    await socket.SendAsync(message.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
                }
                lifetime.Cancel();
            }
            catch { lifetime.Cancel(); socket.Abort(); throw; }
        }

        public async Task CompleteAsync(Task sender)
        {
            outgoing.Writer.TryComplete();
            await sender;
        }

        public async Task HeartbeatAsync(DeviceSnapshotConnection snapshots, CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                bool refresh;
                lock (sync)
                {
                    var available = SnapshotIsCurrent && !released;
                    if (!available && announced) { announced = false; Status("unavailable"); }
                    refresh = !available && !released && gateway.IsConnected;
                    Enqueue(new { protocolVersion = 1, type = "ping", deviceId, sessionId });
                }
                if (refresh) await gateway.ResynchronizeDeviceAsync(snapshots, cancellationToken);
            }
        }

        public void Invalidate()
        {
            lock (sync)
            {
                if (announced) { announced = false; Status(gateway.IsConnected ? "synchronizing" : "unavailable"); }
            }
        }

        public void Abort() { lifetime.Cancel(); socket.Abort(); }

        public Task NotifyReadyAsync(CancellationToken cancellationToken)
        {
            lock (sync)
            {
                if (!released && !announced && SnapshotIsCurrent && snapshots is not null && gateway.IsDeviceReady(snapshots))
                {
                    announced = true;
                    Status("synchronized");
                }
            }
            return Task.CompletedTask;
        }

        public Task SetUnavailableAsync(CancellationToken cancellationToken)
        {
            lock (sync) { hasSnapshot = false; announced = false; Status("unavailable"); }
            return Task.CompletedTask;
        }

        public Task ApplySnapshotAsync(DeviceCurrentState state, long connectionStamp, CancellationToken cancellationToken)
        {
            lock (sync)
            {
                if (state.SessionRevision != sessionRevision)
                {
                    released = true;
                    hasSnapshot = false;
                    Status("released");
                    outgoing.Writer.TryComplete();
                    return Task.CompletedTask;
                }
                Enqueue(new { protocolVersion = 1, type = "state", deviceId, sessionId, sessionRevision = Number(state.SessionRevision),
                    customerId = state.CustomerId, auctionId = state.AuctionId,
                    auctionRevision = Number(state.AuctionRevision), auctionLotId = state.AuctionLotId,
                    openForBids = state.OpenForBids, startingPriceOre = Money(state.StartingPrice),
                    highestBidOre = Money(state.HighestBid), customerBidOre = Money(state.CustomerBid),
                    bidSequence = state.BidSequence is null ? null : Number(state.BidSequence.Value) });
                hasSnapshot = true;
                snapshotStamp = connectionStamp;
                announced = false;
                if (!SnapshotIsCurrent) Status("unavailable");
            }
            return Task.CompletedTask;
        }

        public Task SetReleasedAsync(CancellationToken cancellationToken)
        {
            lock (sync) { released = true; hasSnapshot = false; Status("released"); }
            outgoing.Writer.TryComplete();
            return Task.CompletedTask;
        }

        private void Status(string status) => Enqueue(new { protocolVersion = 1, type = "connectionStatus", deviceId,
            sessionId, sessionRevision = Number(sessionRevision), status, reason = (string?)null });
        private static string? Money(decimal? value)
        {
            if (value is null) return null;
            var ore = value.Value * 100m;
            if (ore < 0 || ore != decimal.Truncate(ore)) throw new InvalidOperationException("Invalid money precision");
            return ore.ToString("0", CultureInfo.InvariantCulture);
        }
    }
}
