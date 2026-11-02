using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Auktionshuset.Api.Endpoints.Admin.Auction.CloseAuction;
using Auktionshuset.Api.Endpoints.Admin.Auction.Lifecycle;
using Auktionshuset.Api.Security;
using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Admin.Auctions.UpdateAuction;
using Auktionshuset.Application.Admin.Auctions.CloseAuction;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Application.Auction;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Repositories;
using Auktionshuset.Infrastructure.Service.Auctions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public class ManualAuctionTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("same")]
    [InlineData("replace")]
    [InlineData("remove")]
    [InlineData("assign")]
    public async Task UpdateUpcomingAuctionPersistsWithoutDuplicateEmployeeTracking(string operation)
    {
        await using var fixture = await Fixture.CreateAsync(startMinutes: 60);
        Employee CreateEmployee(string name) => new()
        {
            EmployeeId = Guid.NewGuid(), AuctionHouseId = fixture.Auction.AuctionHouseId,
            FirstName = name, LastName = "Test", Address = "Test", BirthDate = new DateOnly(1990, 1, 1)
        };
        var original = CreateEmployee("Original");
        var replacement = CreateEmployee("Replacement");
        fixture.Db.Employee.AddRange(original, replacement);
        fixture.Auction.EmployeeId = operation == "assign" ? null : original.EmployeeId;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear(); // Match a fresh update request against persisted data.

        Guid? selectedId = operation switch
        {
            "remove" => null,
            "replace" or "assign" => replacement.EmployeeId,
            _ => original.EmployeeId
        };
        var handler = new UpdateAuctionHandler(new EFAuctionRepository(fixture.Db),
            new EFLotRepo(fixture.Db), new EFEmployeeRepository(fixture.Db),
            fixture.Outbox, new Sessions(fixture.Db), new FixedClock());
        var result = await handler.HandleAsync(new UpdateAuctionCommand(
            fixture.Auction.AuctionId, "Updated auction", Now.AddHours(2), Now.AddHours(3),
            selectedId, fixture.Auction.AuctionHouseId,
            [new AuctionLotSelection(fixture.Item.LotId, 1, 125m)]), default);

        Assert.True(result.Succeeded);
        Assert.Single(fixture.Outbox.Events.OfType<AuctionUpdatedIntegrationEvent>());
        Assert.Single(fixture.Outbox.Events.OfType<AuctionRevisionChangedIntegrationEvent>());
        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.Auction.Include(a => a.Employee).SingleAsync();
        Assert.Equal("Updated auction", saved.Name);
        Assert.Equal(selectedId, saved.EmployeeId);
        Assert.Equal(selectedId, saved.Employee?.EmployeeId);
        Assert.Equal(125m, (await fixture.Db.AuctionLot.SingleAsync()).StartingPrice);
        Assert.Equal(2, await fixture.Db.Employee.CountAsync());
        Assert.Equal("Original", (await fixture.Db.Employee.FindAsync(original.EmployeeId))!.FirstName);
        Assert.Equal("Replacement", (await fixture.Db.Employee.FindAsync(replacement.EmployeeId))!.FirstName);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("close")]
    [InlineData("lot")]
    [InlineData("selection")]
    public async Task OperationsRequireAuctionUpdatePermission(string operation)
    {
        using var host = await new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(services =>
            {
                services.AddRouting();
                var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:SigningKey"] = "Manual-auction-tests-signing-key-at-least-32-bytes",
                    ["Authentication:Issuer"] = "test", ["Authentication:Audience"] = "test",
                    ["Authentication:Users:0:Email"] = "admin@example.test",
                    ["Authentication:Users:0:Password"] = "test-password",
                    ["Authentication:Users:0:Role"] = "Admin",
                    ["Authentication:Users:1:Email"] = "auctionadmin@example.test",
                    ["Authentication:Users:1:Password"] = "test-password",
                    ["Authentication:Users:1:Role"] = "AuctionAdmin",
                    ["Authentication:Users:2:Email"] = "lotadmin@example.test",
                    ["Authentication:Users:2:Password"] = "test-password",
                    ["Authentication:Users:2:Role"] = "LotAdmin",
                    ["Authentication:Users:3:Email"] = "customer@example.test",
                    ["Authentication:Users:3:Password"] = "test-password",
                    ["Authentication:Users:3:Role"] = "Customer"
                }).Build();
                services.AddSingleton<IConfiguration>(configuration);
                services.AddSecurityServices(configuration);
                services.AddSingleton<IAuctionLifeCycleStore, MissingAuctionStore>();
                services.AddSingleton<ICloseAuctionLotStore, MissingLotStore>();
                services.AddSingleton<ISharedAuctionLotStore, MissingSharedLotStore>();
                services.AddTransient<CloseAuctionLotHandler>();
            });
            web.Configure(app =>
            {
                app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGroup("/api/auctions").MapAuctionLifecycle();
                    endpoints.MapCloseAuctionLotEndpoint();
                });
            });
        }).StartAsync();
        using var client = host.GetTestClient();
        string path = operation == "lot" ? $"/api/auction-lots/{Guid.NewGuid()}" : $"/api/auctions/{Guid.NewGuid()}/{(operation == "selection" ? "shared-lot" : operation)}";
        Task<HttpResponseMessage> SendAsync() => operation == "selection"
            ? client.PutAsJsonAsync(path, new { AuctionLotId = (Guid?)null }) : client.PostAsync(path, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync()).StatusCode);
        foreach (var role in new[] { SecurityRoles.Customer, SecurityRoles.LotAdmin, SecurityRoles.AuctionAdmin, SecurityRoles.Admin })
        {
            var user = await host.Services.GetRequiredService<ConfiguredAuthUserStore>().FindByEmailAsync($"{role.ToLowerInvariant()}@example.test", default);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Services.GetRequiredService<IAccessTokenService>().Issue(user!).Value);
            Assert.Equal(role is SecurityRoles.Admin or SecurityRoles.AuctionAdmin ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden,
                (await SendAsync()).StatusCode);
        }
        var configuredReader = (await host.Services.GetRequiredService<ConfiguredAuthUserStore>().FindByEmailAsync("auctionadmin@example.test", default))!;
        var reader = new AuthUser { UserId = configuredReader.UserId, Email = configuredReader.Email, PasswordHash = "",
            Roles = [SecurityRoles.AuctionAdmin], Permissions = [SecurityPermissions.ViewAuctions] };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Services.GetRequiredService<IAccessTokenService>().Issue(reader).Value);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync()).StatusCode);
    }

    [Theory]
    [InlineData(1, null)] // Before planned start.
    [InlineData(-60, 0)] // At planned end.
    [InlineData(-60, -1)]
    public async Task StartOutsidePlannedWindowIsRejected(int startMinutes, int? endMinutes)
    {
        await using var fixture = await Fixture.CreateAsync(startMinutes, endMinutes);
        await Assert.ThrowsAsync<AuctionStateConflictException>(() => fixture.Lifecycle.StartAsync(fixture.Auction.AuctionId, default));
        Assert.Null(fixture.Auction.StartedAtTime);
        Assert.False(fixture.Item.OpenForBids);
        Assert.Empty(fixture.Outbox.Events);
    }

    [Theory]
    [InlineData(0, null)] // Exact start, no planned end.
    [InlineData(-60, 60)]
    public async Task StartAndCloseAreIdempotentAndKeepPlannedEnd(int startMinutes, int? endMinutes)
    {
        await using var fixture = await Fixture.CreateAsync(startMinutes, endMinutes);
        var id = fixture.Auction.AuctionId;
        var plannedEnd = fixture.Auction.EndedAt;
        Assert.True(await fixture.Lifecycle.StartAsync(id, default));
        Assert.True(await fixture.Lifecycle.StartAsync(id, default));
        Assert.Equal(Now, fixture.Auction.StartedAtTime);
        Assert.True(fixture.Item.OpenForBids);
        Assert.Single(fixture.Outbox.Events.OfType<AuctionUpdatedIntegrationEvent>());
        Assert.True(await fixture.Lifecycle.CloseAsync(id, default));
        Assert.True(await fixture.Lifecycle.CloseAsync(id, default));
        Assert.True(await fixture.Lifecycle.StartAsync(id, default)); // A retry never reopens a closed auction.
        Assert.Equal(AuctionStatuses.Ended, fixture.Auction.AuctionStatus);
        Assert.Equal(Now, fixture.Auction.ClosedAtTime);
        Assert.Equal(plannedEnd, fixture.Auction.EndedAt);
        Assert.False(fixture.Item.OpenForBids);
        Assert.Equal(Now, fixture.Item.ClosedAtTime);
        Assert.Equal(3, fixture.Outbox.Events.Count(e => e is not AuctionRevisionChangedIntegrationEvent));
        Assert.Equal(3, fixture.Outbox.Events.OfType<AuctionRevisionChangedIntegrationEvent>().Count());
    }

    [Fact]
    public async Task UnstartedCloseAndEmptyStartAreRejectedAndMissingAuctionReturnsFalse()
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<AuctionStateConflictException>(() => fixture.Lifecycle.CloseAsync(fixture.Auction.AuctionId, default));
        fixture.Db.AuctionLot.Remove(fixture.Item);
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<AuctionStateConflictException>(() => fixture.Lifecycle.StartAsync(fixture.Auction.AuctionId, default));
        Assert.False(await fixture.Lifecycle.StartAsync(Guid.NewGuid(), default));
        Assert.False(await fixture.Lifecycle.CloseAsync(Guid.NewGuid(), default));
        Assert.Empty(fixture.Outbox.Events);
    }

    [Fact]
    public async Task SingleLotCloseRequiresLiveAuctionAndRepeatedCallsDoNotPublishAgain()
    {
        await using var fixture = await Fixture.CreateAsync();
        var store = new AuctionLotBiddingStore(fixture.Db, new FixedClock(), fixture.Outbox);
        await Assert.ThrowsAsync<AuctionStateConflictException>(() => store.CloseAuctionLotAsync(fixture.Item.AuctionLotId, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.CloseAuctionLotAsync(Guid.NewGuid(), default));
        await fixture.Lifecycle.StartAsync(fixture.Auction.AuctionId, default);
        Assert.Null(await store.CloseAuctionLotAsync(fixture.Item.AuctionLotId, default));
        Assert.Null(await store.CloseAuctionLotAsync(fixture.Item.AuctionLotId, default));
        Assert.Equal(2, fixture.Outbox.Events.Count(e => e is not AuctionRevisionChangedIntegrationEvent));
        Assert.False(fixture.Item.OpenForBids);
        Assert.Equal(Now, fixture.Item.ClosedAtTime);
        await fixture.Lifecycle.CloseAsync(fixture.Auction.AuctionId, default);
        Assert.Null(await store.CloseAuctionLotAsync(fixture.Item.AuctionLotId, default));
        Assert.Equal(3, fixture.Outbox.Events.Count(e => e is not AuctionRevisionChangedIntegrationEvent));
    }

    [Theory]
    [InlineData(60, "Kommende")]
    [InlineData(-60, "Kommende")]
    [InlineData(-60, "Live")] // A status value alone cannot replace actual start.
    public async Task RegisteredCustomerCannotBidUntilActualStart(int startMinutes, string status)
    {
        await using var fixture = await Fixture.CreateAsync(startMinutes);
        var customer = new Customer { CustomerId = Guid.NewGuid(), FirstName = "Anna", LastName = "Kunde",
            Email = "anna@example.test", PhoneNumber = "12345678", Address = "Test", BirthDate = new DateOnly(1990, 1, 1) };
        fixture.Db.AuctionRegistrations.Add(new AuctionRegistration { AuctionId = fixture.Auction.AuctionId,
            CustomerId = customer.CustomerId, Customer = customer, RegisteredAt = Now });
        fixture.Auction.AuctionStatus = status;
        fixture.Item.OpenForBids = true; // Even an incorrectly open lot must be protected by auction state.
        await fixture.Db.SaveChangesAsync();
        var store = new AuctionLotBiddingStore(fixture.Db, new FixedClock(), fixture.Outbox);
        var result = await store.PlaceBidAsync(new PlaceBidCommand(fixture.Item.AuctionLotId, customer.CustomerId,
            null, Guid.NewGuid(), 110), default);
        Assert.False(result.Accepted);
        Assert.Equal("BiddingClosed", result.ErrorCode);
        Assert.Empty(await fixture.Db.Bids.ToListAsync());
        Assert.Empty(fixture.Outbox.Events);
        if (startMinutes <= 0 && status == AuctionStatuses.Upcoming)
        {
            await fixture.Lifecycle.StartAsync(fixture.Auction.AuctionId, default);
            Assert.Single(await fixture.Db.AuctionRegistrations.ToListAsync());
            var accepted = await store.PlaceBidAsync(new PlaceBidCommand(fixture.Item.AuctionLotId, customer.CustomerId,
                null, Guid.NewGuid(), 110), default);
            Assert.True(accepted.Accepted);
            Assert.Equal(customer.CustomerId, (await fixture.Db.Bids.SingleAsync()).CustomerId);
        }
    }

    [Fact]
    public async Task DeviceBidsRequireCurrentRevisionAndSelectedLotButWebAndRetriesRemainSupported()
    {
        await using var fixture = await Fixture.CreateAsync();
        var customer = new Customer { CustomerId = Guid.NewGuid(), FirstName = "Anna", LastName = "Kunde",
            Email = "device-bid@example.test", PhoneNumber = "12345678", Address = "Test", BirthDate = new DateOnly(1990, 1, 1) };
        var device = new Device { DeviceId = Guid.NewGuid(), AuctionHouseId = fixture.Auction.AuctionHouseId,
            DeviceNumber = 1, Status = "Available" };
        var deviceSession = new DeviceSession { DeviceSessionId = Guid.NewGuid(), DeviceId = device.DeviceId, Device = device,
            CustomerId = customer.CustomerId, Customer = customer, AuctionId = fixture.Auction.AuctionId,
            StartedAt = Now.AddMinutes(-1), EndedAt = null };
        fixture.Db.DeviceSessions.Add(deviceSession);
        fixture.Db.DeviceCredentials.Add(new DeviceCredential { DeviceId = device.DeviceId,
            SecretHash = new Auktionshuset.Infrastructure.Service.Auth.DeviceSecretService().HashSecret(new string('a', 64)), IsEnabled = true, CredentialVersion = 1 });
        var otherLot = new Lot { LotId = Guid.NewGuid(), AuctionHouseId = fixture.Auction.AuctionHouseId,
            Name = "Bord", Category = "Møbler", Description = "Test", Quantity = 1, EstimatedValue = 100 };
        var otherLine = new AuctionLot { AuctionLotId = Guid.NewGuid(), AuctionId = fixture.Auction.AuctionId,
            Auction = fixture.Auction, LotId = otherLot.LotId, Lot = otherLot, Quantity = 1, StartingPrice = 100 };
        fixture.Db.AuctionLot.Add(otherLine);
        fixture.Db.AuctionRegistrations.Add(new AuctionRegistration { AuctionId = fixture.Auction.AuctionId,
            CustomerId = customer.CustomerId, Customer = customer, RegisteredAt = Now });
        await fixture.Db.SaveChangesAsync();
        await fixture.Lifecycle.StartAsync(fixture.Auction.AuctionId, default);
        var bids = new AuctionLotBiddingStore(fixture.Db, new FixedClock(), fixture.Outbox);
        var select = new SharedAuctionLotStore(fixture.Db, new Sessions(fixture.Db), fixture.Outbox, new FixedClock());
        var missing = new PlaceBidCommand(fixture.Item.AuctionLotId, customer.CustomerId, deviceSession.DeviceSessionId, Guid.NewGuid(), 110,
                    DeviceId: device.DeviceId, CredentialVersion: 1);
        Assert.Equal("AuctionRevisionRequired", (await bids.PlaceBidAsync(missing, default)).ErrorCode);
        Assert.True((await bids.PlaceBidAsync(missing, default)).Duplicate);
        Assert.Equal("RequestIdReuseWithDifferentPayload", (await bids.PlaceBidAsync(missing with { AuctionRevision = 1 }, default)).ErrorCode);
        Assert.Equal("AuctionContextChanged", (await bids.PlaceBidAsync(missing with { RequestId = Guid.NewGuid(), AuctionRevision = 1 }, default)).ErrorCode);
        Assert.Empty(await fixture.Db.Bids.ToListAsync());

        var selected = await select.SelectAsync(fixture.Auction.AuctionId, fixture.Item.AuctionLotId, default);
        var command = missing with { RequestId = Guid.NewGuid(), AuctionRevision = selected!.AuctionRevision };
        var accepted = await bids.PlaceBidAsync(command, default);
        Assert.True(accepted.Accepted);
        var retry = await bids.PlaceBidAsync(command, default);
        Assert.True(retry.Accepted);
        Assert.True(retry.Duplicate);
        Assert.Equal(accepted.BidId, retry.BidId);
        Assert.Equal("RequestIdReuseWithDifferentPayload", (await bids.PlaceBidAsync(command with { AuctionRevision = selected.AuctionRevision + 1 }, default)).ErrorCode);
        Assert.Equal("AuctionContextChanged", (await bids.PlaceBidAsync(command with { RequestId = Guid.NewGuid(), Amount = 120 }, default)).ErrorCode);

        // Simulate a revision committed by another context while this context still tracks the old auction.
        await fixture.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Auction\" SET \"AuctionRevision\" = \"AuctionRevision\" + 1 WHERE \"AuctionId\" = {fixture.Auction.AuctionId}");
        Assert.Equal("AuctionContextChanged", (await bids.PlaceBidAsync(command with { RequestId = Guid.NewGuid(), Amount = 120, AuctionRevision = selected.AuctionRevision + 1 }, default)).ErrorCode);

        var switched = await select.SelectAsync(fixture.Auction.AuctionId, otherLine.AuctionLotId, default);
        Assert.Equal("AuctionContextChanged", (await bids.PlaceBidAsync(command with { RequestId = Guid.NewGuid(), Amount = 120, AuctionRevision = switched!.AuctionRevision }, default)).ErrorCode);
        var cleared = await select.SelectAsync(fixture.Auction.AuctionId, null, default);
        Assert.Equal("AuctionContextChanged", (await bids.PlaceBidAsync(command with { RequestId = Guid.NewGuid(), Amount = 120, AuctionRevision = cleared!.AuctionRevision }, default)).ErrorCode);
        Assert.Single(await fixture.Db.Bids.ToListAsync());
        var web = await bids.PlaceBidAsync(new PlaceBidCommand(fixture.Item.AuctionLotId, customer.CustomerId, null, Guid.NewGuid(), 120), default);
        Assert.True(web.Accepted);
        Assert.Equal(2, await fixture.Db.Bids.CountAsync());
    }

    [Fact]
    public async Task SharedSelectionValidatesAndClearsOnLotClose()
    {
        await using var fixture = await Fixture.CreateAsync();
        var selection = new SharedAuctionLotStore(fixture.Db, new Sessions(fixture.Db), fixture.Outbox, new FixedClock());
        Guid id = fixture.Auction.AuctionId;
        Assert.Null(await selection.GetAsync(Guid.NewGuid(), default));
        await Assert.ThrowsAsync<AuctionStateConflictException>(() => selection.SelectAsync(id, fixture.Item.AuctionLotId, default));
        await fixture.Lifecycle.StartAsync(id, default);
        await Assert.ThrowsAsync<AuctionStateConflictException>(() => selection.SelectAsync(id, Guid.NewGuid(), default));
        var otherAuction = new Auction { AuctionId = Guid.NewGuid(), AuctionHouseId = fixture.Auction.AuctionHouseId,
            Name = "Other", AuctionStatus = AuctionStatuses.Live, StartsAt = Now.AddHours(-1), StartedAtTime = Now };
        var otherLine = new AuctionLot { AuctionLotId = Guid.NewGuid(), AuctionId = otherAuction.AuctionId,
            Auction = otherAuction, LotId = fixture.Item.LotId, Quantity = 1, OpenForBids = true };
        fixture.Db.AuctionLot.Add(otherLine);
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<AuctionStateConflictException>(() => selection.SelectAsync(id, otherLine.AuctionLotId, default));
        var selected = await selection.SelectAsync(id, fixture.Item.AuctionLotId, default);
        Assert.Equal(2, selected!.AuctionRevision);
        Assert.Equal(fixture.Item.AuctionLotId, selected.AuctionLotId);
        Assert.Equal(selected, await selection.SelectAsync(id, fixture.Item.AuctionLotId, default));
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(selected, await selection.GetAsync(id, default));
        var closer = new AuctionLotBiddingStore(fixture.Db, new FixedClock(), fixture.Outbox);
        await closer.CloseAuctionLotAsync(fixture.Item.AuctionLotId, default);
        var cleared = await selection.GetAsync(id, default);
        Assert.Null(cleared!.AuctionLotId);
        Assert.Equal(3, cleared.AuctionRevision);
        Assert.Equal(new long[] { 1, 2, 3 }, fixture.Outbox.Events.OfType<AuctionRevisionChangedIntegrationEvent>().Select(e => e.AuctionRevision));
    }

    [Fact]
    public async Task SelectionCanSwitchAndClearAndAuctionCloseClearsWithoutAdvancing()
    {
        await using var fixture = await Fixture.CreateAsync();
        var secondLot = new Lot { LotId = Guid.NewGuid(), AuctionHouseId = fixture.Auction.AuctionHouseId,
            Name = "Bord", Category = "Møbler", Description = "Test", Quantity = 1, EstimatedValue = 100 };
        var secondLine = new AuctionLot { AuctionLotId = Guid.NewGuid(), AuctionId = fixture.Auction.AuctionId,
            Auction = fixture.Auction, LotId = secondLot.LotId, Lot = secondLot, Quantity = 1, StartingPrice = 100 };
        fixture.Db.AuctionLot.Add(secondLine);
        await fixture.Db.SaveChangesAsync();
        await fixture.Lifecycle.StartAsync(fixture.Auction.AuctionId, default);
        var selection = new SharedAuctionLotStore(fixture.Db, new Sessions(fixture.Db), fixture.Outbox, new FixedClock());
        Guid id = fixture.Auction.AuctionId;
        await selection.SelectAsync(id, fixture.Item.AuctionLotId, default);
        Assert.Equal(secondLine.AuctionLotId, (await selection.SelectAsync(id, secondLine.AuctionLotId, default))!.AuctionLotId);
        Assert.Null((await selection.SelectAsync(id, null, default))!.AuctionLotId);
        await selection.SelectAsync(id, fixture.Item.AuctionLotId, default);
        await fixture.Lifecycle.CloseAsync(id, default);
        fixture.Db.ChangeTracker.Clear();
        var closed = await selection.GetAsync(id, default);
        Assert.Null(closed!.AuctionLotId);
        Assert.Equal(8, closed.AuctionRevision);
    }

    [Fact]
    public async Task FailedSelectionOutboxRollsBackSelectionAndRevision()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Lifecycle.StartAsync(fixture.Auction.AuctionId, default);
        var sessions = new PostgresAuctionWriteSessionFactory(fixture.Db, new EfUnitOfWork(fixture.Db));
        var selection = new SharedAuctionLotStore(fixture.Db, sessions, new FailingSelectionOutbox(), new FixedClock());
        await Assert.ThrowsAsync<InvalidOperationException>(() => selection.SelectAsync(fixture.Auction.AuctionId, fixture.Item.AuctionLotId, default));
        fixture.Db.ChangeTracker.Clear();
        var saved = await selection.GetAsync(fixture.Auction.AuctionId, default);
        Assert.Null(saved!.AuctionLotId);
        Assert.Equal(1, saved.AuctionRevision);
    }

    private sealed class FailingSelectionOutbox : IOutboxWriter
    {
        public Task AddAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Outbox failure");
    }

    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
    private sealed class Outbox : IOutboxWriter
    {
        public List<IIntegrationEvent> Events { get; } = [];
        public Task AddAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        { Events.Add(integrationEvent); return Task.CompletedTask; }
    }
    private sealed class Sessions(AHDBContext db) : IAuctionWriteSessionFactory
    {
        public Task<IAuctionWriteSession> BeginAsync(Guid id, CancellationToken ct) => Task.FromResult<IAuctionWriteSession>(new Session(db));
        private sealed class Session(AHDBContext db) : IAuctionWriteSession
        {
            public async Task CommitAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
    // SQLite verifies sequential state transitions, not PostgreSQL row-lock concurrency.
    private sealed class SqliteLocks : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        { command.CommandText = command.CommandText.Replace("FOR UPDATE", ""); return ValueTask.FromResult(result); }
    }
    private sealed class TestDb(DbContextOptions<AHDBContext> options) : AHDBContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            foreach (var property in builder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()).Where(p => p.ClrType == typeof(decimal)))
                property.SetValueConverter(new ValueConverter<decimal, double>(v => (double)v, v => (decimal)v));
        }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public AHDBContext Db { get; private set; } = null!;
        public Auction Auction { get; private set; } = null!;
        public AuctionLot Item { get; private set; } = null!;
        public Outbox Outbox { get; } = new();
        public AuctionLifeCycleStore Lifecycle => new(Db, new FixedClock(), Outbox, new Sessions(Db));
        public static async Task<Fixture> CreateAsync(int startMinutes = -60, int? endMinutes = null)
        {
            var f = new Fixture();
            await f.connection.OpenAsync();
            f.Db = new TestDb(new DbContextOptionsBuilder<AHDBContext>().UseSqlite(f.connection).AddInterceptors(new SqliteLocks()).Options);
            await f.Db.Database.EnsureCreatedAsync();
            var house = new AuctionHouse { AuctionHouseId = Guid.NewGuid(), AuctionHouseName = "Test", Address = "Test", CVRNumber = 12345678, PhoneNumber = "12345678", Email = "test@example.test" };
            f.Auction = new Auction { AuctionId = Guid.NewGuid(), AuctionHouseId = house.AuctionHouseId, AuctionHouse = house,
                Name = "Manual test", AuctionStatus = AuctionStatuses.Upcoming, StartsAt = Now.AddMinutes(startMinutes), EndedAt = endMinutes is int end ? Now.AddMinutes(end) : null };
            var lot = new Lot { LotId = Guid.NewGuid(), AuctionHouseId = house.AuctionHouseId, AuctionHouse = house,
                Name = "Stol", Category = "Møbler", Description = "Test", Quantity = 1, EstimatedValue = 100 };
            f.Item = new AuctionLot { AuctionLotId = Guid.NewGuid(), AuctionId = f.Auction.AuctionId, Auction = f.Auction,
                LotId = lot.LotId, Lot = lot, Quantity = 1, StartingPrice = 100 };
            f.Db.AuctionLot.Add(f.Item);
            await f.Db.SaveChangesAsync();
            return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed class MissingAuctionStore : IAuctionLifeCycleStore
    {
        public Task<bool> StartAsync(Guid id, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> CloseAsync(Guid id, CancellationToken ct) => Task.FromResult(false);
    }
    private sealed class MissingSharedLotStore : ISharedAuctionLotStore
    {
        public Task<SharedAuctionLotState?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult<SharedAuctionLotState?>(null);
        public Task<SharedAuctionLotState?> SelectAsync(Guid id, Guid? lotId, CancellationToken ct) => Task.FromResult<SharedAuctionLotState?>(null);
    }
    private sealed class MissingLotStore : ICloseAuctionLotStore
    { public Task<Guid?> CloseAuctionLotAsync(Guid id, CancellationToken ct) => throw new KeyNotFoundException(); }
}
