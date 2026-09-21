namespace Auktionshuset.Contracts.Dto.Admin.Lot.UpdateLot {
    public sealed record UpdateLotNotification(
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
