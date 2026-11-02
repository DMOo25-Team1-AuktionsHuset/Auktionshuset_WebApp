using System.Security.Claims;
using Auktionshuset.Api.Services;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Contracts.Dto.Customer.Response;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Auktionshuset.Infrastructure.Service.Auctions;

namespace Auktionshuset.Api.Endpoints.Customer;

public static class CustomerAuctionEndpoints
{
    public static void MapCustomerAuctions(this RouteGroupBuilder group)
    {
        group.MapGet("/auctions", GetAuctionsAsync);
        group.MapGet("/my-auctions", GetMyAuctionsAsync);
        group.MapGet("/purchases", GetPurchasesAsync);
        group.MapPost("/auctions/{auctionId:guid}/registration", RegisterAsync);
        group.MapDelete("/auctions/{auctionId:guid}/registration", UnregisterAsync);
        group.MapGet("/auctions/{auctionId:guid}", GetAuctionAsync);
        group.MapPost("/lots/{auctionLotId:guid}/bids", Admin.Bid.BidEndpoints.HandleAsync);
    }

    private static IQueryable<Domain.Entities.Auction> LiveAuctions(AHDBContext db, DateTime now) =>
        db.Auction.AsNoTracking().Where(a => a.AuctionStatus == AuctionStatuses.Live
            && a.StartedAtTime != null && a.ClosedAtTime == null && a.StartsAt <= now
            && (a.EndedAt == null || a.EndedAt > now));

    private static IQueryable<Domain.Entities.Auction> AvailableAuctions(AHDBContext db, DateTime now) =>
        db.Auction.AsNoTracking().Where(a => a.ClosedAtTime == null
            && (a.EndedAt == null || a.EndedAt > now)
            && ((a.AuctionStatus == AuctionStatuses.Upcoming && a.StartedAtTime == null)
                || (a.AuctionStatus == AuctionStatuses.Live && a.StartedAtTime != null && a.StartsAt <= now)));

    public static async Task<IResult> GetAuctionsAsync(ClaimsPrincipal user, CustomerProfileService profiles,
        AHDBContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var own = await profiles.GetOwnProfileAsync(user, cancellationToken);
        if (own.Profile is null) return Results.Problem(CustomerProfileProblems.NoCustomerLink());
        Guid customerId = own.Profile.CustomerId;
        var auctions = await AvailableAuctions(db, clock.GetUtcNow().UtcDateTime)
            .OrderBy(a => a.AuctionStatus == AuctionStatuses.Live ? 0 : 1).ThenBy(a => a.StartsAt).ThenBy(a => a.AuctionId)
            .Select(a => new CustomerAuctionResponse(a.AuctionId, a.Name, a.StartsAt, a.EndedAt,
                a.AuctionLots.Count, db.AuctionRegistrations.Any(r => r.AuctionId == a.AuctionId
                    && r.CustomerId == customerId), a.AuctionStatus)).ToListAsync(cancellationToken);
        return Results.Ok(auctions);
    }

