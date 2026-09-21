namespace Auktionshuset.Contracts.Dto.Admin.Auction;

public sealed record AuctionLotResponse(
    Guid LotId,
    string Name,
    string Category,
    int Quantity,
    decimal EstimatedValue,
    string? ImageUrl);

public sealed record AuctionDetailResponse(
    Guid AuctionId,
    string Name,
    string Status,
    DateTime StartsAt,
    DateTime EndsAt,
    Guid EmployeeId,
    string EmployeeName,
    int LotCount,
    int ItemCount,
    IReadOnlyList<AuctionLotResponse> Lots);
