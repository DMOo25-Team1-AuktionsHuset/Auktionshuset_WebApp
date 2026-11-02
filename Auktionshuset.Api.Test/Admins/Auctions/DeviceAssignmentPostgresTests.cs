using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.Admin.Devices.AuthenticateDevice;
using Auktionshuset.Application.Devices.AuthenticateDevice;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Repositories;
using Auktionshuset.Infrastructure.Service.Auth;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public sealed partial class AuctionStockPostgresTests
{
    private async Task<(Guid Auction, Guid OtherAuction, Guid Customer, Guid OtherCustomer, Guid Device, Guid OtherDevice)> SeedAssignmentsAsync()
    {
        await using var db = Db();
        var auctions = Enumerable.Range(0, 2).Select(i => new Auction {
            AuctionId = Guid.NewGuid(), AuctionHouseId = houseId, Name = $"Device auction {i}",
            StartsAt = Now, AuctionStatus = "Scheduled" }).ToArray();
        var customers = Enumerable.Range(0, 2).Select(i => new Customer {
            CustomerId = Guid.NewGuid(), FirstName = "Test", LastName = "Customer",
            Email = $"{Guid.NewGuid()}@example.test", PhoneNumber = "12345678",
            Address = "Test", BirthDate = new(1990, 1, 1) }).ToArray();
        var secrets = new DeviceSecretService();
        var devices = Enumerable.Range(1, 2).Select(i => new Device {
            DeviceId = Guid.NewGuid(), AuctionHouseId = houseId, DeviceNumber = i, Status = "Active" }).ToArray();
        db.Auction.AddRange(auctions);
        db.Customers.AddRange(customers);
        db.AddRange(devices);
        db.DeviceCredentials.AddRange(devices.Select(d => new DeviceCredential {
            DeviceId = d.DeviceId, SecretHash = secrets.HashSecret(new string('a', 64)), IsEnabled = true }));
        await db.SaveChangesAsync();
        return (auctions[0].AuctionId, auctions[1].AuctionId, customers[0].CustomerId,
            customers[1].CustomerId, devices[0].DeviceId, devices[1].DeviceId);
    }

    private static PostgresDeviceRepository AssignmentRepo(AHDBContext db) => new(db, Clock, new TestOutbox(db));

    [PostgresStockFact]
    public async Task ConcurrentAssignmentsOfSameDeviceAcrossAuctionsAllowOnlyOne()
    {
        var data = await SeedAssignmentsAsync();
        await using var owner = Db();
        await using var first = Db();
        await using var second = Db();
        Task<DeviceAssignmentResult> a, b;
        await using (var held = await Sessions(owner).BeginAsync(Guid.Empty, default))
        {
            a = new IssueDeviceHandler(AssignmentRepo(first)).HandleAsync(new(data.Device, data.Auction, data.Customer), default);
            b = new IssueDeviceHandler(AssignmentRepo(second)).HandleAsync(new(data.Device, data.OtherAuction, data.OtherCustomer), default);
            await held.CommitAsync(default);
        }
        var results = await Task.WhenAll(a, b);
        Assert.Single(results, r => r.Status == DeviceAssignmentStatus.Success);
        Assert.Single(results, r => r.Status == DeviceAssignmentStatus.DeviceAlreadyAssigned);
        await using var check = Db();
        Assert.Equal(1, await check.DeviceSessions.CountAsync());
        Assert.Equal(1, await check.AuctionRegistrations.CountAsync());
    }

    [PostgresStockFact]
    public async Task ConcurrentAssignmentsToSameCustomerAllowOnlyOne()
    {
        var data = await SeedAssignmentsAsync();
        await using var owner = Db();
        await using var first = Db();
        await using var second = Db();
        Task<DeviceAssignmentResult> a, b;
        await using (var held = await Sessions(owner).BeginAsync(Guid.Empty, default))
        {
            a = new IssueDeviceHandler(AssignmentRepo(first)).HandleAsync(new(data.Device, data.Auction, data.Customer), default);
            b = new IssueDeviceHandler(AssignmentRepo(second)).HandleAsync(new(data.OtherDevice, data.Auction, data.Customer), default);
            await held.CommitAsync(default);
        }
        var results = await Task.WhenAll(a, b);
        Assert.Single(results, r => r.Status == DeviceAssignmentStatus.Success);
        Assert.Single(results, r => r.Status == DeviceAssignmentStatus.CustomerAlreadyAssigned);
        await using var check = Db();
        Assert.Equal(1, await check.DeviceSessions.CountAsync());
        Assert.Equal(1, await check.AuctionRegistrations.CountAsync());
    }

    [PostgresStockFact]
    public async Task ReleaseValidatesIdentityIsIdempotentAndAllowsReassignmentWithoutDuplicateRegistration()
    {
        var data = await SeedAssignmentsAsync();
        await using var db = Db();
        var repo = AssignmentRepo(db);
        var issue = new IssueDeviceHandler(repo);
        var release = new ReleaseDeviceHandler(repo);
        var initial = await issue.HandleAsync(new(data.Device, data.Auction, data.Customer), default);
        Assert.Equal(DeviceAssignmentStatus.Success, initial.Status);
        var session = Assert.IsType<Guid>(initial.SessionId);
        Assert.Equal(DeviceAssignmentStatus.DeviceAlreadyAssigned, (await issue.HandleAsync(new(data.Device, data.Auction, data.Customer), default)).Status);
        Assert.Equal(DeviceAssignmentStatus.CustomerAlreadyAssigned, (await issue.HandleAsync(new(data.OtherDevice, data.Auction, data.Customer), default)).Status);
        Assert.Equal(DeviceAssignmentStatus.SessionNotFound, (await release.HandleAsync(new(data.OtherDevice, data.Auction, session), default)).Status);
        Assert.Equal(DeviceAssignmentStatus.SessionNotFound, (await release.HandleAsync(new(data.Device, data.OtherAuction, session), default)).Status);
        Assert.Equal(DeviceAssignmentStatus.Success, (await release.HandleAsync(new(data.Device, data.Auction, session), default)).Status);
        Assert.Equal(DeviceAssignmentStatus.Success, (await release.HandleAsync(new(data.Device, data.Auction, session), default)).Status);
        var reassigned = await issue.HandleAsync(new(data.OtherDevice, data.Auction, data.Customer), default);
        Assert.Equal(DeviceAssignmentStatus.Success, reassigned.Status);
        Assert.NotEqual(initial.SessionId, reassigned.SessionId);
        await using var check = Db();
        Assert.Equal(Now, (await check.DeviceSessions.SingleAsync(s => s.DeviceSessionId == session)).EndedAt);
        Assert.Equal(2, await check.DeviceSessions.CountAsync());
        Assert.Equal(1, await check.AuctionRegistrations.CountAsync());
    }

    [PostgresStockFact]
    public async Task AuthenticationAndDeactivationPersistAndPreventFurtherIssuance()
    {
        var data = await SeedAssignmentsAsync();
        await using var db = Db();
        var repo = AssignmentRepo(db);
        var auth = new AuthenticateDeviceHandler(repo, new DeviceSecretService());
        Assert.Null(await auth.HandleAsync(new AuthenticateDeviceCommand { DeviceId = data.Device, Secret = new string('b', 64) }, default));
        Assert.Null(await auth.HandleAsync(new AuthenticateDeviceCommand { DeviceId = Guid.NewGuid(), Secret = new string('a', 64) }, default));
        Assert.NotNull(await auth.HandleAsync(new AuthenticateDeviceCommand { DeviceId = data.Device, Secret = new string('a', 64) }, default));
        var issued = await new IssueDeviceHandler(repo).HandleAsync(new(data.Device, data.Auction, data.Customer), default);
        Assert.Equal(DeviceAssignmentStatus.Success, issued.Status);
        Assert.Equal(DeviceAssignmentStatus.Success, (await new DeactivateDeviceHandler(repo).HandleAsync(new(data.Device), default)).Status);
        Assert.Null(await auth.HandleAsync(new AuthenticateDeviceCommand { DeviceId = data.Device, Secret = new string('a', 64) }, default));
        Assert.Equal(DeviceAssignmentStatus.DeviceDisabled, (await new IssueDeviceHandler(repo).HandleAsync(new(data.Device, data.Auction, data.Customer), default)).Status);
        await using var check = Db();
        var credential = await check.DeviceCredentials.Include(c => c.Device).SingleAsync(c => c.DeviceId == data.Device);
        Assert.False(credential.IsEnabled);
        Assert.Equal("Disabled", credential.Device.Status);
        Assert.Equal(Now, (await check.DeviceSessions.SingleAsync()).EndedAt);
        Assert.Equal(1, await check.AuctionRegistrations.CountAsync());
        Assert.Equal(DeviceAssignmentStatus.Success, (await new IssueDeviceHandler(AssignmentRepo(check)).HandleAsync(new(data.OtherDevice, data.Auction, data.Customer), default)).Status);
    }

    [PostgresStockFact]
    public async Task SessionRevisionPersistsAcrossIssueReleaseAndDeactivation()
    {
        var data = await SeedAssignmentsAsync();
        await using var db = Db();
        var repo = AssignmentRepo(db);
        var initial = await repo.IssueAsync(data.Device, data.Auction, data.Customer, default);
        var sessionId = Assert.IsType<Guid>(initial.SessionId);
        Assert.Equal(1, (await repo.GetActiveSessionAsync(data.Device, sessionId, default))!.SessionRevision);
        await repo.ReleaseAsync(data.Device, data.Auction, sessionId, default);
        await repo.ReleaseAsync(data.Device, data.Auction, sessionId, default);
        Assert.Equal(2, (await repo.GetCredentialAsync(data.Device, default))!.SessionRevision);
        var next = await repo.IssueAsync(data.Device, data.Auction, data.Customer, default);
        Assert.Equal(3, (await repo.GetActiveSessionAsync(data.Device, next.SessionId, default))!.SessionRevision);
        await repo.DeactivateAsync(data.Device, default);
        await repo.DeactivateAsync(data.Device, default);
        Assert.Equal(4, (await repo.GetCredentialAsync(data.Device, default))!.SessionRevision);
        Assert.Null(await repo.GetActiveSessionAsync(data.Device, next.SessionId, default));
    }

    [PostgresStockFact]
    public async Task InvalidAndMissingIdentifiersDoNotCreateAssignments()
    {
        var data = await SeedAssignmentsAsync();
        await using var db = Db();
        var repo = AssignmentRepo(db);
        var issue = new IssueDeviceHandler(repo);
        Assert.Equal(DeviceAssignmentStatus.InvalidRequest, (await issue.HandleAsync(new(Guid.Empty, data.Auction, data.Customer), default)).Status);
        Assert.Equal(DeviceAssignmentStatus.InvalidRequest, (await issue.HandleAsync(new(data.Device, Guid.Empty, data.Customer), default)).Status);
        Assert.Equal(DeviceAssignmentStatus.InvalidRequest, (await issue.HandleAsync(new(data.Device, data.Auction, Guid.Empty), default)).Status);
        Assert.Equal(DeviceAssignmentStatus.InvalidRequest, (await new ReleaseDeviceHandler(repo).HandleAsync(new(data.Device, data.Auction, Guid.Empty), default)).Status);
        Assert.Equal(DeviceAssignmentStatus.InvalidRequest, (await new DeactivateDeviceHandler(repo).HandleAsync(new(Guid.Empty), default)).Status);
        Assert.Equal(DeviceAssignmentStatus.AuctionNotFound, (await issue.HandleAsync(new(data.Device, Guid.NewGuid(), data.Customer), default)).Status);
        Assert.Equal(DeviceAssignmentStatus.DeviceNotFound, (await issue.HandleAsync(new(Guid.NewGuid(), data.Auction, data.Customer), default)).Status);
        Assert.Equal(DeviceAssignmentStatus.CustomerNotFound, (await issue.HandleAsync(new(data.Device, data.Auction, Guid.NewGuid()), default)).Status);
        Assert.Equal(DeviceAssignmentStatus.DeviceNotFound, (await new DeactivateDeviceHandler(repo).HandleAsync(new(Guid.NewGuid()), default)).Status);
        Assert.Empty(await db.DeviceSessions.ToListAsync());
        Assert.Empty(await db.AuctionRegistrations.ToListAsync());
    }
}
