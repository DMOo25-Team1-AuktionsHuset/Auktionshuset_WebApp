using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Admin.Auctions.Bids;
using Auktionshuset.Application.Auction;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Auktionshuset.Application.Admin.Auctions;
using Npgsql;
using BidEntity = Auktionshuset.Domain.Entities.Bid;

namespace Auktionshuset.Infrastructure.Service.Auctions
{
    public class AuctionLotBiddingStore(AHDBContext db, TimeProvider clock, IOutboxWriter outboxWriter) : IPlaceBidStore, ICloseAuctionLotStore
    {
        public async Task<PlaceBidResult> PlaceBidAsync(PlaceBidCommand command, CancellationToken cancellationToken)
        {
            // Checks the requests basic values
            if (command.RequestId == Guid.Empty || command.Amount <= 0)
            {
                return Rejected("InvalidBidRequest", 0m);
            }

            // Returns a receipt promptly, if it exists
            PlaceBidResult? previousResult = await FindPreviousResultAsync(command, cancellationToken);
            if (previousResult is not null) return previousResult;

            // Starts the transaction for a new bid
            await using IDbContextTransaction transaction =
                await db.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                bool foundAuction = await SqlAuctionWriteLock.AcquireForLotAsync(
                    db, 
                    command.AuctionLotId, 
                    cancellationToken);

                if (!foundAuction) return Rejected("AuctionItemNotFound", 0m);

                await SqlAuctionLotLock.AcquireAsync(
                    db,
                    command.AuctionLotId,
                    cancellationToken);

                // The receipt may have been created while waiting for the lock, so check again
                previousResult = await FindPreviousResultAsync(command, cancellationToken);

                if (previousResult is not null) return previousResult;
            

                Customer? customer = await db.Customers.SingleOrDefaultAsync(
                    c => c.CustomerId == command.CustomerId,
                    cancellationToken);

                if (customer is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Rejected("InvalidCustomer", 0m);
                }

                AuctionLot? item = await db.AuctionLot
                    .Include(al => al.Auction)
                    .SingleOrDefaultAsync(al => al.AuctionLotId == command.AuctionLotId, cancellationToken);

                if (item == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Rejected("AuctionItemNotFound", 0m);
                }

                DateTime nowUtc = clock.GetUtcNow().UtcDateTime;

                DeviceSession? session = null;

                if (command.DeviceSessionId is Guid deviceSessionId)
                {
                    session = await db.DeviceSessions.SingleOrDefaultAsync(
                        s => s.DeviceSessionId == deviceSessionId
                          && s.CustomerId == command.CustomerId
                          && s.AuctionId == item.AuctionId
                          && s.StartedAt <= nowUtc
                          && (s.EndedAt == null || s.EndedAt > nowUtc),
                        cancellationToken);

                    if (session is null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Rejected("InvalidDeviceSession", 0m);
                    }
                }

                var leader = await db.Bids
                    .Where(b => b.AuctionLotId == item.AuctionLotId)
                    .OrderByDescending(b => b.Amount)
                    .ThenBy(b => b.SequenceNumber)
                    .Select(b => new { b.BidId, b.Amount })
                    .FirstOrDefaultAsync(cancellationToken);

                decimal currentLeaderPrice = leader?.Amount ?? item.StartingPrice;
                bool bidIsTooLow = leader is null
                    ? command.Amount < item.StartingPrice
                    : command.Amount <= leader.Amount;

                bool auctionIsLive = 
                    item.Auction.StartedAtTime is not null
                        && item.Auction.ClosedAtTime is null
                        && item.Auction.AuctionStatus == AuctionStatuses.Live
                        && item.ClosedAtTime is null
                        && item.Auction.StartsAt <= nowUtc
                        && (item.Auction.EndedAt is null
                            || nowUtc < item.Auction.EndedAt.Value);

                string? errorCode = null;

                if (!item.OpenForBids || !auctionIsLive)
                {
                    errorCode = "BiddingClosed";
                }
                else if (bidIsTooLow)
                {
                    errorCode = "BidTooLow";
                }

                if (errorCode != null)
                {
                    PlaceBidResult rejected = Rejected(errorCode, currentLeaderPrice);
                    await SaveKeyAsync(
                        command, 
                        rejected, 
                        nowUtc, 
                        cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return rejected;
                }

                // the item lock makes MAX + 1 safe for this AuctionLot
                int lastSequence = await db.Bids
                                       .Where(b => b.AuctionLotId == item.AuctionLotId)
                                       .MaxAsync(b => (int?)b.SequenceNumber, cancellationToken)
                                   ?? 0;
                
                var bid = new BidEntity
                {
                    BidId = Guid.NewGuid(),
                    Amount = command.Amount,
                    PlacedAt = nowUtc,
                    SequenceNumber = checked(lastSequence + 1),
                    AuctionLotId = item.AuctionLotId,
                    AuctionLot = item,
                    CustomerId = customer.CustomerId,
                    Customer = customer,
                    DeviceSessionId = session?.DeviceSessionId,
                    DeviceSession = session
                };

                db.Bids.Add(bid);

                item.CurrentHighestBidId = bid.BidId;
                item.CurrentHighestBid = bid;

                var accepted = new PlaceBidResult(
                Accepted: true,
                Duplicate: false,
                BidId: bid.BidId,
                SequenceNumber: bid.SequenceNumber,
                CurrentPrice: bid.Amount,
                ErrorCode: null);

                await outboxWriter.AddAsync(new BidPlacedIntegrationEvent(
                        EventId: Guid.NewGuid(),
                        BidId: bid.BidId,
                        AuctionId: item.AuctionId,
                        AuctionLotId: item.AuctionLotId,
                        CustomerId: customer.CustomerId,
                        Amount: bid.Amount,
                        SequenceNumber: bid.SequenceNumber,
                        OccurredAt: nowUtc),
                    cancellationToken);

                await SaveKeyAsync(command, accepted, nowUtc, cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return accepted;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                await transaction.RollbackAsync(cancellationToken);
                await transaction.DisposeAsync();
                db.ChangeTracker.Clear();

                // Handles two concurrent copies of one RequestId. If another
                // constraint caused the exception, no matching receipt exists
                // and the exception is rethrown.
                BidCommandKey? previous = await db.BidCommandKeys
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        r => r.CustomerId == command.CustomerId
                          && r.RequestId == command.RequestId,
                        cancellationToken);

                if (previous is null)
                {
                    throw;
                }

                return previous.Matches(command.AuctionLotId, command.Amount)
                    ? previous.ToResult(duplicate: true)
                    : Rejected(
                        "RequestIdReuseWithDifferentPayload",
                        previous.CurrentPrice);
            }
        }

