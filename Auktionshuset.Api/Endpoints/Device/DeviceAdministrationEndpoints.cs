using Auktionshuset.Api.Security;
using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.Admin.Devices.RegisterDevice;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

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

        group.MapPost("/", async (RegisterDeviceCommand request, RegisterDeviceHandler handler, HttpContext context, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(request, ct);
            if (!result.Succeeded) return Results.BadRequest(new { errors = result.Errors });
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { result.DeviceId, result.AuctionHouseId, result.DeviceNumber, result.Secret });
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
