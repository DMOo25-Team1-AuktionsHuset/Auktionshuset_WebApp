namespace Auktionshuset.Application.Admin.Auctions.UpdateAuction;

public sealed record UpdateAuctionResult
{
    public Guid AuctionId { get; private init; }
    public int LotCount { get; private init; }
    public int ItemCount { get; private init; }
    public IReadOnlyCollection<string> Errors { get; private init; } = [];

    /// <summary>
    /// Gets a value indicating that the auction did not exist. This is not a validation failure.
    /// </summary>
    public bool NotFound { get; private init; }

    public bool Succeeded => Errors.Count == 0 && !NotFound;

    public static UpdateAuctionResult Updated(Guid auctionId, int lotCount, int itemCount) =>
        new() { AuctionId = auctionId, LotCount = lotCount, ItemCount = itemCount };

    public static UpdateAuctionResult Invalid(IReadOnlyCollection<string> errors) =>
        new() { Errors = errors };

    public static UpdateAuctionResult Missing() =>
        new() { NotFound = true };
}
