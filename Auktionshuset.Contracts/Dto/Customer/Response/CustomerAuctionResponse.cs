namespace Auktionshuset.Contracts.Dto.Customer.Response;

public sealed record CustomerAuctionResponse(Guid AuctionId, string Name, DateTime StartsAt,
    DateTime? EndsAt, int LotCount, bool IsRegistered, string Status);

public sealed record CustomerAuctionDetailResponse(Guid AuctionId, string Name, bool IsLive,
    IReadOnlyList<CustomerAuctionLotResponse> Lots, DateTime StartsAt, string Status);

public sealed record CustomerAuctionLotResponse(Guid AuctionLotId, string Name, string Description,
    int Quantity, decimal StartingPrice, decimal CurrentPrice, bool HasBids, bool OpenForBids,
    decimal? OwnHighestBid = null, bool IsLeading = false, bool IsClosed = false,
    bool IsWinner = false, decimal? HammerPrice = null)
{
    public string BidStatus => IsClosed
        ? HammerPrice is null ? "Usolgt" : IsWinner ? "Vundet" : OwnHighestBid.HasValue ? "Tabt" : "Solgt"
        : !HasBids ? "Ingen bud" : IsLeading ? "Du fører" : OwnHighestBid.HasValue ? "Du er overbudt" : "Du har ikke budt";
}

public sealed record CustomerPurchaseResponse(Guid AuctionId, string AuctionName, Guid AuctionLotId,
    string Name, int Quantity, decimal HammerPrice, DateTime ClosedAt);
