namespace Auktionshuset.Contracts.Dto.Admin.Lot;

public sealed record LotListItemResponse(
    Guid LotId,
    string Name,
    string Category,
    int Quantity,
    decimal EstimatedValue,
    string Description,
    IReadOnlyList<string> Tags,
    Guid AuctionHouseId);
