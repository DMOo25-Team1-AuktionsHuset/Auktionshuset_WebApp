namespace Auktionshuset.Application.Abstraction.Auction {
    public interface ICloseAuctionLotStore {
        Task<Guid?> CloseAuctionLotAsync(Guid AuctionLotId, CancellationToken cancellationToken);
    }
}
