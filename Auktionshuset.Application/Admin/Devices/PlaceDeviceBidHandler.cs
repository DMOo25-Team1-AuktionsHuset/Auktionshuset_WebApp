using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Admin.Devices.AuthenticateDevice;
using Auktionshuset.Application.Auction;
using Auktionshuset.Application.Devices.AuthenticateDevice;

namespace Auktionshuset.Application.Admin.Devices;

public sealed record PlaceDeviceBidCommand(
    Guid DeviceId,
    string Secret,
    Guid SessionId,
    Guid AuctionId,
    Guid AuctionLotId,
    long AuctionRevision,
    Guid RequestId,
    decimal Amount,
    long? SessionRevision = null)
{
    public override string ToString() => $"{nameof(PlaceDeviceBidCommand)} {{ DeviceId = {DeviceId}, SessionId = {SessionId}, RequestId = {RequestId} }}";
}

public sealed record DeviceBidReceipt(
    Guid DeviceId,
    Guid SessionId,
    Guid AuctionId,
    Guid AuctionLotId,
    long RequestedAuctionRevision,
    Guid RequestId,
    bool Accepted,
    bool Duplicate,
    Guid? BidId,
    int? SequenceNumber,
    decimal? CurrentPrice,
    string? ErrorCode,
    long? AuctionRevision = null);

public sealed class PlaceDeviceBidHandler(
    AuthenticateDeviceHandler authenticateDevice,
    PlaceBidHandler placeBid)
{
    public async Task<DeviceBidReceipt> HandleAsync(PlaceDeviceBidCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        DeviceBidReceipt Reject(string errorCode) => new(
            command.DeviceId, command.SessionId, command.AuctionId, command.AuctionLotId,
            command.AuctionRevision, command.RequestId, false, false, null, null, null, errorCode);

        var authenticated = await authenticateDevice.HandleAsync(
            new AuthenticateDeviceCommand { DeviceId = command.DeviceId, Secret = command.Secret }, cancellationToken);
        if (authenticated is null)
            return Reject("InvalidDeviceCredentials");

        var session = authenticated.Session;
        if (session is null || session.SessionId != command.SessionId || session.DeviceId != command.DeviceId ||
                    (command.SessionRevision is not null && session.SessionRevision != command.SessionRevision))
            return Reject("InvalidDeviceSession");

        if (session.AuctionId != command.AuctionId)
            return Reject("AuctionContextChanged");

        if (command.AuctionLotId == Guid.Empty || command.RequestId == Guid.Empty || command.AuctionRevision < 0)
            return Reject("InvalidBidRequest");

        var result = await placeBid.HandleAsync(
            new PlaceBidCommand(command.AuctionLotId, session.CustomerId, session.SessionId,
                command.RequestId, command.Amount, command.AuctionRevision,
                authenticated.DeviceId, authenticated.CredentialVersion), cancellationToken);

        return new DeviceBidReceipt(
            command.DeviceId, session.SessionId, session.AuctionId, command.AuctionLotId,
            command.AuctionRevision, command.RequestId, result.Accepted, result.Duplicate,
            result.BidId, result.SequenceNumber, result.CurrentPrice, result.ErrorCode, result.AuctionRevision);
    }
}
