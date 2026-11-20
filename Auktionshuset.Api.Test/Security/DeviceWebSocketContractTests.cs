using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Auktionshuset.Api.Test.Security;

// Run against an isolated, provisioned instance; the bid test changes auction data.
[Collection("Device WebSocket contract")]
public sealed class DeviceWebSocketContractTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [DeviceSocketFact]
    public async Task UnknownDeviceIsRejectedWithoutIssuingSession()
    {
        using var socket = await ConnectAsync();
        var request = Authentication(Guid.NewGuid(), "invalid-test-credential");
        await SendAsync(socket, request);
        var response = await ReceiveTypeAsync(socket, "authResult");
        Assert.False(response["accepted"]!.GetValue<bool>());
        Assert.Equal(request["requestId"]!.ToString(), response["requestId"]!.ToString());
        Assert.Equal(request["deviceId"]!.ToString(), response["deviceId"]!.ToString());
        Assert.False(string.IsNullOrWhiteSpace(response["errorCode"]?.ToString()));
        Assert.Null(response["sessionId"]);
        Assert.Null(response["sessionRevision"]);
    }

    [ProvisionedDeviceSocketFact]
    public async Task FragmentedAuthenticationReturnsSessionAndAuthoritativeState()
    {
        using var socket = await ConnectAsync();
        var request = Authentication(DeviceId(), Credential());
        var bytes = Encoding.UTF8.GetBytes(request.ToJsonString());
        using var timeout = new CancellationTokenSource(Timeout);
        var split = bytes.Length / 2;
        await socket.SendAsync(bytes.AsMemory(0, split), WebSocketMessageType.Text, false, timeout.Token);
        await socket.SendAsync(bytes.AsMemory(split), WebSocketMessageType.Text, true, timeout.Token);
        var auth = await ReceiveTypeAsync(socket, "authResult");
        Assert.True(auth["accepted"]!.GetValue<bool>());
        Assert.Equal(request["requestId"]!.ToString(), auth["requestId"]!.ToString());
        Assert.Equal(DeviceId().ToString(), auth["deviceId"]!.ToString());
        Assert.True(Guid.TryParse(auth["sessionId"]?.ToString(), out _));
        AssertDecimalString(auth, "sessionRevision");
        var state = await ReceiveTypeAsync(socket, "state");
        AssertState(state, auth);
    }

    [DeviceSocketFact]
    public async Task MalformedJsonIsRejectedRatherThanAuthenticated()
    {
        using var socket = await ConnectAsync();
        using var timeout = new CancellationTokenSource(Timeout);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{invalid").AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
        var (type, message) = await ReceiveAsync(socket, timeout.Token);
        if (type == WebSocketMessageType.Close) return;
        Assert.Equal(WebSocketMessageType.Text, type);
        Assert.NotNull(message);
        Assert.Equal("error", message["type"]?.ToString());
    }

    [ProvisionedDeviceSocketFact]
    public async Task ReconnectAfterAbruptDisconnectAuthenticatesAndFetchesFreshState()
    {
        using var first = await ConnectAsync();
        var firstAuth = await AuthenticateAsync(first);
        var firstState = await ReceiveTypeAsync(first, "state");
        AssertState(firstState, firstAuth);
        first.Abort();

        using var second = await ConnectAsync();
        var secondAuth = await AuthenticateAsync(second);
        var secondState = await ReceiveTypeAsync(second, "state");
        AssertState(secondState, secondAuth);
        Assert.Equal(firstAuth["sessionId"]!.ToString(), secondAuth["sessionId"]!.ToString());
        Assert.Equal(firstState["customerId"]!.ToString(), secondState["customerId"]!.ToString());
        Assert.Equal(firstState["auctionId"]!.ToString(), secondState["auctionId"]!.ToString());
    }

    [DeviceSocketBidFact]
    public async Task BidRetryReturnsSameReceiptAndRejectsChangedPayload()
    {
        using var socket = await ConnectAsync();
        var auth = await AuthenticateAsync(socket);
        var state = await ReceiveTypeAsync(socket, "state");
        AssertState(state, auth);
        Assert.True(state["openForBids"]!.GetValue<bool>());
        Assert.NotNull(state["auctionLotId"]);
        var amount = Environment.GetEnvironmentVariable("DEVICE_WS_BID_AMOUNT_ORE")!;
        Assert.True(ulong.TryParse(amount, out var numericAmount) && numericAmount < ulong.MaxValue);
        var request = new JsonObject
        {
            ["protocolVersion"] = 1, ["type"] = "placeBid", ["requestId"] = Guid.NewGuid().ToString(),
            ["deviceId"] = DeviceId().ToString(), ["sessionId"] = auth["sessionId"]!.ToString(),
            ["sessionRevision"] = auth["sessionRevision"]!.ToString(), ["auctionId"] = state["auctionId"]!.ToString(),
            ["auctionLotId"] = state["auctionLotId"]!.ToString(), ["auctionRevision"] = state["auctionRevision"]!.ToString(),
            ["amountOre"] = amount
        };
        await SendAsync(socket, request);
        var original = await ReceiveTypeAsync(socket, "bidReceipt");
        Assert.Equal(request["requestId"]!.ToString(), original["requestId"]!.ToString());
        Assert.True(original["accepted"]!.GetValue<bool>());
        Assert.False(original["duplicate"]!.GetValue<bool>());
        Assert.True(Guid.TryParse(original["bidId"]?.ToString(), out _));
        await SendAsync(socket, request);
        var retry = await ReceiveTypeAsync(socket, "bidReceipt");
        Assert.True(retry["accepted"]!.GetValue<bool>());
        Assert.True(retry["duplicate"]!.GetValue<bool>());
        Assert.Equal(original["bidId"]!.ToString(), retry["bidId"]!.ToString());
        Assert.Equal(original["bidSequence"]!.ToString(), retry["bidSequence"]!.ToString());
        request["amountOre"] = (numericAmount + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await SendAsync(socket, request);
        var changed = await ReceiveTypeAsync(socket, "bidReceipt");
        Assert.False(changed["accepted"]!.GetValue<bool>());
        Assert.False(string.IsNullOrWhiteSpace(changed["errorCode"]?.ToString()));
    }

    private static async Task<ClientWebSocket> ConnectAsync()
    {
        var socket = new ClientWebSocket();
        try
        {
            var uri = new Uri(Environment.GetEnvironmentVariable("DEVICE_WS_URL")!);
            Assert.True((uri.Scheme == "ws" || uri.Scheme == "wss") && uri.AbsolutePath == "/ws/device" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment));
            using var timeout = new CancellationTokenSource(Timeout);
            await socket.ConnectAsync(uri, timeout.Token);
            return socket;
        }
        catch { socket.Dispose(); throw; }
    }

    private static JsonObject Authentication(Guid device, string credential) => new()
    {
        ["protocolVersion"] = 1, ["type"] = "authenticate", ["requestId"] = Guid.NewGuid().ToString(),
        ["deviceId"] = device.ToString(), ["credential"] = credential
    };

    private static async Task<JsonObject> AuthenticateAsync(ClientWebSocket socket)
    {
        var request = Authentication(DeviceId(), Credential());
        await SendAsync(socket, request);
        var result = await ReceiveTypeAsync(socket, "authResult");
        Assert.True(result["accepted"]!.GetValue<bool>());
        Assert.Equal(request["requestId"]!.ToString(), result["requestId"]!.ToString());
        Assert.True(Guid.TryParse(result["sessionId"]?.ToString(), out _));
        AssertDecimalString(result, "sessionRevision");
        return result;
    }

    private static async Task SendAsync(ClientWebSocket socket, JsonObject message)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        await socket.SendAsync(Encoding.UTF8.GetBytes(message.ToJsonString()).AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
    }

    private static async Task<JsonObject> ReceiveTypeAsync(ClientWebSocket socket, string expected)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (true)
        {
            var (type, message) = await ReceiveAsync(socket, timeout.Token);
            Assert.Equal(WebSocketMessageType.Text, type);
            Assert.NotNull(message);
            if (message["type"]?.ToString() == expected) return message;
            Assert.Contains(message["type"]?.ToString(), new[] { "connectionStatus", "update", "state" });
        }
    }

    private static async Task<(WebSocketMessageType Type, JsonObject? Message)> ReceiveAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        using var data = new MemoryStream();
        var buffer = new byte[4096];
        ValueWebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return (result.MessageType, null);
            Assert.True(data.Length + result.Count <= 65536, "Server message exceeds test limit.");
            data.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return (result.MessageType, JsonNode.Parse(data.ToArray())?.AsObject());
    }

    private static void AssertState(JsonObject state, JsonObject auth)
    {
        Assert.Equal(1, state["protocolVersion"]!.GetValue<int>());
        Assert.Equal(DeviceId().ToString(), state["deviceId"]!.ToString());
        Assert.Equal(auth["sessionId"]!.ToString(), state["sessionId"]!.ToString());
        Assert.Equal(auth["sessionRevision"]!.ToString(), state["sessionRevision"]!.ToString());
        Assert.True(Guid.TryParse(state["customerId"]?.ToString(), out _));
        Assert.True(Guid.TryParse(state["auctionId"]?.ToString(), out _));
        AssertDecimalString(state, "auctionRevision");
        Assert.False(state.ContainsKey("draftOre"));
        foreach (var field in new[] { "startingPriceOre", "highestBidOre", "customerBidOre", "bidSequence" })
        {
            Assert.True(state.ContainsKey(field));
            if (state[field] is not null) AssertDecimalString(state, field);
            if (state["auctionLotId"] is null) Assert.Null(state[field]);
        }
    }

    private static void AssertDecimalString(JsonObject value, string field)
    {
        Assert.True(value[field] is JsonValue node && node.TryGetValue<string>(out var text) &&
                    text.Length > 0 && text.All(c => c is >= '0' and <= '9'), $"{field} must be a decimal digit string.");
    }

    private static Guid DeviceId() => Guid.Parse(Environment.GetEnvironmentVariable("DEVICE_WS_DEVICE_ID")!);
    private static string Credential() => Environment.GetEnvironmentVariable("DEVICE_WS_CREDENTIAL")!;
}

