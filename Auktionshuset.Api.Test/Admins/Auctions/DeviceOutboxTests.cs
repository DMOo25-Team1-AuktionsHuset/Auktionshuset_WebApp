using System.Text.Json;
using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public sealed class DeviceOutboxTests
{
    [Fact]
    public async Task EventsFollowTransitionsAndRepeatedReleaseDoesNotEmitAgain()
    {
        await using var f = await Fixture.CreateAsync();
        var repo = new PostgresDeviceRepository(f.Db, TimeProvider.System, new TestOutbox(f.Db));
        await repo.AddAsync(f.Device, f.Credential, default);
        var issued = await repo.IssueAsync(f.Device.DeviceId, f.Auction.AuctionId, f.Customer.CustomerId, default);
        Assert.Equal(DeviceAssignmentStatus.Success, issued.Status);
        Assert.Equal(DeviceAssignmentStatus.DeviceAlreadyAssigned, (await repo.IssueAsync(f.Device.DeviceId, f.Auction.AuctionId, f.Customer.CustomerId, default)).Status);
        var sessionId = Assert.IsType<Guid>(issued.SessionId);
        await repo.ReleaseAsync(f.Device.DeviceId, f.Auction.AuctionId, sessionId, default);
        await repo.ReleaseAsync(f.Device.DeviceId, f.Auction.AuctionId, sessionId, default);
        var messages = await f.Db.OutboxMessages.AsNoTracking().ToListAsync();
        Assert.Equal(3, messages.Count);
        Assert.Single(messages, m => m.EventType == nameof(DeviceRegisteredIntegrationEvent));
        Assert.Single(messages, m => m.EventType == nameof(DeviceIssuedIntegrationEvent));
        Assert.Single(messages, m => m.EventType == nameof(DeviceReleasedIntegrationEvent));
        Assert.All(messages, m => { Assert.DoesNotContain("secret", m.Payload, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("hash-value", m.Payload); });
    }

    [Fact]
    public async Task SessionRevisionIncreasesOnlyForCommittedTransitions()
    {
        await using var f = await Fixture.CreateAsync();
        var repo = new PostgresDeviceRepository(f.Db, TimeProvider.System, new TestOutbox(f.Db));
        await repo.AddAsync(f.Device, f.Credential, default);
        var issued = await repo.IssueAsync(f.Device.DeviceId, f.Auction.AuctionId, f.Customer.CustomerId, default);
        var sessionId = issued.SessionId!.Value;
        Assert.Equal(1, (await repo.GetActiveSessionAsync(f.Device.DeviceId, sessionId, default))!.SessionRevision);
        await repo.ReleaseAsync(f.Device.DeviceId, f.Auction.AuctionId, sessionId, default);
        await repo.ReleaseAsync(f.Device.DeviceId, f.Auction.AuctionId, sessionId, default);
        Assert.Equal(2, (await repo.GetCredentialAsync(f.Device.DeviceId, default))!.SessionRevision);
        var next = await repo.IssueAsync(f.Device.DeviceId, f.Auction.AuctionId, f.Customer.CustomerId, default);
        Assert.Equal(3, (await repo.GetActiveSessionAsync(f.Device.DeviceId, next.SessionId, default))!.SessionRevision);
        await repo.DeactivateAsync(f.Device.DeviceId, default);
        await repo.DeactivateAsync(f.Device.DeviceId, default);
        Assert.Equal(4, (await repo.GetCredentialAsync(f.Device.DeviceId, default))!.SessionRevision);
    }

    [Fact]
    public async Task OutboxFailureRollsBackRegistrationIssueAndRelease()
    {
        await using var f = await Fixture.CreateAsync();
        var failing = new PostgresDeviceRepository(f.Db, TimeProvider.System, new TestOutbox(f.Db, true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.AddAsync(f.Device, f.Credential, default));
        f.Db.ChangeTracker.Clear();
        Assert.Empty(await f.Db.Set<Device>().ToListAsync());
        var repo = new PostgresDeviceRepository(f.Db, TimeProvider.System, new TestOutbox(f.Db));
        await repo.AddAsync(f.Device, f.Credential, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.IssueAsync(f.Device.DeviceId, f.Auction.AuctionId, f.Customer.CustomerId, default));
        f.Db.ChangeTracker.Clear();
        Assert.Empty(await f.Db.DeviceSessions.ToListAsync());
        Assert.Empty(await f.Db.AuctionRegistrations.ToListAsync());
        Assert.Equal(0, (await repo.GetCredentialAsync(f.Device.DeviceId, default))!.SessionRevision);
        var issued = await repo.IssueAsync(f.Device.DeviceId, f.Auction.AuctionId, f.Customer.CustomerId, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.ReleaseAsync(f.Device.DeviceId, f.Auction.AuctionId, issued.SessionId!.Value, default));
        f.Db.ChangeTracker.Clear();
        Assert.Null((await f.Db.DeviceSessions.SingleAsync()).EndedAt);
        Assert.Equal(1, (await repo.GetCredentialAsync(f.Device.DeviceId, default))!.SessionRevision);
        Assert.Equal(2, await f.Db.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task DeactivationPublishesReleaseOnceAndRollsBackOnOutboxFailure()
    {
        await using var f = await Fixture.CreateAsync();
        var repo = new PostgresDeviceRepository(f.Db, TimeProvider.System, new TestOutbox(f.Db));
        await repo.AddAsync(f.Device, f.Credential, default);
        var issued = await repo.IssueAsync(f.Device.DeviceId, f.Auction.AuctionId, f.Customer.CustomerId, default);
        var failing = new PostgresDeviceRepository(f.Db, TimeProvider.System, new TestOutbox(f.Db, true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.DeactivateAsync(f.Device.DeviceId, default));
        f.Db.ChangeTracker.Clear();
        Assert.Null((await f.Db.DeviceSessions.SingleAsync()).EndedAt);
        Assert.True((await f.Db.DeviceCredentials.SingleAsync()).IsEnabled);
        Assert.Equal(1, (await repo.GetCredentialAsync(f.Device.DeviceId, default))!.SessionRevision);

        await repo.DeactivateAsync(f.Device.DeviceId, default);
        await repo.DeactivateAsync(f.Device.DeviceId, default);
        var message = await f.Db.OutboxMessages.AsNoTracking().SingleAsync(m => m.EventType == nameof(DeviceReleasedIntegrationEvent));
        var released = JsonSerializer.Deserialize<DeviceReleasedIntegrationEvent>(message.Payload)!;
        Assert.Equal(issued.SessionId, released.SessionId);
        Assert.Equal(f.Device.DeviceId, released.DeviceId);
    }

    private sealed class TestOutbox(AHDBContext db, bool fail = false) : IOutboxWriter
    {
        public Task AddAsync(IIntegrationEvent message, CancellationToken cancellationToken = default)
        {
            if (fail) throw new InvalidOperationException("Outbox failed");
            db.OutboxMessages.Add(new OutboxMessage { OutboxId = message.EventId, EventType = message.GetType().Name,
                Payload = JsonSerializer.Serialize(message, message.GetType()), OccurredAtTime = message.OccurredAt });
            return Task.CompletedTask;
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public AHDBContext Db { get; private set; } = null!;
        public Device Device { get; private set; } = null!;
        public DeviceCredential Credential { get; private set; } = null!;
        public Auction Auction { get; private set; } = null!;
        public Customer Customer { get; private set; } = null!;
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture();
            await f.connection.OpenAsync();
            f.Db = new AHDBContext(new DbContextOptionsBuilder<AHDBContext>().UseSqlite(f.connection).Options);
            await f.Db.Database.EnsureCreatedAsync();
            var house = new AuctionHouse { AuctionHouseId = Guid.NewGuid(), AuctionHouseName = "Test", Address = "Test", CVRNumber = 12345678, PhoneNumber = "12345678", Email = "test@example.test" };
            f.Auction = new Auction { AuctionId = Guid.NewGuid(), AuctionHouseId = house.AuctionHouseId, Name = "Test", AuctionStatus = "Kommende", StartsAt = DateTime.UtcNow };
            f.Customer = new Customer { CustomerId = Guid.NewGuid(), FirstName = "Test", LastName = "Test", Email = "customer@example.test", Address = "Test", PhoneNumber = "12345678", BirthDate = new(1990, 1, 1) };
            f.Device = new Device { DeviceId = Guid.NewGuid(), AuctionHouseId = house.AuctionHouseId, DeviceNumber = 1, Status = "Available" };
            f.Credential = new DeviceCredential { DeviceId = f.Device.DeviceId, SecretHash = "hash-value", IsEnabled = true, CredentialVersion = 1 };
            f.Db.AddRange(house, f.Auction, f.Customer);
            await f.Db.SaveChangesAsync();
            f.Db.ChangeTracker.Clear();
            return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
