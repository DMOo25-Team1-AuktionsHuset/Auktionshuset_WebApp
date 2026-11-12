using Auktionshuset.Api.Security;
using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.Admin.Devices.RegisterDevice;
using Auktionshuset.Domain;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Api.Services;
using Microsoft.EntityFrameworkCore;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Service.Auctions;
using System.Security.Claims;

namespace Auktionshuset.Api.Endpoints.Device;

public static class DeviceAdministrationEndpoints
{
    public static void MapDeviceAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/staff/devices").RequireAuthorization(SecurityPolicies.BillingStaff);

        group.MapGet("/customers", async (AHDBContext db, CancellationToken ct) =>
            Results.Ok(await db.Customers.AsNoTracking().OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
                .ThenBy(c => c.CustomerId)
                .Select(c => new { c.CustomerId, c.FirstName, c.LastName, c.Email }).ToListAsync(ct)));

        group.MapGet("/auctions", async (AHDBContext db, CancellationToken ct) =>
            Results.Ok(await db.Auction.AsNoTracking().OrderByDescending(a => a.StartsAt).ThenBy(a => a.AuctionId)
                .Select(a => new { a.AuctionId, a.AuctionHouseId, a.AuctionNumber, a.Name, a.StartsAt, a.AuctionStatus })
                .ToListAsync(ct)));

        group.MapGet("/available", async (AHDBContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            return Results.Ok(await db.DeviceCredentials.AsNoTracking()
                .Where(c => c.IsEnabled && !db.DeviceSessions.Any(s => s.DeviceId == c.DeviceId && (s.EndedAt == null || s.EndedAt > now)))
                .OrderBy(c => c.Device.DeviceNumber).ThenBy(c => c.DeviceId)
                .Select(c => new { c.DeviceId, c.Device.AuctionHouseId, c.Device.DeviceNumber }).ToListAsync(ct));
        });

        group.MapGet("/sessions", async (AHDBContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            return Results.Ok(await db.DeviceSessions.AsNoTracking()
                .Where(s => s.StartedAt <= now && (s.EndedAt == null || s.EndedAt > now)
                    && db.DeviceCredentials.Any(c => c.DeviceId == s.DeviceId && c.IsEnabled))
                .OrderBy(s => s.StartedAt).ThenBy(s => s.DeviceSessionId)
                .Select(s => new { SessionId = s.DeviceSessionId, s.DeviceId, s.Device.DeviceNumber, s.AuctionId, s.CustomerId, s.StartedAt, s.EndedAt }).ToListAsync(ct));
        });

        group.MapGet("/{deviceId:guid}/bid-requests/{requestId:guid}", async (Guid deviceId,
            Guid requestId, AHDBContext db, TimeProvider clock, CancellationToken ct) =>
        {
            if (!await db.Set<Auktionshuset.Domain.Entities.Device>().AnyAsync(d => d.DeviceId == deviceId, ct))
                return Results.NotFound();
            var result = await (from key in db.BidCommandKeys.AsNoTracking()
                join session in db.DeviceSessions.AsNoTracking() on key.DeviceSessionId equals session.DeviceSessionId
                where session.DeviceId == deviceId && key.RequestId == requestId
                select new { key.RequestId, key.DeviceSessionId, key.AuctionLotId, key.RequestedAmount,
                    key.Accepted, key.BidId, key.ErrorCode, key.ProcessedAt }).SingleOrDefaultAsync(ct);
            if (result is null)
            {
                var recovery = await (from r in db.DeviceBidRecoveries.AsNoTracking()
                    join session in db.DeviceSessions.AsNoTracking() on r.SessionId equals session.DeviceSessionId
                    where r.DeviceId == deviceId && r.RequestId == requestId
                    select new { r.SessionId, r.AuctionLotId, RequestedAmount = r.Amount,
                        r.ReportedAt, session.EndedAt }).SingleOrDefaultAsync(ct);
                return Results.Ok(new { state = "unresolved", requestId, deviceId,
                    deviceSessionId = recovery?.SessionId, auctionLotId = recovery?.AuctionLotId,
                    requestedAmount = recovery?.RequestedAmount, reportedAt = recovery?.ReportedAt,
                    canClear = recovery is not null && recovery.EndedAt is DateTime ended &&
                        ended <= clock.GetUtcNow().UtcDateTime });
            }
            return Results.Ok(new { state = result.Accepted ? "accepted" : "rejected", deviceId,
                result.RequestId, result.DeviceSessionId, result.AuctionLotId, result.RequestedAmount,
                result.BidId, result.ErrorCode, result.ProcessedAt });
        });

