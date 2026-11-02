using Auktionshuset.Domain.Entities;
using AuctionEntity = Auktionshuset.Domain.Entities.Auction;

namespace Auktionshuset.Application.Abstraction.Admin.Auctions;

public interface IAuctionRepository
{
    // Open lines reserve stock until explicitly closed, even after the planned end.
    async Task<IReadOnlyDictionary<Guid, long>> GetReservedQuantitiesAsync(Guid? excludingAuctionId, CancellationToken cancellationToken)
    {
        var totals = new Dictionary<Guid, long>();
        foreach (var auction in await GetAllAsync(cancellationToken))
        {
            if (auction.AuctionId == excludingAuctionId || auction.ClosedAtTime is not null
                || (auction.AuctionStatus != "Kommende" && auction.AuctionStatus != "Live")) continue;
            foreach (var line in await GetAuctionLotsAsync(auction.AuctionId, cancellationToken))
                if (line.ClosedAtTime is null)
                    totals[line.LotId] = line.Quantity <= 0 || totals.GetValueOrDefault(line.LotId) == long.MaxValue
                        ? long.MaxValue // Invalid legacy counts block new reservations for this stock.
                        : totals.GetValueOrDefault(line.LotId) + line.Quantity;
        }
        return totals;
    }

    Task AddAsync(
        AuctionEntity auction,
        IReadOnlyCollection<AuctionLot> auctionLots,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets every auction, ordered by start time.
    /// </summary>
    Task<IReadOnlyList<AuctionEntity>> GetAllAsync(CancellationToken cancellationToken);

    Task<AuctionEntity?> GetByIdAsync(Guid auctionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuctionLot>> GetAuctionLotsAsync(Guid auctionId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces an existing auction and its lot relationships.
    /// </summary>
    /// <param name="auction">The auction holding the updated values.</param>
    /// <param name="auctionLots">The lot relationships that replace the stored ones.</param>
    /// <returns><see langword="true"/> when the auction existed and was updated; otherwise, <see langword="false"/>.</returns>
    Task<bool> UpdateAsync(
        AuctionEntity auction,
        IReadOnlyCollection<AuctionLot> auctionLots,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an auction together with its lot relationships.
    /// </summary>
    /// <returns><see langword="true"/> when the auction existed and was deleted; otherwise, <see langword="false"/>.</returns>
    Task<bool> DeleteAsync(Guid auctionId, CancellationToken cancellationToken);
}