        private async Task<PlaceBidResult?> FindPreviousResultAsync(
            PlaceBidCommand command,
            CancellationToken cancellationToken)
        {
            BidCommandKey? previous = await db.BidCommandKeys
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    key => key.CustomerId == command.CustomerId
                        && key.RequestId == command.RequestId,
                    cancellationToken);

            if (previous is null) return null;

            return previous.Matches(command.AuctionLotId, command.Amount)
                ? previous.ToResult(duplicate: true)
                : Rejected(
                    "RequestIdReuseWithDifferentPayload",
                    previous.CurrentPrice);
        }

        public async Task<Guid?> CloseAuctionLotAsync(Guid auctionLotId, CancellationToken cancellationToken)
        {
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            bool foundAuction = await SqlAuctionWriteLock.AcquireForLotAsync(
                db,
                auctionLotId,
                cancellationToken);

            if (!foundAuction) throw new KeyNotFoundException($"Genstand {auctionLotId} findes ikke.");

            await SqlAuctionLotLock.AcquireAsync(
                db,
                auctionLotId,
                cancellationToken);

            AuctionLot? item = await db.AuctionLot
                .Include(candidate => candidate.Auction)
                .SingleOrDefaultAsync(candidate => candidate.AuctionLotId == auctionLotId, 
                                      cancellationToken);

            if (item is null) throw new KeyNotFoundException($"Genstand {auctionLotId} findes ikke.");

            if (item.ClosedAtTime is not null) return item.CurrentHighestBidId;

            if (item.Auction.StartedAtTime is null
                || item.Auction.ClosedAtTime is not null
                || item.Auction.AuctionStatus != AuctionStatuses.Live)
                throw new AuctionStateConflictException("Auktionslinjen kan ikke lukkes fra denne tilstand.");

            Guid? winningBidId = await AuctionLotCloser.StageAsync(
                db,
                outboxWriter,
                item,
                clock.GetUtcNow().UtcDateTime,
                cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return winningBidId;
        }

        private async Task SaveKeyAsync(
            PlaceBidCommand command,
            PlaceBidResult result,
            DateTime now,
            CancellationToken cancellationToken)
        {
            db.BidCommandKeys.Add(new BidCommandKey
            {
                BidCommandKeyId = Guid.NewGuid(),
                CustomerId = command.CustomerId,
                DeviceSessionId = command.DeviceSessionId,
                RequestId = command.RequestId,
                AuctionLotId = command.AuctionLotId,
                RequestedAmount = command.Amount,
                Accepted = result.Accepted,
                BidId = result.BidId,
                SequenceNumber = result.SequenceNumber,
                CurrentPrice = result.CurrentPrice,
                ErrorCode = result.ErrorCode,
                ProcessedAt = now
            });

            // This also saves an accepted Bid added earlier in PlaceBidAsync.
            await db.SaveChangesAsync(cancellationToken);
        }


        private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
                ex.GetBaseException() is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation
                };

        private static PlaceBidResult Rejected(string errorCode, decimal currentPrice) =>
                new(
                    Accepted: false,
                    Duplicate: false,
                    BidId: null,
                    SequenceNumber: null,
                    CurrentPrice: currentPrice,
                    ErrorCode: errorCode);
    }
}
