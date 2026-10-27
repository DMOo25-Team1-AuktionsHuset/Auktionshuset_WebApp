namespace Auktionshuset.Contracts.Dto.Admin.Auction
{
    public sealed record BidPlacedNotification(
    Guid EventId,
    Guid BidId,
    Guid AuctionLotId,
    decimal Amount,
    int SequenceNumber,
    DateTime OccurredAt);
}
