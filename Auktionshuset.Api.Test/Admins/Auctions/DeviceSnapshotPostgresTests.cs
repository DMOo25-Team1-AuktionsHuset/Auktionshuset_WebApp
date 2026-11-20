using Auktionshuset.Api.Services.DeviceGateway;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Service.Auctions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public sealed partial class AuctionStockPostgresTests
{
    [PostgresStockFact]
    public async Task DeviceSnapshotReadsBidsAndRejectsReleasedSession()
    {
        var data = await DeviceBidSetupAsync();
        var services = new ServiceCollection();
        services.AddDbContext<AHDBContext>(options => options.UseNpgsql(connectionString));
        await using var provider = services.BuildServiceProvider();
        var reader = new DeviceCurrentStateReader(provider.GetRequiredService<IServiceScopeFactory>(), Clock);
        var initial = await reader.ReadAsync(data.Device, data.Session, default);
        Assert.NotNull(initial);
        Assert.Equal(data.Revision, initial.AuctionRevision);
        Assert.Null(initial.BidSequence);
        await using var db = Db();
        var result = await new AuctionLotBiddingStore(db, Clock, new TestOutbox(db)).PlaceBidAsync(
            new(data.Line, data.Customer, data.Session, Guid.NewGuid(), 12.34m, data.Revision, data.Device, 1), default);
        Assert.True(result.Accepted);
        var state = await reader.ReadAsync(data.Device, data.Session, default);
        Assert.Equal(12.34m, state!.HighestBid);
        Assert.Equal(12.34m, state.CustomerBid);
        Assert.Equal(1, state.BidSequence);
        Assert.Equal(data.Revision + 1, state.AuctionRevision);
        await AssignmentRepo(db).ReleaseAsync(data.Device, data.Auction, data.Session, default);
        Assert.Null(await reader.ReadAsync(data.Device, data.Session, default));
    }
}
