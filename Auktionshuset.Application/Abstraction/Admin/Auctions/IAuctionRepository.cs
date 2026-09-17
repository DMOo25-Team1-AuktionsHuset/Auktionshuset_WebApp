using Auktionshuset.Domain.Entities;
using AuctionEntity = Auktionshuset.Domain.Entities.Auction;

namespace Auktionshuset.Application.Abstraction.Admin.Auctions;

public interface IAuctionRepository
{
    Task AddAsync(
        AuctionEntity auction,
        IReadOnlyCollection<AuctionLot> auctionLots,
        CancellationToken cancellationToken);

    Task<AuctionEntity?> GetByIdAsync(Guid auctionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuctionLot>> GetAuctionLotsAsync(Guid auctionId, CancellationToken cancellationToken);
}
