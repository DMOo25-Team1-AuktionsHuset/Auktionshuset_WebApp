namespace Auktionshuset.Application.Admin.Auctions.CreateAuction;

public sealed record CreateAuctionResult
{
    public Guid AuctionId { get; private init; }
    public int LotCount { get; private init; }
    public IReadOnlyCollection<string> Errors { get; private init; } = [];
    public bool Succeeded => Errors.Count == 0;

    public static CreateAuctionResult Created(Guid auctionId, int lotCount) =>
        new() { AuctionId = auctionId, LotCount = lotCount };

    public static CreateAuctionResult Invalid(IReadOnlyCollection<string> errors) =>
        new() { Errors = errors };
}
