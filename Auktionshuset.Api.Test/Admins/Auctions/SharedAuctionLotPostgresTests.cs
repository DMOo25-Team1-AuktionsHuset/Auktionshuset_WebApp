using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Auction;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service.Auctions;
using Auktionshuset.Infrastructure.Service.Auth;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public sealed partial class AuctionStockPostgresTests
{
    [PostgresStockFact]
    public async Task QueuedDeviceBidRechecksRevisionAfterAuctionLock()
    {
        var scheduled = await ScheduledAuctionAsync();
        await using var setup = Db();
        var device = new Device { DeviceId = Guid.NewGuid(), AuctionHouseId = houseId, DeviceNumber = 1, Status = "Available" };
        var deviceSession = new DeviceSession { DeviceSessionId = Guid.NewGuid(), DeviceId = device.DeviceId, Device = device,
            CustomerId = scheduled.CustomerId, AuctionId = scheduled.AuctionId, StartedAt = Now.AddMinutes(-1), EndedAt = null };
        setup.DeviceSessions.Add(deviceSession);
        setup.DeviceCredentials.Add(new DeviceCredential { DeviceId = device.DeviceId,
            SecretHash = new DeviceSecretService().HashSecret(new string('a', 64)), IsEnabled = true, CredentialVersion = 1 });
        await setup.SaveChangesAsync();
        var selector = new SharedAuctionLotStore(setup, Sessions(setup), new TestOutbox(setup), Clock);
        var selected = await selector.SelectAsync(scheduled.AuctionId, scheduled.LineId, default);
        await using var locked = Db();
        await using var bidding = Db();
        await using var transaction = await Sessions(locked).BeginAsync(scheduled.AuctionId, default);
        var bid = new AuctionLotBiddingStore(bidding, Clock, new TestOutbox(bidding)).PlaceBidAsync(
            new PlaceBidCommand(scheduled.LineId, scheduled.CustomerId, deviceSession.DeviceSessionId, Guid.NewGuid(), 5, selected!.AuctionRevision, device.DeviceId, 1), default);
        var auction = await locked.Auction.SingleAsync(a => a.AuctionId == scheduled.AuctionId);
        auction.SelectedAuctionLotId = null;
        auction.AuctionRevision++;
        await transaction.CommitAsync(default);
        Assert.Equal("AuctionContextChanged", (await bid).ErrorCode);
        Assert.Empty(await bidding.Bids.ToListAsync());
    }

    [PostgresStockFact]
    public async Task ConcurrentSelectionAndLotCloseCannotLeaveClosedLotSelected()
    {
        var scheduled = await ScheduledAuctionAsync();
        await using var selecting = Db();
        await using var closing = Db();
        var selection = new SharedAuctionLotStore(selecting, Sessions(selecting), new TestOutbox(selecting), Clock);
        var closer = new AuctionLotBiddingStore(closing, Clock, new TestOutbox(closing));
        async Task SelectAsync()
        {
            try { await selection.SelectAsync(scheduled.AuctionId, scheduled.LineId, default); }
            catch (AuctionStateConflictException) { }
        }
        await Task.WhenAll(SelectAsync(), closer.CloseAuctionLotAsync(scheduled.LineId, default));
        await using var verify = Db();
        var auction = await verify.Auction.AsNoTracking().SingleAsync(a => a.AuctionId == scheduled.AuctionId);
        var line = await verify.AuctionLot.AsNoTracking().SingleAsync(l => l.AuctionLotId == scheduled.LineId);
        Assert.Null(auction.SelectedAuctionLotId);
        Assert.NotNull(line.ClosedAtTime);
        Assert.True(auction.AuctionRevision >= 2);
        Assert.Equal(auction.AuctionRevision, await verify.OutboxMessages.LongCountAsync(m => m.EventType == "AuctionRevisionChangedIntegrationEvent"));
    }
}
