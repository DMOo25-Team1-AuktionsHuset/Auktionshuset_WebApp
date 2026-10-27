namespace Auktionshuset.Contracts.Dto.Admin.Auction
{
    public sealed record CreateAuctionNotification(
        Guid EventId,
        Guid AuctionId,
        string Name,
        string Status,
        DateTime StartsAt,
        DateTime? EndsAt,
        int LotCount,
        int ItemCount,
        DateTime OccurredAt);

   
}
