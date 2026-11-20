namespace Auktionshuset.Application.Auction
{
    public sealed record PlaceBidCommand(
        Guid AuctionLotId,
        Guid CustomerId,
        Guid? DeviceSessionId,
        Guid RequestId,
        decimal Amount,
        long? AuctionRevision = null,
        Guid? DeviceId = null,
        int? CredentialVersion = null);
}
