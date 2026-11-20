using Auktionshuset.Api.Services.DeviceGateway;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public sealed class DeviceSnapshotTests
{
    [Fact]
    public async Task SnapshotUsesActiveSessionAndOnlyItsCustomersLatestBid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<AHDBContext>(o => o.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var house = new AuctionHouse { AuctionHouseId = Guid.NewGuid(), AuctionHouseName = "Test", Address = "Test", CVRNumber = 12345678, PhoneNumber = "12345678", Email = "house@example.test" };
        var auction = new Auction { AuctionId = Guid.NewGuid(), AuctionHouseId = house.AuctionHouseId, Name = "Test", StartsAt = now.AddHours(-1), StartedAtTime = now.AddHours(-1), AuctionStatus = "Live", AuctionRevision = 4 };
        var lot = new Lot { LotId = Guid.NewGuid(), AuctionHouseId = house.AuctionHouseId, Name = "Test", Category = "Test", Description = "Test", Quantity = 1, EstimatedValue = 1 };
        var line = new AuctionLot { AuctionLotId = Guid.NewGuid(), AuctionId = auction.AuctionId, LotId = lot.LotId, Quantity = 1, StartingPrice = 1.23m, OpenForBids = true };
        auction.SelectedAuctionLotId = line.AuctionLotId;
        var customer = Customer("first");
        var other = Customer("other");
        var device = new Device { DeviceId = Guid.NewGuid(), AuctionHouseId = house.AuctionHouseId, DeviceNumber = 1, Status = "Active" };
        var session = new DeviceSession { DeviceSessionId = Guid.NewGuid(), DeviceId = device.DeviceId, AuctionId = auction.AuctionId, CustomerId = customer.CustomerId, StartedAt = now.AddMinutes(-1), EndedAt = null };
        db.AddRange(house, auction, lot, line, customer, other, device, session,
            new DeviceCredential { DeviceId = device.DeviceId, SecretHash = "hash", IsEnabled = true, CredentialVersion = 1, SessionRevision = 7 });
        await db.SaveChangesAsync();
        var reader = new DeviceCurrentStateReader(provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
        var initial = await reader.ReadAsync(device.DeviceId, session.DeviceSessionId, default);
        Assert.NotNull(initial);
        Assert.Equal(7, initial.SessionRevision);
        Assert.Null(initial.HighestBid);
        Assert.Null(initial.BidSequence);
        Assert.Equal(1.23m, initial.StartingPrice);
        var first = Bid(line.AuctionLotId, customer.CustomerId, 1, 2.01m, now);
        var second = Bid(line.AuctionLotId, other.CustomerId, 2, 3.02m, now);
        db.Bids.AddRange(first, second);
        await db.SaveChangesAsync();
        line.CurrentHighestBidId = second.BidId;
        await db.SaveChangesAsync();
        var state = await reader.ReadAsync(device.DeviceId, session.DeviceSessionId, default);
        Assert.NotNull(state);
        Assert.Equal(customer.CustomerId, state.CustomerId);
        Assert.Equal(2.01m, state.CustomerBid);
        Assert.Equal(3.02m, state.HighestBid);
        Assert.Equal(2, state.BidSequence);
        Assert.True(state.OpenForBids);
        Assert.Null(await reader.ReadAsync(device.DeviceId, Guid.NewGuid(), default));
        auction.SelectedAuctionLotId = null;
        await db.SaveChangesAsync();
        state = await reader.ReadAsync(device.DeviceId, session.DeviceSessionId, default);
        Assert.Null(state!.AuctionLotId);
        Assert.Null(state.CustomerBid);
        session.EndedAt = now;
        await db.SaveChangesAsync();
        Assert.Null(await reader.ReadAsync(device.DeviceId, session.DeviceSessionId, default));
    }

    [Fact]
    public async Task RegistrationInvalidationReconnectAndReleaseRefreshState()
    {
        var gateway = new DeviceGatewayProcessor();
        var device = Guid.NewGuid(); var session = Guid.NewGuid(); var auction = Guid.NewGuid();
        var reader = new Reader { State = new(device, session, Guid.NewGuid(), auction, 1, null, null, null, null, null, null) };
        var sink = new Sink();
        var client = new DeviceSnapshotConnection(device, session, gateway, reader, sink);
        Assert.True(await client.RegisterAsync(default));
        Assert.Single(sink.States);
        await gateway.AuctionChangedAsync(Guid.NewGuid(), auction, 1, default);
        Assert.Single(sink.States);
        reader.State = reader.State with { AuctionRevision = 3 };
        await gateway.AuctionChangedAsync(Guid.NewGuid(), auction, 3, default);
        await gateway.ResynchronizeAsync(default);
        Assert.Equal(3, sink.States.Last().AuctionRevision);
        await gateway.SetUnavailableAsync(default);
        await gateway.ResynchronizeAsync(default);
        Assert.Equal(4, sink.States.Count);
        reader.State = null;
        await gateway.AssignmentChangedAsync(Guid.NewGuid(), device, session, default);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!sink.Released) await Task.Delay(10, timeout.Token);
        Assert.True(sink.Released);
        Assert.False(client.Unregister());
        Assert.True(sink.Unavailable >= 4);
    }

    private static Customer Customer(string name) => new() { CustomerId = Guid.NewGuid(), FirstName = name, LastName = "Test", Email = name + "@example.test", PhoneNumber = "12345678", Address = "Test", BirthDate = new(1990, 1, 1) };
    private static Bid Bid(Guid line, Guid customer, int sequence, decimal amount, DateTime now) => new() { BidId = Guid.NewGuid(), AuctionLotId = line, CustomerId = customer, SequenceNumber = sequence, Amount = amount, PlacedAt = now, DeviceSessionId = null, DeviceSession = null!, Customer = null! };
    private sealed class Reader : IDeviceCurrentStateReader
    {
        public DeviceCurrentState? State { get; set; }
        public Task<DeviceCurrentState?> ReadAsync(Guid deviceId, Guid sessionId, CancellationToken cancellationToken) => Task.FromResult(State);
    }
    private sealed class Sink : IDeviceSnapshotSink
    {
        public List<DeviceCurrentState> States { get; } = [];
        public int Unavailable { get; private set; }
        public bool Released { get; private set; }
        public Task SetUnavailableAsync(CancellationToken cancellationToken) { Unavailable++; return Task.CompletedTask; }
        public Task ApplySnapshotAsync(DeviceCurrentState state, long connectionStamp, CancellationToken cancellationToken) { States.Add(state); return Task.CompletedTask; }
        public Task SetReleasedAsync(CancellationToken cancellationToken) { Released = true; return Task.CompletedTask; }
    }
}
