using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Application.Admin.Auctions.UpdateAuction;
using Auktionshuset.Application.Admin.Auctions.DeleteAuction;
using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Auktionshuset.Application.Auction;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Repositories;
using Auktionshuset.Infrastructure.Service.Auctions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Auktionshuset.Api.Test.Admins.Auctions;

// Opt in with an isolated PostgreSQL server. Each test creates and drops its own database.
public sealed class PostgresStockFactAttribute : FactAttribute
{
    public PostgresStockFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AUCTION_STOCK_TEST_CONNECTION")))
            Skip = "Set AUCTION_STOCK_TEST_CONNECTION to an isolated PostgreSQL test server.";
    }
}

public sealed partial class AuctionStockPostgresTests : IAsyncLifetime
{
    private string databaseName = "codex_stock_" + Guid.NewGuid().ToString("N");
    private bool resumedAcceptanceDatabase;
    private string connectionString = "";
    private Guid houseId, lotId;
    private static readonly DateTime Now = new(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeProvider Clock = new FixedClock();

    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("AUCTION_STOCK_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(source)) return;
        var resumeDatabase = Environment.GetEnvironmentVariable("AH_USB_ACCEPTANCE_RESUME_DB");
        if (resumeDatabase is not null)
        {
            if (Environment.GetEnvironmentVariable("AH_BROWSER_ACCEPTANCE") != "1" ||
                !System.Text.RegularExpressions.Regex.IsMatch(resumeDatabase, @"\Acodex_stock_[0-9a-f]{32}\z"))
                throw new InvalidOperationException("Invalid isolated USB acceptance database name.");
            databaseName = resumeDatabase;
            resumedAcceptanceDatabase = true;
        }
        await using var admin = new NpgsqlConnection(source);
        await admin.OpenAsync();
        if (!resumedAcceptanceDatabase)
        {
            // The identifier is generated internally, never supplied by configuration.
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
            await command.ExecuteNonQueryAsync();
        }
        connectionString = new NpgsqlConnectionStringBuilder(source) { Database = databaseName, Pooling = false }.ConnectionString;
        await using var db = Db();
        if (resumedAcceptanceDatabase)
        {
            if (!await db.Set<Device>().AnyAsync(d => d.DeviceNumber == 9001))
                throw new InvalidOperationException("The isolated acceptance database has no device 9001.");
            return;
        }
        await db.Database.EnsureCreatedAsync();
        var house = new AuctionHouse { AuctionHouseId = Guid.NewGuid(), AuctionHouseName = "Stock tests",
            Address = "Test", CVRNumber = 12345678, PhoneNumber = "12345678", Email = "stock@example.test" };
        houseId = house.AuctionHouseId;
        var lot = new Lot { LotId = Guid.NewGuid(), AuctionHouseId = houseId, AuctionHouse = house,
            Name = "Stole", Category = "Møbler", Description = "Samlet linje", Quantity = 10, EstimatedValue = 100 };
        lotId = lot.LotId;
        db.Lot.Add(lot);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (connectionString.Length == 0 || resumedAcceptanceDatabase) return;
        await using var admin = new NpgsqlConnection(Environment.GetEnvironmentVariable("AUCTION_STOCK_TEST_CONNECTION"));
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    private AHDBContext Db() => new(new DbContextOptionsBuilder<AHDBContext>().UseNpgsql(connectionString).Options);
    private static PostgresAuctionWriteSessionFactory Sessions(AHDBContext db) => new(db, new EfUnitOfWork(db));
    private static CreateAuctionHandler Creator(AHDBContext db) => new(new EFAuctionRepository(db), new EFLotRepo(db),
        new EFEmployeeRepository(db), new TestOutbox(db), Sessions(db), Clock);
    private static UpdateAuctionHandler Updater(AHDBContext db) => new(new EFAuctionRepository(db), new EFLotRepo(db),
        new EFEmployeeRepository(db), new TestOutbox(db), Sessions(db), Clock);
    private static AuctionLifeCycleStore Lifecycle(AHDBContext db) => new(db, Clock, new TestOutbox(db), Sessions(db));
    private CreateAuctionCommand Create(int quantity, decimal? price = 0) => new("Stock auction", Now.AddHours(1), null,
        null, houseId, [new AuctionLotSelection(lotId, quantity, price)]);
    private UpdateAuctionCommand Update(Guid id, int quantity, decimal? price = null) => new(id, "Stock auction", Now.AddHours(1), null,
        null, houseId, quantity == 0 ? [] : [new AuctionLotSelection(lotId, quantity, price)]);

    [PostgresStockFact]
    public async Task PricesRoundTripThroughCreateEditAndDetailAndOmittedPriceIsPreserved()
    {
        await using var db = Db();
        var created = await Creator(db).HandleAsync(Create(3, 12.34m), default);
        Assert.True(created.Succeeded);
        var lineId = (await db.AuctionLot.AsNoTracking().SingleAsync()).AuctionLotId;
        Assert.True((await Updater(db).HandleAsync(Update(created.AuctionId, 4), default)).Succeeded);
        db.ChangeTracker.Clear();
        Assert.Equal(12.34m, (await db.AuctionLot.SingleAsync()).StartingPrice);
        Assert.True((await Updater(db).HandleAsync(Update(created.AuctionId, 4, 25.75m), default)).Succeeded);
        db.ChangeTracker.Clear();
        Assert.Equal(25.75m, (await db.AuctionLot.SingleAsync()).StartingPrice);
        Assert.True((await Updater(db).HandleAsync(Update(created.AuctionId, 4, 0), default)).Succeeded);
        db.ChangeTracker.Clear();
        var line = await db.AuctionLot.SingleAsync();
        Assert.Equal(0, line.StartingPrice);
        Assert.Equal(lineId, line.AuctionLotId);
        var detail = await new Auktionshuset.Application.Admin.Auctions.GetAuctions.GetAuctionsHandler(
            new EFAuctionRepository(db), new EFEmployeeRepository(db)).HandleByIdAsync(created.AuctionId, default);
        Assert.Equal(0, Assert.Single(detail!.Lots).StartingPrice);
    }

    [PostgresStockFact]
    public async Task InvalidPricesAreRejectedWithoutChangingStoredPricesOrReservations()
    {
        await using var db = Db();
        var created = await Creator(db).HandleAsync(Create(2, 17.50m), default);
        foreach (var price in new[] { -0.01m, 0.001m, 10000000000000000m, decimal.MaxValue })
        {
            Assert.False((await Creator(db).HandleAsync(Create(1, price), default)).Succeeded);
            Assert.False((await Updater(db).HandleAsync(Update(created.AuctionId, 9, price), default)).Succeeded);
        }
        db.ChangeTracker.Clear();
        var line = await db.AuctionLot.SingleAsync();
        Assert.Equal(17.50m, line.StartingPrice);
        Assert.Equal(2, line.Quantity);
        Assert.Single(await db.Auction.ToListAsync());
    }

    [PostgresStockFact]
    public async Task EditRemoveAndDeleteReleaseReservationsAndPastPlannedEndStillReserves()
    {
        await using var db = Db();
        var first = await Creator(db).HandleAsync(Create(8), default);
        Assert.False((await Creator(db).HandleAsync(Create(3), default)).Succeeded);
        Assert.True((await Updater(db).HandleAsync(Update(first.AuctionId, 5), default)).Succeeded);
        var second = await Creator(db).HandleAsync(Create(5), default);
        Assert.True(second.Succeeded);
        Assert.False((await Updater(db).HandleAsync(Update(first.AuctionId, 6), default)).Succeeded);
        Assert.True((await Updater(db).HandleAsync(Update(first.AuctionId, 0), default)).Succeeded);
        var auction = await db.Auction.SingleAsync(a => a.AuctionId == second.AuctionId);
        auction.StartsAt = Now.AddDays(-2); auction.EndedAt = Now.AddDays(-1);
        await db.SaveChangesAsync();
        Assert.False((await Creator(db).HandleAsync(Create(6), default)).Succeeded);
        Assert.True(await new DeleteAuctionHandler(new EFAuctionRepository(db), new TestOutbox(db), Sessions(db), Clock)
            .HandleAsync(new DeleteAuctionCommand(second.AuctionId), default));
        Assert.True((await Creator(db).HandleAsync(Create(10), default)).Succeeded);
        Assert.Equal(10, (await db.Lot.SingleAsync()).Quantity);
    }

    [PostgresStockFact]
    public async Task ConcurrentCreatesAndEditsCannotOverReserveAcrossDifferentAuctions()
    {
        await using var db = Db();
        // Queue both transactions behind a real PostgreSQL advisory lock, then release them together.
        await using var owner = Db();
        await using var a = Db();
        await using var b = Db();
        CreateAuctionResult[] results;
        await using (var held = await Sessions(owner).BeginAsync(Guid.Empty, default))
        {
            var first = Creator(a).HandleAsync(Create(6), default);
            var second = Creator(b).HandleAsync(Create(6), default);
            await Task.Delay(150);
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
            await held.CommitAsync(default);
            results = await Task.WhenAll(first, second);
        }
        var winner = Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => !result.Succeeded);
        var small = await Creator(db).HandleAsync(Create(2), default);
        await using var c = Db();
        await using var d = Db();
        await using (var held = await Sessions(owner).BeginAsync(Guid.Empty, default))
        {
            var first = Updater(c).HandleAsync(Update(winner.AuctionId, 8), default);
            var second = Updater(d).HandleAsync(Update(small.AuctionId, 8), default);
            await Task.Delay(150);
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
            await held.CommitAsync(default);
            var edits = await Task.WhenAll(first, second);
            Assert.Single(edits, result => result.Succeeded);
            Assert.Single(edits, result => !result.Succeeded);
        }
        Assert.True(await db.AuctionLot.SumAsync(line => line.Quantity) <= 10);
    }

    [PostgresStockFact]
    public async Task WarehouseCannotReduceBelowReservationsAndLegacyConflictsBlockStart()
    {
        await using var db = Db();
        var created = await Creator(db).HandleAsync(Create(8), default);
        var handler = new UpdateLotHandler(new EFLotRepo(db), new TestOutbox(db), Sessions(db), Clock, new EFAuctionRepository(db));
        await Assert.ThrowsAsync<AuctionStateConflictException>(() => handler.HandleAsync(
            new UpdateLotCommand(lotId, houseId, "Stole", "Møbler", 7, 100, "Test", []), default));
        db.ChangeTracker.Clear();
        Assert.Equal(10, (await db.Lot.SingleAsync()).Quantity);
        // Simulate pre-existing conflicting data; production handlers never create it.
        var stock = await db.Lot.SingleAsync(); stock.Quantity = 5;
        var auction = await db.Auction.SingleAsync(); auction.StartsAt = Now.AddHours(-1);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<AuctionStateConflictException>(() => Lifecycle(db).StartAsync(created.AuctionId, default));
        Assert.Equal(5, (await db.Lot.AsNoTracking().SingleAsync()).Quantity);
        Assert.Equal(8, (await db.AuctionLot.AsNoTracking().SingleAsync()).Quantity);
    }

    [PostgresStockFact]
    public async Task WholeLineIsSoldOnceUnsoldStockIsReleasedAndZeroStartRequiresPositiveBid()
    {
        await using var db = Db();
        var sold = await Creator(db).HandleAsync(Create(4, 0), default);
        var unsold = await Creator(db).HandleAsync(Create(6, 0), default);
        foreach (var auction in await db.Auction.ToListAsync()) auction.StartsAt = Now.AddHours(-1);
        var customer = new Customer { CustomerId = Guid.NewGuid(), FirstName = "Test", LastName = "Kunde", Email = "bid@example.test",
            PhoneNumber = "12345678", Address = "Test", BirthDate = new DateOnly(1990, 1, 1) };
        db.AuctionRegistrations.Add(new AuctionRegistration { AuctionId = sold.AuctionId, CustomerId = customer.CustomerId,
            Customer = customer, RegisteredAt = Now });
        await db.SaveChangesAsync();
        Assert.True(await Lifecycle(db).StartAsync(sold.AuctionId, default));
        Assert.True(await Lifecycle(db).StartAsync(unsold.AuctionId, default));
        Assert.False((await Creator(db).HandleAsync(Create(1), default)).Succeeded); // live lines still reserve
        var line = await db.AuctionLot.SingleAsync(a => a.AuctionId == sold.AuctionId);
        var bids = new AuctionLotBiddingStore(db, Clock, new TestOutbox(db));
        foreach (var invalid in new[] { 0m, -1m, 0.001m, decimal.MaxValue })
            Assert.False((await bids.PlaceBidAsync(new PlaceBidCommand(line.AuctionLotId, customer.CustomerId, null, Guid.NewGuid(), invalid), default)).Accepted);
        Assert.True((await bids.PlaceBidAsync(new PlaceBidCommand(line.AuctionLotId, customer.CustomerId, null, Guid.NewGuid(), 0.01m), default)).Accepted);
        var winner = await bids.CloseAuctionLotAsync(line.AuctionLotId, default);
        Assert.NotNull(winner);
        Assert.Equal(winner, await bids.CloseAuctionLotAsync(line.AuctionLotId, default));
        Assert.True(await Lifecycle(db).CloseAsync(sold.AuctionId, default));
        Assert.True(await Lifecycle(db).CloseAsync(sold.AuctionId, default));
        Assert.True(await Lifecycle(db).CloseAsync(unsold.AuctionId, default));
        db.ChangeTracker.Clear();
        Assert.Equal(6, (await db.Lot.SingleAsync()).Quantity);
        Assert.Empty(await ((Auktionshuset.Application.Abstraction.Admin.Auctions.IAuctionRepository)new EFAuctionRepository(db)).GetReservedQuantitiesAsync(null, default));
        Assert.True((await Creator(db).HandleAsync(Create(6), default)).Succeeded);
        Assert.Equal(0.01m, (await db.Bids.SingleAsync()).Amount); // whole line price, never multiplied by four
    }

    [PostgresStockFact]
    public async Task ConcurrentCloseDebitsOnceAndFailedCloseRollsBackStockWinnerAndOutbox()
    {
        await using var db = Db();
        var created = await Creator(db).HandleAsync(Create(4, 50), default);
        var auction = await db.Auction.SingleAsync(); auction.StartsAt = Now.AddHours(-1);
        await db.SaveChangesAsync();
        await Lifecycle(db).StartAsync(created.AuctionId, default);
        var line = await db.AuctionLot.SingleAsync();
        var customer = new Customer { CustomerId = Guid.NewGuid(), FirstName = "Test", LastName = "Kunde", Email = "close@example.test",
            PhoneNumber = "12345678", Address = "Test", BirthDate = new DateOnly(1990, 1, 1) };
        db.Bids.Add(new Bid { BidId = Guid.NewGuid(), AuctionLotId = line.AuctionLotId, CustomerId = customer.CustomerId,
            Customer = customer, DeviceSessionId = null, DeviceSession = null, Amount = 50, SequenceNumber = 1, PlacedAt = Now });
        await db.SaveChangesAsync();
        var before = await db.OutboxMessages.CountAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AuctionLifeCycleStore(db, Clock, new FailingOutbox(), Sessions(db))
            .CloseAsync(created.AuctionId, default));
        db.ChangeTracker.Clear();
        Assert.Equal(10, (await db.Lot.SingleAsync()).Quantity);
        Assert.Null((await db.AuctionLot.SingleAsync()).ClosedAtTime);
        Assert.Null((await db.AuctionLot.SingleAsync()).CurrentHighestBidId);
        Assert.Equal(before, await db.OutboxMessages.CountAsync());
        await using var a = Db(); await using var b = Db();
        await Task.WhenAll(Lifecycle(a).CloseAsync(created.AuctionId, default), Lifecycle(b).CloseAsync(created.AuctionId, default));
        db.ChangeTracker.Clear();
        Assert.Equal(6, (await db.Lot.SingleAsync()).Quantity);
        Assert.Equal(before + 4, await db.OutboxMessages.CountAsync());
    }

