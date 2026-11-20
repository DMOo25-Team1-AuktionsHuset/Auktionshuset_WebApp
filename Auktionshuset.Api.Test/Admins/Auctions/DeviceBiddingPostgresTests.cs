using Auktionshuset.Application.Auction;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service.Auctions;
using Auktionshuset.Infrastructure.Service.Auth;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public sealed partial class AuctionStockPostgresTests
{
    private async Task<(Guid Auction, Guid Line, Guid Customer, Guid Device, Guid Session, long Revision)> DeviceBidSetupAsync()
    {
        var scheduled = await ScheduledAuctionAsync();
        await using var db = Db();
        var device = new Device { DeviceId = Guid.NewGuid(), AuctionHouseId = houseId, DeviceNumber = 1, Status = "Active" };
        var session = new DeviceSession { DeviceSessionId = Guid.NewGuid(), DeviceId = device.DeviceId, Device = device,
            CustomerId = scheduled.CustomerId, AuctionId = scheduled.AuctionId, StartedAt = Now.AddMinutes(-1), EndedAt = null };
        db.DeviceSessions.Add(session);
        db.DeviceCredentials.Add(new DeviceCredential { DeviceId = device.DeviceId,
            SecretHash = new DeviceSecretService().HashSecret(new string('a', 64)), IsEnabled = true, CredentialVersion = 1 });
        await db.SaveChangesAsync();
        var selected = await new SharedAuctionLotStore(db, Sessions(db), new TestOutbox(db), Clock)
            .SelectAsync(scheduled.AuctionId, scheduled.LineId, default);
        return (scheduled.AuctionId, scheduled.LineId, scheduled.CustomerId, device.DeviceId, session.DeviceSessionId, selected!.AuctionRevision);
    }

    [PostgresStockFact]
    public async Task ConcurrentWebAndDeviceBidsSerializeAndPreserveTheWinningBid()
    {
        var data = await DeviceBidSetupAsync();
        await using var owner = Db();
        await using var web = Db();
        await using var device = Db();
        Task<PlaceBidResult> webBid, deviceBid;
        await using (var held = await Sessions(owner).BeginAsync(data.Auction, default))
        {
            webBid = new AuctionLotBiddingStore(web, Clock, new TestOutbox(web)).PlaceBidAsync(
                new(data.Line, data.Customer, null, Guid.NewGuid(), 10), default);
            deviceBid = new AuctionLotBiddingStore(device, Clock, new TestOutbox(device)).PlaceBidAsync(
                new(data.Line, data.Customer, data.Session, Guid.NewGuid(), 10, data.Revision, data.Device, 1), default);
            await held.CommitAsync(default);
        }
        var results = await Task.WhenAll(webBid, deviceBid);
        Assert.Single(results, r => r.Accepted);
        Assert.Contains(results, r => !r.Accepted && (r.ErrorCode == "BidTooLow" || r.ErrorCode == "AuctionContextChanged"));
        await using var verify = Db();
        var bid = await verify.Bids.SingleAsync();
        Assert.Equal(10m, bid.Amount);
        Assert.Equal(bid.BidId, (await verify.AuctionLot.SingleAsync()).CurrentHighestBidId);
        Assert.Equal(results.Single(r => r.Accepted).BidId, bid.BidId);
    }

    [PostgresStockFact]
    public async Task DeviceRejectedLowBidIsIdempotentAndChangedPayloadCannotReuseRequestId()
    {
        var data = await DeviceBidSetupAsync();
        await using var db = Db();
        var store = new AuctionLotBiddingStore(db, Clock, new TestOutbox(db));
        var command = new PlaceBidCommand(data.Line, data.Customer, data.Session, Guid.NewGuid(), 0.5m, data.Revision, data.Device, 1);
        var rejected = await store.PlaceBidAsync(command, default);
        Assert.Equal("BidTooLow", rejected.ErrorCode);
        Assert.Equal(1m, rejected.CurrentPrice);
        Assert.Equal(data.Revision, rejected.AuctionRevision);
        var duplicate = await store.PlaceBidAsync(command, default);
        Assert.True(duplicate.Duplicate);
        Assert.Equal(rejected.ErrorCode, duplicate.ErrorCode);
        Assert.Equal(rejected.CurrentPrice, duplicate.CurrentPrice);
        Assert.Equal(rejected.AuctionRevision, duplicate.AuctionRevision);
        Assert.Equal("RequestIdReuseWithDifferentPayload", (await store.PlaceBidAsync(command with { Amount = 2 }, default)).ErrorCode);
        Assert.Equal("RequestIdReuseWithDifferentPayload", (await store.PlaceBidAsync(command with { DeviceSessionId = null }, default)).ErrorCode);
        Assert.Empty(await db.Bids.ToListAsync());
        Assert.Single(await db.BidCommandKeys.ToListAsync());
    }

    [PostgresStockFact]
    public async Task WebAndDeviceReceiptsRetainOriginalRevisionAfterLaterBids()
    {
        var data = await DeviceBidSetupAsync();
        var web = new PlaceBidCommand(data.Line, data.Customer, null, Guid.NewGuid(), 10);
        await using (var db = Db())
        {
            var first = await new AuctionLotBiddingStore(db, Clock, new TestOutbox(db)).PlaceBidAsync(web, default);
            Assert.Equal(data.Revision + 1, first.AuctionRevision);
        }
        var device = new PlaceBidCommand(data.Line, data.Customer, data.Session, Guid.NewGuid(), 20, data.Revision + 1, data.Device, 1);
        await using (var db = Db())
        {
            var accepted = await new AuctionLotBiddingStore(db, Clock, new TestOutbox(db)).PlaceBidAsync(device, default);
            Assert.Equal(data.Revision + 2, accepted.AuctionRevision);
        }
        var staleCommand = device with { RequestId = Guid.NewGuid() };
        await using (var db = Db())
        {
            var store = new AuctionLotBiddingStore(db, Clock, new TestOutbox(db));
            var stale = await store.PlaceBidAsync(staleCommand, default);
            Assert.Equal("AuctionContextChanged", stale.ErrorCode);
            Assert.Equal(data.Revision + 2, stale.AuctionRevision);
            var next = await store.PlaceBidAsync(web with { RequestId = Guid.NewGuid(), Amount = 30 }, default);
            Assert.Equal(data.Revision + 3, next.AuctionRevision);
        }
        await using (var db = Db())
        {
            var store = new AuctionLotBiddingStore(db, Clock, new TestOutbox(db));
            var webRetry = await store.PlaceBidAsync(web, default);
            var deviceRetry = await store.PlaceBidAsync(device, default);
            var staleRetry = await store.PlaceBidAsync(staleCommand, default);
            Assert.True(staleRetry.Duplicate);
            Assert.Equal("AuctionContextChanged", staleRetry.ErrorCode);
            Assert.Equal(data.Revision + 2, staleRetry.AuctionRevision);
            Assert.True(webRetry.Duplicate);
            Assert.Equal(data.Revision + 1, webRetry.AuctionRevision);
            Assert.True(deviceRetry.Duplicate);
            Assert.Equal(data.Revision + 2, deviceRetry.AuctionRevision);
        }
    }

    [PostgresStockFact]
    public async Task ConcurrentDuplicateDeviceBidsPersistOneReceiptAndOneBid()
    {
        var data = await DeviceBidSetupAsync();
        var command = new PlaceBidCommand(data.Line, data.Customer, data.Session, Guid.NewGuid(), 10, data.Revision, data.Device, 1);
        await using var owner = Db();
        await using var first = Db();
        await using var second = Db();
        Task<PlaceBidResult> a, b;
        await using (var held = await Sessions(owner).BeginAsync(data.Auction, default))
        {
            a = new AuctionLotBiddingStore(first, Clock, new TestOutbox(first)).PlaceBidAsync(command, default);
            b = new AuctionLotBiddingStore(second, Clock, new TestOutbox(second)).PlaceBidAsync(command, default);
            await held.CommitAsync(default);
        }
        var results = await Task.WhenAll(a, b);
        Assert.All(results, r => Assert.True(r.Accepted));
        Assert.Single(results, r => r.Duplicate);
        Assert.Equal(results[0].BidId, results[1].BidId);
        Assert.Equal(data.Revision + 1, results[0].AuctionRevision);
        Assert.Equal(results[0].AuctionRevision, results[1].AuctionRevision);
        await using var verify = Db();
        Assert.Single(await verify.Bids.ToListAsync());
        Assert.Single(await verify.BidCommandKeys.ToListAsync());
        Assert.Equal(1, await verify.OutboxMessages.CountAsync(m => m.EventType == "BidPlacedIntegrationEvent"));
    }

    [PostgresStockFact]
    public async Task WrongOrReleasedDeviceSessionsCannotPlaceNewBidsButReceiptCanBeRetried()
    {
        var data = await DeviceBidSetupAsync();
        await using var db = Db();
        var store = new AuctionLotBiddingStore(db, Clock, new TestOutbox(db));
        var command = new PlaceBidCommand(data.Line, data.Customer, data.Session, Guid.NewGuid(), 10, data.Revision, data.Device, 1);
        Assert.Equal("InvalidDeviceSession", (await store.PlaceBidAsync(command with { DeviceSessionId = Guid.NewGuid() }, default)).ErrorCode);
        var otherCustomer = new Customer { CustomerId = Guid.NewGuid(), FirstName = "Other", LastName = "Customer", Email = "other@example.test",
            PhoneNumber = "12345678", Address = "Test", BirthDate = new DateOnly(1990, 1, 1) };
        db.Customers.Add(otherCustomer);
        await db.SaveChangesAsync();
        Assert.Equal("InvalidDeviceSession", (await store.PlaceBidAsync(command with { CustomerId = otherCustomer.CustomerId }, default)).ErrorCode);
        var accepted = await store.PlaceBidAsync(command, default);
        Assert.True(accepted.Accepted);
        await using (var releasing = Db())
            await AssignmentRepo(releasing).ReleaseAsync(data.Device, data.Auction, data.Session, default);
        var duplicate = await store.PlaceBidAsync(command, default);
        Assert.True(duplicate.Duplicate);
        Assert.Equal(accepted.BidId, duplicate.BidId);
        Assert.Equal("InvalidDeviceSession", (await store.PlaceBidAsync(command with { RequestId = Guid.NewGuid() }, default)).ErrorCode);
        Assert.Single(await db.Bids.ToListAsync());
    }

    [PostgresStockFact]
    public Task QueuedDeviceBidRejectsReleasedSession() => QueuedDeviceBidAfterChangeAsync("release");

    [PostgresStockFact]
    public Task QueuedDeviceBidRejectsUnregisteredCustomer() => QueuedDeviceBidAfterChangeAsync("unregister");

    [PostgresStockFact]
    public Task QueuedDeviceBidRejectsClosedLot() => QueuedDeviceBidAfterChangeAsync("close");

    private async Task QueuedDeviceBidAfterChangeAsync(string change)
    {
        var data = await DeviceBidSetupAsync();
        await using var owner = Db();
        await using var bidding = Db();
        await using var observer = Db();
        await bidding.Database.OpenConnectionAsync();
        await observer.Database.OpenConnectionAsync();
        var bidderPid = ((NpgsqlConnection)bidding.Database.GetDbConnection()).ProcessID;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var held = await Sessions(owner).BeginAsync(data.Auction, timeout.Token);
        var ownerPid = ((NpgsqlConnection)owner.Database.GetDbConnection()).ProcessID;
        var bid = new AuctionLotBiddingStore(bidding, Clock, new TestOutbox(bidding)).PlaceBidAsync(
            new(data.Line, data.Customer, data.Session, Guid.NewGuid(), 10, data.Revision, data.Device, 1), timeout.Token);

        await using (var check = new NpgsqlCommand("SELECT @owner = ANY(pg_blocking_pids(@bidder))",
            (NpgsqlConnection)observer.Database.GetDbConnection()))
        {
            check.Parameters.AddWithValue("owner", ownerPid);
            check.Parameters.AddWithValue("bidder", bidderPid);
            while (!(bool)(await check.ExecuteScalarAsync(timeout.Token))!)
            {
                if (bid.IsCompleted) await bid;
                await Task.Delay(25, timeout.Token);
            }
        }

        if (change == "release")
        {
            var session = await owner.DeviceSessions.SingleAsync(s => s.DeviceSessionId == data.Session, timeout.Token);
            session.EndedAt = Now;
        }
        else if (change == "unregister")
        {
            var registration = await owner.AuctionRegistrations.SingleAsync(
                r => r.AuctionId == data.Auction && r.CustomerId == data.Customer, timeout.Token);
            owner.AuctionRegistrations.Remove(registration);
        }
        else
        {
            var line = await owner.AuctionLot.SingleAsync(l => l.AuctionLotId == data.Line, timeout.Token);
            var auction = await owner.Auction.SingleAsync(a => a.AuctionId == data.Auction, timeout.Token);
            line.OpenForBids = false;
            line.ClosedAtTime = Now;
            auction.SelectedAuctionLotId = null;
            auction.AuctionRevision++;
        }
        await held.CommitAsync(timeout.Token);
        var result = await bid;
        Assert.False(result.Accepted);
        Assert.Equal(change == "release" ? "InvalidDeviceSession" : change == "unregister" ? "NotRegistered" : "AuctionContextChanged", result.ErrorCode);
        await using var verify = Db();
        Assert.Empty(await verify.Bids.Where(b => b.AuctionLotId == data.Line).ToListAsync(timeout.Token));
    }

    [PostgresStockFact]
    public async Task ConcurrentLotAndAuctionCloseAfterDeviceBidProduceOneWinnerAndStockDebit()
    {
        var data = await DeviceBidSetupAsync();
        await using var bidding = Db();
        var accepted = await new AuctionLotBiddingStore(bidding, Clock, new TestOutbox(bidding)).PlaceBidAsync(
            new(data.Line, data.Customer, data.Session, Guid.NewGuid(), 10, data.Revision, data.Device, 1), default);
        Assert.True(accepted.Accepted);
        await using var lotClose = Db();
        await using var auctionClose = Db();
        await Task.WhenAll(new AuctionLotBiddingStore(lotClose, Clock, new TestOutbox(lotClose)).CloseAuctionLotAsync(data.Line, default),
            Lifecycle(auctionClose).CloseAsync(data.Auction, default));
        await using var verify = Db();
        var line = await verify.AuctionLot.SingleAsync();
        Assert.NotNull(line.ClosedAtTime);
        Assert.Equal(accepted.BidId, line.CurrentHighestBidId);
        Assert.Equal(6, (await verify.Lot.SingleAsync()).Quantity);
        Assert.Single(await verify.Bids.ToListAsync());
    }
}
