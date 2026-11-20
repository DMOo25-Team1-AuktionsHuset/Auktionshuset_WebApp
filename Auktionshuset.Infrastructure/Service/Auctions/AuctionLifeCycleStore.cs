using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Admin.Auctions.UpdateAuction;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using AuctionEntity = Auktionshuset.Domain.Entities.Auction;

namespace Auktionshuset.Infrastructure.Service.Auctions
{
    public class AuctionLifeCycleStore(
        AHDBContext context,
        TimeProvider clock,
        IOutboxWriter outboxWriter,
        IAuctionWriteSessionFactory sessions) : IAuctionLifeCycleStore
    {
        public async Task<bool> StartAsync(
            Guid auctionId,
            CancellationToken cancellationToken)
        {
            await using IAuctionWriteSession session =
                await sessions.BeginAsync(auctionId, cancellationToken);

            AuctionEntity? auction = 
                await LoadAsync(auctionId, cancellationToken);

            if (auction is null) return false;

            // Start has been called before or auction is already started
            // A later close isn't changed by this repeated start call
            if (auction.StartedAtTime is not null) return true;

            if (auction.ClosedAtTime is not null
                || auction.AuctionStatus != AuctionStatuses.Upcoming)
                throw new AuctionStateConflictException(
                    "Auktionen kan ikke startes fra sin nuværende tilstand");

            if (auction.AuctionLots.Count == 0)
                throw new AuctionStateConflictException("Auktionen skal have mindst en genstand.");

            DateTime nowUtc = clock.GetUtcNow().UtcDateTime;

            if (nowUtc < auction.StartsAt || (auction.EndedAt is DateTime end && nowUtc >= end))
                throw new AuctionStateConflictException(
                    "Auktionen kan kun startes inden for sit planlagte tidsrum.");

            var stockIds = auction.AuctionLots.Select(line => line.LotId).ToArray();
            var stocks = await context.Lot.AsNoTracking().Where(lot => stockIds.Contains(lot.LotId))
                .ToDictionaryAsync(lot => lot.LotId, cancellationToken);
            var reserved = await new Auktionshuset.Infrastructure.Repositories.EFAuctionRepository(context)
                .GetReservedQuantitiesAsync(null, cancellationToken);
            foreach (var line in auction.AuctionLots)
            {
                if (line.ClosedAtTime is not null || line.Quantity <= 0 || line.StartingPrice < 0
                    || line.StartingPrice > 9999999999999999.99m || decimal.Round(line.StartingPrice, 2) != line.StartingPrice
                    || !stocks.TryGetValue(line.LotId, out var stock)
                    || stock.Quantity < reserved.GetValueOrDefault(line.LotId))
                    throw new AuctionStateConflictException($"Datakonflikt på auktionslinje {line.AuctionLotId}. Afstem antal, startpris og lager før start.");
            }

            auction.StartedAtTime = nowUtc;
            auction.AuctionStatus = AuctionStatuses.Live;

            foreach (var item in auction.AuctionLots)
                if (item.ClosedAtTime is null)
                    item.OpenForBids = true;

            await AuctionRevision.AdvanceAsync(auction, outboxWriter, nowUtc, cancellationToken);
            await AddUpdatedEventAsync(
                auction,
                nowUtc,
                cancellationToken);

            await session.CommitAsync(cancellationToken);

            return true;
        }

        public async Task<bool> CloseAsync(
            Guid auctionId,
            CancellationToken cancellationToken) =>
            await CloseCoreAsync(auctionId, onlyWhenDue: false, cancellationToken);

        public Task<bool> CloseDueAsync(Guid auctionId, CancellationToken cancellationToken) =>
            CloseCoreAsync(auctionId, onlyWhenDue: true, cancellationToken);

        private async Task<bool> CloseCoreAsync(
            Guid auctionId, bool onlyWhenDue, CancellationToken cancellationToken)
        {
            await using IAuctionWriteSession session =
                await sessions.BeginAsync(auctionId, cancellationToken);

            AuctionEntity? auction =
                await LoadAsync(auctionId, cancellationToken);

            if (auction is null) return false;

            if (auction.ClosedAtTime is not null) return true;

            DateTime nowUtc = clock.GetUtcNow().UtcDateTime;

            // Recheck after acquiring the write lock: discovery is only a hint.
            if (onlyWhenDue && (auction.StartedAtTime is null
                || auction.AuctionStatus != AuctionStatuses.Live
                || auction.EndedAt is null || auction.EndedAt > nowUtc))
                return false;

            if (auction.StartedAtTime is null || auction.AuctionStatus != AuctionStatuses.Live)
                throw new AuctionStateConflictException(
                    "Auktionen kan ikke lukkes fra sin nuværende tilstand");

            foreach (var item in auction.AuctionLots)
                await AuctionLotCloser.StageAsync(
                    context,
                    outboxWriter,
                    item,
                    nowUtc,
                    cancellationToken);

            auction.ClosedAtTime = nowUtc;
            auction.AuctionStatus = AuctionStatuses.Ended;
            auction.SelectedAuctionLotId = null;
            await AuctionRevision.AdvanceAsync(auction, outboxWriter, nowUtc, cancellationToken);

            //EndedAt is kept as planned end time, not actual end time
            await AddUpdatedEventAsync(
                auction,
                nowUtc,
                cancellationToken);

            await session.CommitAsync(cancellationToken);

            return true;
        }



        private Task<AuctionEntity?> LoadAsync(
            Guid auctionId,
            CancellationToken cancellationToken) =>
            context.Auction
                .Include(auction => auction.AuctionLots)
                .SingleOrDefaultAsync(
                    auction => auction.AuctionId == auctionId,
                    cancellationToken);

        private Task AddUpdatedEventAsync(
            AuctionEntity auction,
            DateTime nowUtc,
            CancellationToken cancellationToken) =>
            outboxWriter.AddAsync(
                new AuctionUpdatedIntegrationEvent(
                    EventId: Guid.NewGuid(),
                    AuctionId: auction.AuctionId,
                    Name: auction.Name,
                    Status: auction.AuctionStatus,
                    StartsAt: auction.StartsAt,
                    EndsAt: auction.EndedAt,
                    LotCount: auction.AuctionLots.Count,
                    ItemCount: auction.AuctionLots.Sum(item => item.Quantity),
                    OccurredAt: nowUtc),
                cancellationToken);
    }
}
