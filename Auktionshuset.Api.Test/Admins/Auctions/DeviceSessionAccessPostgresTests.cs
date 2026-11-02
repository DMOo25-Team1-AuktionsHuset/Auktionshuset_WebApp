using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.Admin.Devices.AuthenticateDevice;
using Auktionshuset.Application.Devices.AuthenticateDevice;
using Auktionshuset.Infrastructure.Service.Auth;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public sealed partial class AuctionStockPostgresTests
{
    [PostgresStockFact]
    public async Task DeviceAccessUsesCurrentSessionAndCredentialDespiteTrackedStaleEntities()
    {
        var data = await SeedAssignmentsAsync();
        await using var stale = Db();
        await using var other = Db();
        var staleRepo = AssignmentRepo(stale);
        var otherRepo = AssignmentRepo(other);
        var issue = await staleRepo.IssueAsync(data.Device, data.Auction, data.Customer, default);
        Guid sessionId = Assert.IsType<Guid>(issue.SessionId);
        await stale.DeviceCredentials.Include(c => c.Device).SingleAsync(c => c.DeviceId == data.Device);
        await stale.DeviceSessions.SingleAsync(s => s.DeviceSessionId == sessionId);
        var validator = new ValidateDeviceSessionHandler(staleRepo);
        var auth = new AuthenticateDeviceHandler(staleRepo, new DeviceSecretService());
        var command = new AuthenticateDeviceCommand { DeviceId = data.Device, Secret = new string('a', 64) };
        Assert.Equal(sessionId, (await auth.HandleAsync(command, default))!.Session!.SessionId);
        Assert.NotNull(await validator.HandleAsync(new(data.Device, sessionId), default));

        Assert.Equal(DeviceAssignmentStatus.Success, (await otherRepo.ReleaseAsync(data.Device, data.Auction, sessionId, default)).Status);
        Assert.Null(await validator.HandleAsync(new(data.Device, sessionId), default));
        Assert.Null((await auth.HandleAsync(command, default))!.Session);
        var reissue = await staleRepo.IssueAsync(data.Device, data.Auction, data.Customer, default);
        Guid newSession = Assert.IsType<Guid>(reissue.SessionId);
        Assert.NotEqual(sessionId, newSession);
        Assert.Equal(DeviceAssignmentStatus.Success, (await staleRepo.ReleaseAsync(data.Device, data.Auction, sessionId, default)).Status);
        Assert.NotNull(await validator.HandleAsync(new(data.Device, newSession), default));

        Assert.Equal(DeviceAssignmentStatus.Success, (await otherRepo.DeactivateAsync(data.Device, default)).Status);
        Assert.Null(await validator.HandleAsync(new(data.Device, newSession), default));
        Assert.Null(await auth.HandleAsync(command, default));
        Assert.Equal(DeviceAssignmentStatus.DeviceDisabled, (await staleRepo.IssueAsync(data.Device, data.Auction, data.Customer, default)).Status);
    }
}