        group.MapPost("/{deviceId:guid}/bid-requests/{requestId:guid}/clear-no-bid", async (
            Guid deviceId, Guid requestId, AHDBContext db, TimeProvider clock, HttpContext context,
            CancellationToken ct) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var recovery = await db.DeviceBidRecoveries.SingleOrDefaultAsync(r =>
                r.DeviceId == deviceId && r.RequestId == requestId, ct);
            if (recovery is null) return Results.NotFound();
            await SqlAuctionWriteLock.AcquireAsync(db, recovery.AuctionId, ct);
            if (db.Database.IsNpgsql()) await SqlAuctionLotLock.AcquireAsync(db, recovery.AuctionLotId, ct);
            var session = await db.DeviceSessions.AsNoTracking().SingleOrDefaultAsync(s =>
                s.DeviceSessionId == recovery.SessionId && s.DeviceId == deviceId &&
                s.CustomerId == recovery.CustomerId, ct);
            if (session?.EndedAt is not DateTime ended || ended > clock.GetUtcNow().UtcDateTime)
                return Results.Conflict(new { error = "OriginalSessionStillActive" });
            if (await db.BidCommandKeys.AnyAsync(k => k.CustomerId == recovery.CustomerId &&
                k.RequestId == requestId, ct))
                return Results.Conflict(new { error = "BidAlreadyRecorded" });
            db.BidCommandKeys.Add(new BidCommandKey
            {
                BidCommandKeyId = Guid.NewGuid(), CustomerId = recovery.CustomerId,
                DeviceSessionId = recovery.SessionId, RequestId = requestId,
                AuctionLotId = recovery.AuctionLotId, RequestedAmount = recovery.Amount,
                AuctionRevision = recovery.AuctionRevision, Accepted = false, BidId = null,
                SequenceNumber = null, CurrentPrice = 0m, ErrorCode = "StaffReconciledNoBid",
                ResultAuctionRevision = null, ProcessedAt = clock.GetUtcNow().UtcDateTime
            });
            recovery.ClearedAt = clock.GetUtcNow().UtcDateTime;
            recovery.ClearedBy = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "staff";
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { deviceId, requestId, state = "rejected" });
        });

        group.MapPost("/", async (RegisterDeviceCommand request, RegisterDeviceHandler handler, HttpContext context, CancellationToken ct) =>
        {
            if (!await db.AuctionHouse.AnyAsync(a => a.AuctionHouseId == AuctionHouseDefaults.DefaultAuctionHouseId, ct))
                return Results.NotFound(new { detail = "Auktionshuset findes ikke." });
            var result = await handler.HandleAsync(request, ct);
            if (!result.Succeeded) return Results.BadRequest(new { errors = result.Errors });
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { result.DeviceId, result.AuctionHouseId, result.DeviceNumber, result.Secret });
        });

        group.MapPost("/station-registration", async (StationRegistrationRequest request,
            UsbStationRegistration registration, HttpContext context, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            try
            {
                var result = await registration.RegisterAsync(request, ct);
                return result is null ? Results.Problem("USB-stationen er ikke konfigureret på backend.", statusCode: 503)
                    : Results.Ok(result);
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = "InvalidRequest" });
            }
            catch (StationDeviceNumberConflictException)
            {
                return Results.Conflict(new { error = "DeviceNumberAlreadyRegistered" });
            }
        });

        group.MapPost("/{deviceId:guid}/complete-station-registration", async (Guid deviceId,
            StationCompletionRequest request, UsbStationRegistration registration, CancellationToken ct) =>
            await registration.CompleteAsync(deviceId, request, ct)
                ? Results.Ok(new { deviceId }) : Results.Conflict(new { error = "InvalidRequest" }));

        group.MapGet("/station-pending/{hardwareId}", async (string hardwareId,
            UsbStationRegistration registration, CancellationToken ct) =>
            await registration.PendingAsync(hardwareId, ct) is { } pending
                ? Results.Ok(pending) : Results.NotFound());
        group.MapPost("/station-resume", async (StationRegistrationRequest request,
            UsbStationRegistration registration, HttpContext context, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return await registration.ResumeAsync(request, ct) is { } result
                ? Results.Ok(result) : Results.Conflict(new { error = "PendingRegistrationNotFound" });
        });
        group.MapPost("/{deviceId:guid}/rotate-station-credential", async (Guid deviceId,
            StationRegistrationRequest request, UsbStationRegistration registration, HttpContext context, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            try
            {
                return await registration.RotateAsync(deviceId, request, ct) is { } result
                    ? Results.Ok(result) : Results.Conflict(new { error = "RepairNotAllowed" });
            }
            catch (StationDeviceAssignedException)
            { return Results.Conflict(new { error = "DeviceAlreadyAssigned" }); }
        });

        group.MapPost("/{deviceId:guid}/issue", async (Guid deviceId, IssueDeviceRequest request, IssueDeviceHandler handler, CancellationToken ct) =>
            AssignmentResponse(await handler.HandleAsync(new(deviceId, request.AuctionId, request.CustomerId), ct)));
        group.MapPost("/{deviceId:guid}/release", async (Guid deviceId, ReleaseDeviceRequest request, ReleaseDeviceHandler handler, CancellationToken ct) =>
            AssignmentResponse(await handler.HandleAsync(new(deviceId, request.AuctionId, request.SessionId), ct)));
    }

    private static IResult AssignmentResponse(DeviceAssignmentResult result) => result.Status switch
    {
        DeviceAssignmentStatus.Success => Results.Ok(new { result.SessionId }),
        DeviceAssignmentStatus.InvalidRequest => Results.BadRequest(new { error = result.Status.ToString() }),
        DeviceAssignmentStatus.DeviceNotFound or DeviceAssignmentStatus.AuctionNotFound or
            DeviceAssignmentStatus.CustomerNotFound or DeviceAssignmentStatus.SessionNotFound => Results.NotFound(new { error = result.Status.ToString() }),
        _ => Results.Conflict(new { error = result.Status.ToString() })
    };

    public sealed record IssueDeviceRequest(Guid AuctionId, Guid CustomerId);
    public sealed record ReleaseDeviceRequest(Guid AuctionId, Guid SessionId);
}
