using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Auction;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using BidEntity = Auktionshuset.Domain.Entities.Bid;

namespace Auktionshuset.Infrastructure.Service.Auctions
{
    public class AuctionLotBiddingStore(AHDBContext db, TimeProvider clock) : IPlaceBidStore, ICloseAuctionLotStore
    {
        public async Task<PlaceBidResult> PlaceBidAsync(PlaceBidCommand command, CancellationToken cancellationToken)
        {
            if (command.RequestId == Guid.Empty || command.Amount <= 0)
            {
                return Rejected("InvalidBidRequest", 0m);
            }

            await using var transaction =
                await db.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await SqlAuctionLotLock.AcquireAsync(db, command.AuctionLotId, cancellationToken);

                // check idempotency before checking whether the session is still active,
                // retry of already-processed request should return the original result
                var previous = await db.BidCommandKeys.SingleOrDefaultAsync(
                    r => r.CustomerId == command.CustomerId
                    && r.RequestId == command.RequestId,
                    cancellationToken);

                if (previous != null)
                {
                    await transaction.CommitAsync(cancellationToken);

                    return previous.Matches(command.AuctionLotId, command.Amount)
                        ? previous.ToResult(duplicate: true)
                        : Rejected("RequestIdReuseWithDifferentPayload", previous.CurrentPrice);
                }

                var customer = await db.Customers.SingleOrDefaultAsync(
                    c => c.CustomerId == command.CustomerId,
                    cancellationToken);

                if (customer is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Rejected("InvalidCustomer", 0m);
                }

                var item = await db.AuctionLot
                    .Include(al => al.Auction)
                    .SingleOrDefaultAsync(al => al.AuctionLotId == command.AuctionLotId, cancellationToken);

                if (item == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Rejected("AuctionItemNotFound", 0m);
                }

                var now = clock.GetLocalNow().DateTime;

                DeviceSession? session = null;

                if (command.DeviceSessionId is Guid deviceSessionId)
                {
                    session = await db.DeviceSessions.SingleOrDefaultAsync(
                        s => s.DeviceSessionId == deviceSessionId
                          && s.CustomerId == command.CustomerId
                          && s.AuctionId == item.AuctionId
                          && s.StartedAt <= now
                          && (s.EndedAt == null || s.EndedAt > now),
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

                var currentLeaderPrice = leader?.Amount ?? item.StartingPrice;
                bool bidIsTooLow = leader is null
                    ? command.Amount < item.StartingPrice
                    : command.Amount <= leader.Amount;
                bool auctionIsLive = item.Auction.StartsAt <= now &&
                    (item.Auction.EndedAt != null || now < item.Auction.EndedAt);

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
                    var rejected = Rejected(errorCode, currentLeaderPrice);
                    await SaveKeyAsync(command, rejected, now, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return rejected;
                }

                // the item lock makes MAX + 1 safe for this AuctionLot
                var lastSequence = await db.Bids
                    .Where(b => b.AuctionLotId == item.AuctionLotId)
                    .MaxAsync(b => (int?)b.SequenceNumber, cancellationToken)
                    ?? 0;

                var bid = new BidEntity
                {
                    BidId = Guid.NewGuid(),
                    Amount = command.Amount,
                    PlacedAt = now,
                    SequenceNumber = checked(lastSequence + 1),
                    AuctionLotId = item.AuctionLotId,
                    AuctionLot = item,
                    CustomerId = customer.CustomerId,
                    Customer = customer,
                    DeviceSessionId = session?.DeviceSessionId,
                    DeviceSession = session
                };

                db.Bids.Add(bid);

                var accepted = new PlaceBidResult(
                Accepted: true,
                Duplicate: false,
                BidId: bid.BidId,
                SequenceNumber: bid.SequenceNumber,
                CurrentPrice: bid.Amount,
                ErrorCode: null);

                await SaveKeyAsync(command, accepted, now, cancellationToken);

                // for RabbiqMQ/SignalR delivery add outbox row here, before SaveChanges in same transaction

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return accepted;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();

                // Handles two concurrent copies of one RequestId. If another
                // constraint caused the exception, no matching receipt exists
                // and the exception is rethrown.
                var previous = await db.BidCommandKeys
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
                        "RequestIdReusedWithDifferentPayload",
                        previous.CurrentPrice);
            }
        }

        public async Task<Guid?> CloseAuctionLotAsync(Guid auctionLotId, CancellationToken cancellationToken)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            await SqlAuctionLotLock.AcquireAsync(db, auctionLotId, cancellationToken);

            var item = await db.AuctionLot.SingleOrDefaultAsync(
                al => al.AuctionLotId == auctionLotId,
                cancellationToken);

            if (item == null)
            {
                throw new KeyNotFoundException($"AuctionLot {auctionLotId} does not exist");
            }

            if (!item.OpenForBids)
            {
                await transaction.CommitAsync(cancellationToken);
                return item.CurrentHighestBidId;
            }

            var winner = await db.Bids
                .Where(b => b.AuctionLotId == auctionLotId)
                .OrderByDescending(b => b.Amount)
                .ThenBy(b => b.SequenceNumber)
                .Select(b => new { b.BidId })
                .FirstOrDefaultAsync(cancellationToken);

            item.OpenForBids = false;
            item.CurrentHighestBidId = winner?.BidId;

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return winner?.BidId;
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
