namespace Auktionshuset.Contracts.Dto.Admin.Lot.CreateLot
{
    public sealed record CreateLotNotification(
        Guid EventId,
        Guid LotId,
        Guid AuctionHouseId,
        string Name,
        string Category,
        int Quantity,
        decimal EstimatedValue,
        string? ImageUrl,
        DateTime OccurredAt);
}