public class DeviceSocketFactAttribute : FactAttribute
{
    public DeviceSocketFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVICE_WS_URL")))
            Skip = "Set DEVICE_WS_URL to an isolated running ws(s)://.../ws/device endpoint.";
    }
}

public class ProvisionedDeviceSocketFactAttribute : DeviceSocketFactAttribute
{
    public ProvisionedDeviceSocketFactAttribute()
    {
        if (Skip is null && (!Guid.TryParse(Environment.GetEnvironmentVariable("DEVICE_WS_DEVICE_ID"), out _) ||
                             string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVICE_WS_CREDENTIAL"))))
            Skip = "Set DEVICE_WS_DEVICE_ID and DEVICE_WS_CREDENTIAL for a provisioned isolated device.";
    }
}

public sealed class DeviceSocketBidFactAttribute : ProvisionedDeviceSocketFactAttribute
{
    public DeviceSocketBidFactAttribute()
    {
        if (Skip is null && (Environment.GetEnvironmentVariable("DEVICE_WS_ALLOW_BID") != "1" ||
                             string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVICE_WS_BID_AMOUNT_ORE"))))
            Skip = "Set DEVICE_WS_ALLOW_BID=1 and DEVICE_WS_BID_AMOUNT_ORE to permit a real bid on an isolated auction.";
    }
}