    public static async Task<IResult> RegisterAsync(Guid auctionId, ClaimsPrincipal user,
        CustomerProfileService profiles, AHDBContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var own = await profiles.GetOwnProfileAsync(user, cancellationToken);
        if (own.Profile is null) return Results.Problem(CustomerProfileProblems.NoCustomerLink());
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SqlAuctionWriteLock.AcquireAsync(db, auctionId, cancellationToken);
        DateTime now = clock.GetUtcNow().UtcDateTime;
        if (!await AvailableAuctions(db, now).AnyAsync(a => a.AuctionId == auctionId, cancellationToken))
            return Results.Conflict(new { detail = "Auktionen er lukket for tilmelding." });
        Guid customerId = own.Profile.CustomerId;
        if (await db.AuctionRegistrations.AnyAsync(r => r.AuctionId == auctionId
            && r.CustomerId == customerId, cancellationToken)) return Results.NoContent();
        db.AuctionRegistrations.Add(new AuctionRegistration
        {
            AuctionId = auctionId, CustomerId = customerId, RegisteredAt = now
        });
        // The shared auction lock serializes duplicate registrations with bids and unregistration.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    public static async Task<IResult> UnregisterAsync(Guid auctionId, ClaimsPrincipal user,
        CustomerProfileService profiles, AHDBContext db, CancellationToken cancellationToken)
    {
        var own = await profiles.GetOwnProfileAsync(user, cancellationToken);
        if (own.Profile is null) return Results.Problem(CustomerProfileProblems.NoCustomerLink());
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // The same lock is held while a bid checks registration and persists the bid.
        await SqlAuctionWriteLock.AcquireAsync(db, auctionId, cancellationToken);
        Guid customerId = own.Profile.CustomerId;
        if (await db.Bids.AnyAsync(b => b.CustomerId == customerId && b.AuctionLot.AuctionId == auctionId, cancellationToken))
            return Results.Conflict(new { detail = "Du kan ikke framelde dig, fordi du har afgivet bud på auktionen. Dine bud er bindende, også når du er blevet overbudt." });
        var registration = await db.AuctionRegistrations.SingleOrDefaultAsync(
            r => r.AuctionId == auctionId && r.CustomerId == customerId, cancellationToken);
        if (registration is not null)
        {
            db.AuctionRegistrations.Remove(registration);
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    public static async Task<IResult> GetMyAuctionsAsync(ClaimsPrincipal user, CustomerProfileService profiles,
        AHDBContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var own = await profiles.GetOwnProfileAsync(user, cancellationToken);
        if (own.Profile is null) return Results.Problem(CustomerProfileProblems.NoCustomerLink());
        var now = clock.GetUtcNow().UtcDateTime;
        var auctions = await db.Auction.AsNoTracking()
            .Where(a => db.AuctionRegistrations.Any(r => r.AuctionId == a.AuctionId && r.CustomerId == own.Profile.CustomerId))
            .OrderByDescending(a => a.StartsAt)
            .Select(a => new CustomerAuctionResponse(a.AuctionId, a.Name, a.StartsAt, a.EndedAt,
                a.AuctionLots.Count, true, a.ClosedAtTime != null ? AuctionStatuses.Ended
                    : a.EndedAt <= now || a.AuctionStatus == AuctionStatuses.Ended ? "Afventer resultat" : a.AuctionStatus))
            .ToListAsync(cancellationToken);
        return Results.Ok(auctions);
    }

    public static async Task<IResult> GetPurchasesAsync(ClaimsPrincipal user, CustomerProfileService profiles,
        AHDBContext db, CancellationToken cancellationToken)
    {
        var own = await profiles.GetOwnProfileAsync(user, cancellationToken);
        if (own.Profile is null) return Results.Problem(CustomerProfileProblems.NoCustomerLink());
        // Only the persisted close result establishes a purchase, never the latest live bid.
        var purchases = await db.AuctionLot.AsNoTracking()
            .Where(l => l.ClosedAtTime != null && l.CurrentHighestBid != null
                && l.CurrentHighestBid.CustomerId == own.Profile.CustomerId
                && !db.AuctionInvoiceLines.Any(i => i.AuctionLotId == l.AuctionLotId))
            .OrderByDescending(l => l.ClosedAtTime)
            .Select(l => new CustomerPurchaseResponse(l.AuctionId, l.Auction.Name, l.AuctionLotId,
                l.Lot.Name, l.Quantity, l.CurrentHighestBid!.Amount, l.ClosedAtTime!.Value))
            .ToListAsync(cancellationToken);
        var invoiced = await db.AuctionInvoiceLines.AsNoTracking()
            .Where(l => l.Invoice.CustomerId == own.Profile.CustomerId)
            .Select(l => new CustomerPurchaseResponse(l.Invoice.AuctionId, l.Invoice.AuctionName, l.AuctionLotId,
                l.Name, l.Quantity, l.HammerPrice, l.WonAt)).ToListAsync(cancellationToken);
        return Results.Ok(purchases.Concat(invoiced).OrderByDescending(p => p.ClosedAt).ToList());
    }

    public static async Task<IResult> GetAuctionAsync(Guid auctionId, ClaimsPrincipal user,
        CustomerProfileService profiles, AHDBContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var own = await profiles.GetOwnProfileAsync(user, cancellationToken);
        if (own.Profile is null) return Results.Problem(CustomerProfileProblems.NoCustomerLink());
        if (!await db.AuctionRegistrations.AnyAsync(r => r.AuctionId == auctionId
            && r.CustomerId == own.Profile.CustomerId, cancellationToken))
            return Results.Problem(statusCode: 403, detail: "Du skal være tilmeldt auktionen for at se budsiden.");
        var auction = await db.Auction.AsNoTracking().SingleOrDefaultAsync(a => a.AuctionId == auctionId, cancellationToken);
        if (auction is null) return Results.NotFound();
        DateTime now = clock.GetUtcNow().UtcDateTime;
        bool live = await LiveAuctions(db, now)
            .AnyAsync(a => a.AuctionId == auctionId, cancellationToken);
        var lots = await db.AuctionLot.AsNoTracking().Where(l => l.AuctionId == auctionId)
            .OrderBy(l => l.Lot.Name).Select(l => new CustomerAuctionLotResponse(l.AuctionLotId,
                l.Lot.Name, l.Lot.Description, l.Quantity, l.StartingPrice,
                l.Bids.OrderByDescending(b => b.SequenceNumber).Select(b => (decimal?)b.Amount).FirstOrDefault() ?? l.StartingPrice,
                l.Bids.Any(), live && l.OpenForBids && l.ClosedAtTime == null,
                l.Bids.Where(b => b.CustomerId == own.Profile.CustomerId).OrderByDescending(b => b.SequenceNumber)
                    .Select(b => (decimal?)b.Amount).FirstOrDefault(),
                l.Bids.OrderByDescending(b => b.SequenceNumber).Select(b => (Guid?)b.CustomerId).FirstOrDefault() == own.Profile.CustomerId,
                l.ClosedAtTime != null,
                l.ClosedAtTime != null && l.CurrentHighestBid != null && l.CurrentHighestBid.CustomerId == own.Profile.CustomerId,
                l.ClosedAtTime != null && l.CurrentHighestBid != null ? (decimal?)l.CurrentHighestBid.Amount : null)).ToListAsync(cancellationToken);
        string status = auction.ClosedAtTime != null ? AuctionStatuses.Ended
            : auction.EndedAt <= now || auction.AuctionStatus == AuctionStatuses.Ended ? "Afventer resultat"
            : live ? AuctionStatuses.Live : AuctionStatuses.Upcoming;
        return Results.Ok(new CustomerAuctionDetailResponse(auctionId, auction.Name, live, lots, auction.StartsAt, status));
    }
}

