using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.Admin.Devices.AuthenticateDevice;
using Auktionshuset.Application.Devices.AuthenticateDevice;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Hubs;

public interface IDeviceGatewayClient
{
    Task AuctionChanged(Guid eventId, Guid auctionId, long auctionRevision);
    Task AssignmentChanged(Guid eventId, Guid deviceId, Guid sessionId);
}

// This hub is for the trusted gateway, not individual devices or staff clients.
public sealed class DeviceGatewayHub(
    AuthenticateDeviceHandler authenticate,
    PlaceDeviceBidHandler placeBid) : Hub<IDeviceGatewayClient>
{
    public Task<AuthenticateDeviceResult?> AuthenticateDevice(Guid deviceId, string secret) =>
        authenticate.HandleAsync(new AuthenticateDeviceCommand { DeviceId = deviceId, Secret = secret }, Context.ConnectionAborted);

    public async Task<ActiveDeviceSession?> ValidateDeviceSession(Guid deviceId, Guid sessionId, string secret)
    {
        var result = await authenticate.HandleAsync(
            new AuthenticateDeviceCommand { DeviceId = deviceId, Secret = secret }, Context.ConnectionAborted);
        return result?.Session?.SessionId == sessionId ? result.Session : null;
    }

    public Task<DeviceBidReceipt> PlaceBid(PlaceDeviceBidCommand command) =>
        placeBid.HandleAsync(command, Context.ConnectionAborted);
}
