using System.Security.Claims;
using Auktionshuset.Api.Security;
using Auktionshuset.Api.Services;
using Auktionshuset.Contracts.Dto.Billing;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Service.Billing;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Api.Endpoints.Billing;

public static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var staff = endpoints.MapGroup("/api/staff/invoices").RequireAuthorization(SecurityPolicies.BillingStaff);
        staff.MapGet("/", async (AHDBContext db, CancellationToken ct) =>
            Results.Ok((await db.AuctionInvoices.AsNoTracking().Include(i => i.Lines)
                .OrderByDescending(i => i.Sequence).ToListAsync(ct)).Select(ToResponse).ToList()));
        staff.MapGet("/{id:guid}", async (Guid id, AHDBContext db, CancellationToken ct) =>
        {
            var invoice = await db.AuctionInvoices.AsNoTracking().Include(i => i.Lines).SingleOrDefaultAsync(i => i.Id == id, ct);
            return invoice is null ? Results.NotFound() : Results.Ok(ToResponse(invoice));
        });
        staff.MapPost("/{id:guid}/payment", async (Guid id, RegisterInvoicePayment request,
            ClaimsPrincipal user, AuctionInvoiceStore store, CancellationToken ct) =>
        {
            if (!TryActor(user, out var actor, out var name)) return Results.Forbid();
            var error = await store.PayAsync(id, request.Method, request.Amount, actor, name, ct);
            return error is null ? Results.NoContent() : Results.Conflict(new { detail = error });
        });
        staff.MapPost("/{id:guid}/lines/{lineId:guid}/collection", async (Guid id, Guid lineId,
            ClaimsPrincipal user, AuctionInvoiceStore store, CancellationToken ct) =>
        {
            if (!TryActor(user, out var actor, out var name)) return Results.Forbid();
            var error = await store.CollectAsync(id, lineId, actor, name, ct);
            return error is null ? Results.NoContent() : Results.Conflict(new { detail = error });
        });
    }

    public static void MapCustomerInvoices(this RouteGroupBuilder group)
    {
        group.MapGet("/invoices", async (ClaimsPrincipal user, CustomerProfileService profiles, AHDBContext db, CancellationToken ct) =>
        {
            var own = await profiles.GetOwnProfileAsync(user, ct);
            if (own.Profile is null) return Results.Problem(Customer.CustomerProfileProblems.NoCustomerLink());
            var invoices = await db.AuctionInvoices.AsNoTracking().Include(i => i.Lines)
                .Where(i => i.CustomerId == own.Profile.CustomerId).OrderByDescending(i => i.Sequence).ToListAsync(ct);
            return Results.Ok(invoices.Select(ToResponse).ToList());
        });
        group.MapGet("/invoices/{id:guid}", async (Guid id, ClaimsPrincipal user, CustomerProfileService profiles,
            AHDBContext db, CancellationToken ct) =>
        {
            var own = await profiles.GetOwnProfileAsync(user, ct);
            if (own.Profile is null) return Results.Problem(Customer.CustomerProfileProblems.NoCustomerLink());
            var invoice = await db.AuctionInvoices.AsNoTracking().Include(i => i.Lines)
                .SingleOrDefaultAsync(i => i.Id == id && i.CustomerId == own.Profile.CustomerId, ct);
            return invoice is null ? Results.NotFound() : Results.Ok(ToResponse(invoice));
        });
        group.MapGet("/invoices/{id:guid}/lines/{lineId:guid}/receipt", async (Guid id, Guid lineId,
            ClaimsPrincipal user, CustomerProfileService profiles, AHDBContext db, CancellationToken ct) =>
        {
            var own = await profiles.GetOwnProfileAsync(user, ct);
            if (own.Profile is null) return Results.Problem(Customer.CustomerProfileProblems.NoCustomerLink());
            var invoice = await db.AuctionInvoices.AsNoTracking().Include(i => i.Lines)
                .SingleOrDefaultAsync(i => i.Id == id && i.CustomerId == own.Profile.CustomerId, ct);
            if (invoice is null || !invoice.Lines.Any(l => l.Id == lineId)) return Results.NotFound();
            if (invoice.PaidAt is null) return Results.Conflict(new { detail = "Kvittering er tilgængelig efter betaling." });
            var response = ToResponse(invoice);
            return Results.Ok(response with { Lines = response.Lines.Where(l => l.Id == lineId).ToList() });
        });
    }

    private static bool TryActor(ClaimsPrincipal user, out Guid id, out string name)
    {
        name = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email") ?? "";
        return Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out id)
            && id != Guid.Empty && !string.IsNullOrWhiteSpace(name);
    }

    public static InvoiceResponse ToResponse(AuctionInvoice i) => new(i.Id, i.Number, i.AuctionId, i.AuctionName,
        i.IssuedAt, i.CustomerName, i.CustomerAddress, i.CustomerEmail, i.SellerName, i.SellerAddress, i.SellerEmail,
        i.SellerCvr, i.Currency, i.PaymentTerms, i.PremiumPercent, i.VatPercent, i.HammerTotal, i.PremiumTotal,
        i.VatTotal, i.Total, i.PaidAt is null ? "Afventer betaling" : i.Lines.All(l => l.CollectedAt != null) ? "Afhentet" : "Betalt",
        i.PaidAt, i.PaymentMethod, i.PaidAmount, i.PaidByUserId, i.PaidByName,
        i.Lines.OrderBy(l => l.Name).ThenBy(l => l.Id).Select(l => new InvoiceLineResponse(l.Id, l.AuctionLotId,
            l.Name, l.Description, l.Quantity, l.HammerPrice, l.Premium, l.HammerVat, l.PremiumVat, l.Total, l.WonAt,
            l.CollectedAt != null ? "Afhentet" : i.PaidAt != null ? "Betalt" : "Afventer betaling",
            l.CollectedAt, l.CollectedByUserId, l.CollectedByName)).ToList());
}