    [PostgresStockFact]
    public async Task AuditReportsLegacyConflictsWithoutRewritingDataAndHistoricalCloseDoesNotDebit()
    {
        await using var db = Db();
        var created = await Creator(db).HandleAsync(Create(8, 15), default);
        var stock = await db.Lot.SingleAsync(); stock.Quantity = 5;
        var line = await db.AuctionLot.SingleAsync(); line.StartingPrice = -1;
        var auction = await db.Auction.SingleAsync(); auction.AuctionStatus = "Live"; // Missing actual start.
        await db.SaveChangesAsync();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "scripts", "audit-auction-stock.sql")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = await File.ReadAllTextAsync(Path.Combine(directory.FullName, "scripts", "audit-auction-stock.sql"));
        await using (var reader = await command.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync()); // over-reserved stock
            Assert.Equal(lotId, reader.GetGuid(0));
            Assert.True(await reader.NextResultAsync());
            Assert.True(await reader.ReadAsync()); // invalid price
            Assert.Equal(line.AuctionLotId, reader.GetGuid(0));
            Assert.True(await reader.NextResultAsync());
            Assert.True(await reader.ReadAsync()); // invalid lifecycle markers
        }
        db.ChangeTracker.Clear();
        Assert.Equal(5, (await db.Lot.SingleAsync()).Quantity);
        line = await db.AuctionLot.SingleAsync();
        Assert.Equal(8, line.Quantity); Assert.Equal(-1, line.StartingPrice);
        line.Quantity = -8;
        await db.SaveChangesAsync();
        Assert.False((await Creator(db).HandleAsync(Create(1), default)).Succeeded); // Negative legacy counts never create free stock.
        // A pre-rollout closed row uses its stored close marker; stock is never guessed/backfilled.
        line.ClosedAtTime = Now.AddDays(-1);
        auction = await db.Auction.SingleAsync(); auction.ClosedAtTime = line.ClosedAtTime;
        auction.AuctionStatus = "Afsluttet";
        await db.SaveChangesAsync();
        Assert.True(await Lifecycle(db).CloseAsync(created.AuctionId, default));
        Assert.Null(await new AuctionLotBiddingStore(db, Clock, new TestOutbox(db)).CloseAuctionLotAsync(line.AuctionLotId, default));
        Assert.Equal(5, (await db.Lot.AsNoTracking().SingleAsync()).Quantity);
    }

    private ServiceProvider ExpirationServices(TimeProvider clock, Func<AHDBContext, IOutboxWriter>? outbox = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Db());
        services.AddSingleton(clock);
        services.AddScoped<IOutboxWriter>(s => outbox?.Invoke(s.GetRequiredService<AHDBContext>())
            ?? new TestOutbox(s.GetRequiredService<AHDBContext>()));
        services.AddScoped<IAuctionWriteSessionFactory>(s => Sessions(s.GetRequiredService<AHDBContext>()));
        services.AddScoped<AuctionLifeCycleStore>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static AuctionExpirationWorker Worker(ServiceProvider services, TimeProvider clock) =>
        new(services.GetRequiredService<IServiceScopeFactory>(), clock, NullLogger<AuctionExpirationWorker>.Instance);

    private async Task<(Guid AuctionId, Guid LineId, Guid CustomerId)> ScheduledAuctionAsync(int quantity = 4)
    {
        await using var db = Db();
        var created = await Creator(db).HandleAsync(Create(quantity, 1), default);
        var auction = await db.Auction.SingleAsync(a => a.AuctionId == created.AuctionId);
        auction.StartsAt = Now.AddHours(-1);
        auction.EndedAt = Now.AddHours(1);
        var customer = new Customer { CustomerId = Guid.NewGuid(), FirstName = "Auto", LastName = "Test",
            Email = $"{Guid.NewGuid()}@example.test", PhoneNumber = "12345678", Address = "Test", BirthDate = new(1990, 1, 1) };
        db.AuctionRegistrations.Add(new AuctionRegistration { AuctionId = auction.AuctionId,
            CustomerId = customer.CustomerId, Customer = customer, RegisteredAt = Now });
        await db.SaveChangesAsync();
        await Lifecycle(db).StartAsync(auction.AuctionId, default);
        return (auction.AuctionId, (await db.AuctionLot.SingleAsync(l => l.AuctionId == auction.AuctionId)).AuctionLotId, customer.CustomerId);
    }

    [PostgresStockFact]
    public async Task ExpirationUsesExactUtcBoundaryAndNeverStartsUpcomingOrClosesManualAuctions()
    {
        var timed = await ScheduledAuctionAsync(2);
        var manual = await ScheduledAuctionAsync(2);
        await using var db = Db();
        var upcoming = await Creator(db).HandleAsync(Create(2), default);
        var legacy = await Creator(db).HandleAsync(Create(2), default);
        foreach (var a in await db.Auction.ToListAsync())
        {
            a.StartsAt = Now.AddHours(-1);
            a.EndedAt = a.AuctionId == manual.AuctionId ? null : Now.AddHours(1);
            if (a.AuctionId == legacy.AuctionId) a.AuctionStatus = AuctionStatuses.Live;
        }
        await db.SaveChangesAsync();
        var clock = new MutableClock(Now.AddHours(1).AddMilliseconds(-1));
        await using var services = ExpirationServices(clock);
        using var worker = Worker(services, clock);
        await worker.ProcessDueAsync(default);
        Assert.Empty(await db.Auction.AsNoTracking().Where(a => a.ClosedAtTime != null).ToListAsync());
        var bid = await new AuctionLotBiddingStore(db, clock, new TestOutbox(db)).PlaceBidAsync(
            new(timed.LineId, timed.CustomerId, null, Guid.NewGuid(), 1), default);
        Assert.True(bid.Accepted);
        clock.Now = Now.AddHours(1);
        var late = await new AuctionLotBiddingStore(db, clock, new TestOutbox(db)).PlaceBidAsync(
            new(timed.LineId, timed.CustomerId, null, Guid.NewGuid(), 2), default);
        Assert.Equal("BiddingClosed", late.ErrorCode); // Worker has not closed it yet.
        await worker.ProcessDueAsync(default);
        db.ChangeTracker.Clear();
        var closed = await db.Auction.SingleAsync(a => a.AuctionId == timed.AuctionId);
        Assert.Equal(clock.Now, closed.ClosedAtTime);
        Assert.Equal(AuctionStatuses.Ended, closed.AuctionStatus);
        Assert.Equal(bid.BidId, (await db.AuctionLot.SingleAsync(l => l.AuctionLotId == timed.LineId)).CurrentHighestBidId);
        Assert.Equal(8, (await db.Lot.SingleAsync()).Quantity);
        Assert.Null((await db.Auction.SingleAsync(a => a.AuctionId == upcoming.AuctionId)).StartedAtTime);
        Assert.Equal(1, await db.Auction.CountAsync(a => a.ClosedAtTime != null));
        await Lifecycle(db).CloseAsync(manual.AuctionId, default); // No deadline remains manually closable.
    }

    [PostgresStockFact]
    public async Task StartupCatchesMissedDeadlineAndRepeatedProcessingPublishesUnsoldOnlyOnce()
    {
        var scheduled = await ScheduledAuctionAsync();
        var clock = new MutableClock(Now.AddDays(2));
        await using var services = ExpirationServices(clock);
        using var worker = Worker(services, clock);
        await using var db = Db();
        int before = await db.OutboxMessages.CountAsync();
        await worker.StartAsync(default);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!await db.Auction.AsNoTracking().AnyAsync(a => a.ClosedAtTime != null, timeout.Token))
                await Task.Delay(20, timeout.Token);
        }
        finally { await worker.StopAsync(default); }
        using var restarted = Worker(services, clock);
        await restarted.ProcessDueAsync(default);
        Assert.Equal(before + 4, await db.OutboxMessages.CountAsync());
        var line = await db.AuctionLot.SingleAsync();
        Assert.False(line.OpenForBids);
        Assert.Null(line.CurrentHighestBidId);
        Assert.NotNull(line.ClosedAtTime);
        Assert.Equal(10, (await db.Lot.SingleAsync()).Quantity);
        Assert.Empty(await new EFAuctionRepository(db).GetReservedQuantitiesAsync(null, default));
    }

    [PostgresStockFact]
    public async Task TwoWorkersManualAuctionAndLineCloseAndLateBidProduceOneResult()
    {
        var scheduled = await ScheduledAuctionAsync();
        await using var seed = Db();
        var accepted = await new AuctionLotBiddingStore(seed, Clock, new TestOutbox(seed)).PlaceBidAsync(
            new(scheduled.LineId, scheduled.CustomerId, null, Guid.NewGuid(), 5), default);
        Assert.True(accepted.Accepted);
        var clock = new MutableClock(Now.AddHours(1));
        await using var services = ExpirationServices(clock);
        using var first = Worker(services, clock);
        using var second = Worker(services, clock);
        await using var manual = Db();
        await using var single = Db();
        await using var bidding = Db();
        var before = await seed.OutboxMessages.CountAsync();
        var late = new AuctionLotBiddingStore(bidding, clock, new TestOutbox(bidding)).PlaceBidAsync(
            new(scheduled.LineId, scheduled.CustomerId, null, Guid.NewGuid(), 10), default);
        await Task.WhenAll(first.ProcessDueAsync(default), second.ProcessDueAsync(default),
            new AuctionLifeCycleStore(manual, clock, new TestOutbox(manual), Sessions(manual)).CloseAsync(scheduled.AuctionId, default),
            new AuctionLotBiddingStore(single, clock, new TestOutbox(single)).CloseAuctionLotAsync(scheduled.LineId, default), late);
        await first.ProcessDueAsync(default);
        Assert.Equal("BiddingClosed", (await late).ErrorCode);
        Assert.Equal(6, (await seed.Lot.SingleAsync()).Quantity);
        Assert.Equal(before + 4, await seed.OutboxMessages.CountAsync());
        Assert.Single(await seed.Bids.ToListAsync());
        Assert.Equal(accepted.BidId, (await seed.AuctionLot.SingleAsync()).CurrentHighestBidId);
    }

    [PostgresStockFact]
    public async Task FailedAutomaticCloseRollsBackContinuesOtherAuctionsAndRetriesWithFreshScope()
    {
        var first = await ScheduledAuctionAsync();
        var other = await ScheduledAuctionAsync();
        await using var db = Db();
        await new AuctionLotBiddingStore(db, Clock, new TestOutbox(db)).PlaceBidAsync(
            new(first.LineId, first.CustomerId, null, Guid.NewGuid(), 5), default);
        int before = await db.OutboxMessages.CountAsync();
        var clock = new MutableClock(Now.AddDays(1));
        bool fail = true;
        await using var services = ExpirationServices(clock, context => new ConditionalOutbox(context,
            message => fail && message is Auktionshuset.Application.Admin.Auctions.CloseAuction.AuctionLotClosedIntegrationEvent e
                && e.AuctionId == first.AuctionId));
        using var worker = Worker(services, clock);
        await worker.ProcessDueAsync(default);
        db.ChangeTracker.Clear();
        Assert.Null((await db.Auction.SingleAsync(a => a.AuctionId == first.AuctionId)).ClosedAtTime);
        var failedLine = await db.AuctionLot.SingleAsync(a => a.AuctionLotId == first.LineId);
        Assert.Null(failedLine.ClosedAtTime);
        Assert.True(failedLine.OpenForBids);
        Assert.NotNull((await db.Auction.SingleAsync(a => a.AuctionId == other.AuctionId)).ClosedAtTime);
        Assert.Equal(10, (await db.Lot.SingleAsync()).Quantity);
        Assert.Equal(before + 4, await db.OutboxMessages.CountAsync());
        fail = false;
        await worker.ProcessDueAsync(default);
        db.ChangeTracker.Clear();
        Assert.Equal(2, await db.Auction.CountAsync(a => a.ClosedAtTime != null));
        Assert.Equal(6, (await db.Lot.SingleAsync()).Quantity);
        Assert.Equal(before + 8, await db.OutboxMessages.CountAsync());
    }

    [PostgresStockFact]
    public async Task AutomaticCloseAndQueuedBidRecheckDeadlineAfterWaitingForWriteLock()
    {
        var scheduled = await ScheduledAuctionAsync();
        var clock = new MutableClock(Now.AddHours(1));
        await using var locked = Db();
        await using var closing = Db();
        await using (var session = await Sessions(locked).BeginAsync(scheduled.AuctionId, default))
        {
            var close = new AuctionLifeCycleStore(closing, clock, new TestOutbox(closing), Sessions(closing))
                .CloseDueAsync(scheduled.AuctionId, default);
            var auction = await locked.Auction.SingleAsync();
            auction.EndedAt = Now.AddHours(2); // Deadline changed after discovery, while the close waits.
            await session.CommitAsync(default);
            Assert.False(await close);
        }
        Assert.Null((await closing.Auction.AsNoTracking().SingleAsync()).ClosedAtTime);
        clock.Now = Now.AddHours(2).AddMilliseconds(-1);
        await using var bidding = Db();
        await using (var session = await Sessions(locked).BeginAsync(scheduled.AuctionId, default))
        {
            var bid = new AuctionLotBiddingStore(bidding, clock, new TestOutbox(bidding)).PlaceBidAsync(
                new(scheduled.LineId, scheduled.CustomerId, null, Guid.NewGuid(), 5), default);
            clock.Now = Now.AddHours(2);
            await session.CommitAsync(default);
            Assert.Equal("BiddingClosed", (await bid).ErrorCode);
        }
        Assert.Empty(await bidding.Bids.ToListAsync());
    }

    [PostgresStockFact]
    public async Task AutomaticCloseHandlesSoldUnsoldAndPreviouslyClosedLinesInOneAuction()
    {
        var scheduled = await ScheduledAuctionAsync();
        await using var db = Db();
        var auction = await db.Auction.SingleAsync();
        var extraIds = new List<Guid>();
        for (var i = 0; i < 2; i++)
        {
            var stock = new Lot { LotId = Guid.NewGuid(), AuctionHouseId = houseId,
                Name = "Extra", Category = "Test", Description = "Test", Quantity = 3, EstimatedValue = 1 };
            var line = new AuctionLot { AuctionLotId = Guid.NewGuid(), AuctionId = scheduled.AuctionId,
                Auction = auction, LotId = stock.LotId, Lot = stock, Quantity = 3, StartingPrice = 1, OpenForBids = true };
            db.AuctionLot.Add(line);
            extraIds.Add(line.AuctionLotId);
        }
        await db.SaveChangesAsync();
        var bids = new AuctionLotBiddingStore(db, Clock, new TestOutbox(db));
        var accepted = await bids.PlaceBidAsync(new(scheduled.LineId, scheduled.CustomerId, null, Guid.NewGuid(), 5), default);
        Assert.True(accepted.Accepted);
        await bids.CloseAuctionLotAsync(extraIds[0], default);
        int before = await db.OutboxMessages.CountAsync();
        var clock = new MutableClock(Now.AddDays(1));
        await using var services = ExpirationServices(clock);
        using var worker = Worker(services, clock);
        await worker.ProcessDueAsync(default);
        db.ChangeTracker.Clear();
        Assert.Equal(before + 6, await db.OutboxMessages.CountAsync()); // Two remaining lines and auction, with revisions.
        var lines = await db.AuctionLot.ToListAsync();
        Assert.All(lines, line => { Assert.False(line.OpenForBids); Assert.NotNull(line.ClosedAtTime); });
        Assert.Equal(accepted.BidId, lines.Single(l => l.AuctionLotId == scheduled.LineId).CurrentHighestBidId);
        Assert.Equal(Now, lines.Single(l => l.AuctionLotId == extraIds[0]).ClosedAtTime);
        Assert.Equal(6, (await db.Lot.SingleAsync(l => l.LotId == lotId)).Quantity);
        Assert.All(await db.Lot.Where(l => l.LotId != lotId).ToListAsync(), l => Assert.Equal(3, l.Quantity));
        Assert.Empty(await new EFAuctionRepository(db).GetReservedQuantitiesAsync(null, default));
    }

    private sealed class MutableClock(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class ConditionalOutbox(AHDBContext db, Func<IIntegrationEvent, bool> fail) : IOutboxWriter
    {
        public Task AddAsync(IIntegrationEvent message, CancellationToken cancellationToken = default) =>
            fail(message) ? throw new InvalidOperationException("Temporary test failure") : new TestOutbox(db).AddAsync(message, cancellationToken);
    }

    private sealed class FailingOutbox : IOutboxWriter
    {
        public Task AddAsync(IIntegrationEvent message, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated outbox failure");
    }

    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
    private sealed class TestOutbox(AHDBContext db) : IOutboxWriter
    {
        public Task AddAsync(IIntegrationEvent message, CancellationToken cancellationToken = default)
        {
            db.OutboxMessages.Add(new OutboxMessage { OutboxId = message.EventId, EventType = message.GetType().Name,
                Payload = "{}", OccurredAtTime = message.OccurredAt });
            return Task.CompletedTask;
        }
    }
}
