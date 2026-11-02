namespace Auktionshuset.Application.Abstraction.Auction;

public sealed record SharedAuctionLotState(Guid AuctionId, Guid? AuctionLotId, long AuctionRevision);

public interface ISharedAuctionLotStore
{
    Task<SharedAuctionLotState?> GetAsync(Guid auctionId, CancellationToken cancellationToken);
    Task<SharedAuctionLotState?> SelectAsync(Guid auctionId, Guid? auctionLotId, CancellationToken cancellationToken);
}
