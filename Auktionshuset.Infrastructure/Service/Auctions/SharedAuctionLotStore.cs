using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Auction = Auktionshuset.Domain.Entities.Auction;

namespace Auktionshuset.Infrastructure.Service.Auctions;

public sealed class SharedAuctionLotStore(AHDBContext db, IAuctionWriteSessionFactory sessions, IOutboxWriter outbox, TimeProvider clock) : ISharedAuctionLotStore
{
    public Task<SharedAuctionLotState?> GetAsync(Guid auctionId, CancellationToken cancellationToken) =>
        db.Auction.AsNoTracking().Where(a => a.AuctionId == auctionId)
            .Select(a => new SharedAuctionLotState(a.AuctionId, a.SelectedAuctionLotId, a.AuctionRevision))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<SharedAuctionLotState?> SelectAsync(Guid auctionId, Guid? auctionLotId, CancellationToken cancellationToken)
    {
        await using var session = await sessions.BeginAsync(auctionId, cancellationToken);
        Auction? auction = await db.Auction.SingleOrDefaultAsync(a => a.AuctionId == auctionId, cancellationToken);
        if (auction is null) return null;
        // A scoped context may already track this auction from before the lock was acquired.
        await db.Entry(auction).ReloadAsync(cancellationToken);
        if (auctionLotId is Guid id)
        {
            DateTime now = clock.GetUtcNow().UtcDateTime;
            bool valid = auction.StartedAtTime is not null && auction.ClosedAtTime is null
                && auction.StartsAt <= now && (auction.EndedAt is null || now < auction.EndedAt)
                && auction.AuctionStatus == AuctionStatuses.Live
                && await db.AuctionLot.AnyAsync(l => l.AuctionId == auctionId && l.AuctionLotId == id
                    && l.ClosedAtTime == null && l.OpenForBids, cancellationToken);
            if (!valid) throw new AuctionStateConflictException("Genstanden skal være åben og tilhøre den aktive auktion.");
        }
        if (auction.SelectedAuctionLotId != auctionLotId)
        {
            auction.SelectedAuctionLotId = auctionLotId;
            await AuctionRevision.AdvanceAsync(auction, outbox, clock.GetUtcNow().UtcDateTime, cancellationToken);
            await session.CommitAsync(cancellationToken);
        }
        return new(auction.AuctionId, auction.SelectedAuctionLotId, auction.AuctionRevision);
    }
}

internal static class AuctionRevision
{
    public static Task AdvanceAsync(Auction auction, IOutboxWriter outbox, DateTime now, CancellationToken cancellationToken)
    {
        auction.AuctionRevision = checked(auction.AuctionRevision + 1);
        return outbox.AddAsync(new AuctionRevisionChangedIntegrationEvent(Guid.NewGuid(), auction.AuctionId,
            auction.SelectedAuctionLotId, auction.AuctionRevision, now), cancellationToken);
    }
}
