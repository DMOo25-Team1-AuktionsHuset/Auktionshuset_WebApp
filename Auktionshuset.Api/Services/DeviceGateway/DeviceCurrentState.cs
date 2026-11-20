using System.Data;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Api.Services.DeviceGateway;

public sealed record DeviceCurrentState(Guid DeviceId, Guid SessionId, Guid CustomerId, Guid AuctionId,
    long AuctionRevision, Guid? AuctionLotId, bool? OpenForBids, decimal? StartingPrice,
    decimal? HighestBid, decimal? CustomerBid, int? BidSequence, long SessionRevision = 0);

public interface IDeviceCurrentStateReader
{
    Task<DeviceCurrentState?> ReadAsync(Guid deviceId, Guid sessionId, CancellationToken cancellationToken);
}

public sealed class DeviceCurrentStateReader(IServiceScopeFactory scopes, TimeProvider clock) : IDeviceCurrentStateReader
{
    public async Task<DeviceCurrentState?> ReadAsync(Guid deviceId, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
        var isolation = db.Database.IsNpgsql() ? IsolationLevel.RepeatableRead : IsolationLevel.Serializable;
        await using var transaction = await db.Database.BeginTransactionAsync(isolation, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var session = await (from s in db.DeviceSessions.AsNoTracking()
            join c in db.DeviceCredentials.AsNoTracking() on s.DeviceId equals c.DeviceId
            where s.DeviceId == deviceId && s.DeviceSessionId == sessionId && c.IsEnabled
                && s.StartedAt <= now && (s.EndedAt == null || s.EndedAt > now)
            select new { s.DeviceId, s.DeviceSessionId, s.CustomerId, s.AuctionId, c.SessionRevision }).SingleOrDefaultAsync(cancellationToken);
        if (session is null) return null;

        var auction = await db.Auction.AsNoTracking().Where(a => a.AuctionId == session.AuctionId)
            .Select(a => new { a.SelectedAuctionLotId, a.AuctionRevision, a.StartedAtTime, a.ClosedAtTime,
                a.StartsAt, a.EndedAt, a.AuctionStatus }).SingleAsync(cancellationToken);
        var lot = await db.AuctionLot.AsNoTracking().Where(l => l.AuctionId == session.AuctionId
                && l.AuctionLotId == auction.SelectedAuctionLotId)
            .Select(l => new { l.AuctionLotId, l.OpenForBids, l.ClosedAtTime, l.StartingPrice,
                Highest = l.CurrentHighestBid == null ? (decimal?)null : l.CurrentHighestBid.Amount })
            .SingleOrDefaultAsync(cancellationToken);
        var bids = lot is null ? null : await db.Bids.AsNoTracking().Where(b => b.AuctionLotId == lot.AuctionLotId)
            .GroupBy(b => b.AuctionLotId).Select(g => new {
                Sequence = (int?)g.Max(b => b.SequenceNumber),
                Customer = g.Where(b => b.CustomerId == session.CustomerId).OrderByDescending(b => b.SequenceNumber)
                    .Select(b => (decimal?)b.Amount).FirstOrDefault()
            }).SingleOrDefaultAsync(cancellationToken);
        var open = lot is not null && lot.OpenForBids && lot.ClosedAtTime is null
            && auction.StartedAtTime is not null && auction.ClosedAtTime is null && auction.AuctionStatus == "Live"
            && auction.StartsAt <= now && (auction.EndedAt is null || now < auction.EndedAt);
        return new(session.DeviceId, session.DeviceSessionId, session.CustomerId, session.AuctionId,
            auction.AuctionRevision, lot?.AuctionLotId, lot is null ? null : open, lot?.StartingPrice,
            lot?.Highest, bids?.Customer, bids?.Sequence, session.SessionRevision);
    }
}
