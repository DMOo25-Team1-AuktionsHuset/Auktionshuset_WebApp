using System.Globalization;
using Auktionshuset.Application.Billing;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Service.Auctions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Auktionshuset.Infrastructure.Service.Billing;

public sealed class AuctionInvoiceStore(AHDBContext db, TimeProvider clock, IConfiguration configuration)
{
    public async Task IssueAsync(Guid auctionId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (db.Database.IsNpgsql()) await SqlAuctionWriteLock.AcquireAsync(db, auctionId, ct);
        var auction = await db.Auction.Include(a => a.AuctionHouse).SingleOrDefaultAsync(a => a.AuctionId == auctionId, ct);
        // A closed line alone is insufficient: the complete auction must be final.
        if (auction?.ClosedAtTime is null || auction.AuctionStatus != AuctionStatuses.Ended) return;
        var lines = await db.AuctionLot.Include(l => l.Lot).Include(l => l.CurrentHighestBid)
            .ThenInclude(b => b!.Customer)
            .Where(l => l.AuctionId == auctionId && l.ClosedAtTime != null && l.CurrentHighestBidId != null)
            .OrderBy(l => l.AuctionLotId).ToListAsync(ct);
        if (await db.AuctionLot.AnyAsync(l => l.AuctionId == auctionId && l.ClosedAtTime == null, ct))
            throw new InvalidOperationException("En afsluttet auktion har uafsluttede linjer.");
        var existing = await db.AuctionInvoices.Where(i => i.AuctionId == auctionId).Select(i => i.CustomerId).ToListAsync(ct);
        var groups = lines.GroupBy(l => l.CurrentHighestBid!.CustomerId).Where(g => !existing.Contains(g.Key)).ToList();
        if (groups.Count == 0) return;
        if (await db.Set<Invoice>().AnyAsync(i => i.AuctionLot.AuctionId == auctionId, ct))
            throw new InvalidOperationException("Auktionen har ældre fakturaer. Afstem dem før ny fakturering.");
        var vat = configuration.GetValue<decimal?>("Billing:VatPercent")
            ?? throw new InvalidOperationException("Billing:VatPercent skal være afklaret før fakturering.");
        const decimal premiumPercent = 10m;
        var counter = await db.BillingSequences.SingleOrDefaultAsync(s => s.Id == 1, ct);
        if (counter is null)
        {
            counter = new BillingSequence();
            db.BillingSequences.Add(counter);
        }
        auction.AuctionNumber ??= checked(++counter.LastAuction);
        var now = clock.GetUtcNow().UtcDateTime;
        var localDate = TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen"));
        foreach (var group in groups.OrderBy(g => g.Key))
        {
            var customer = group.First().CurrentHighestBid!.Customer;
            var invoice = new AuctionInvoice
            {
                Sequence = checked(++counter.LastInvoice), AuctionId = auctionId, CustomerId = customer.CustomerId,
                IssuedAt = now, AuctionName = auction.Name,
                CustomerName = $"{customer.FirstName} {customer.LastName}", CustomerAddress = customer.Address,
                CustomerEmail = customer.Email, SellerName = auction.AuctionHouse.AuctionHouseName,
                SellerAddress = auction.AuctionHouse.Address, SellerEmail = auction.AuctionHouse.Email,
                SellerCvr = auction.AuctionHouse.CVRNumber, PremiumPercent = premiumPercent, VatPercent = vat
            };
            invoice.Number = string.Create(CultureInfo.InvariantCulture,
                $"AH{auction.AuctionNumber}-{localDate:dd-MM-yyyy}-{invoice.Sequence:D6}");
            foreach (var line in group)
            {
                var amounts = InvoiceAmounts.Calculate(line.CurrentHighestBid!.Amount, premiumPercent, vat);
                if (line.Quantity <= 0) throw new InvalidOperationException("Ugyldigt antal på vundet linje.");
                invoice.Lines.Add(new AuctionInvoiceLine
                {
                    AuctionLotId = line.AuctionLotId, WinningBidId = line.CurrentHighestBidId!.Value,
                    Name = line.Lot.Name, Description = line.Lot.Description, Quantity = line.Quantity,
                    HammerPrice = amounts.HammerPrice, Premium = amounts.Premium, HammerVat = amounts.HammerVat,
                    PremiumVat = amounts.PremiumVat, Total = amounts.Total, WonAt = line.ClosedAtTime!.Value
                });
            }
            invoice.HammerTotal = invoice.Lines.Sum(l => l.HammerPrice);
            invoice.PremiumTotal = invoice.Lines.Sum(l => l.Premium);
            invoice.VatTotal = invoice.Lines.Sum(l => l.HammerVat + l.PremiumVat);
            invoice.Total = invoice.Lines.Sum(l => l.Total);
            db.AuctionInvoices.Add(invoice);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<string?> PayAsync(Guid invoiceId, string method, decimal amount, Guid actor, string actorName, CancellationToken ct)
    {
        if (actor == Guid.Empty || string.IsNullOrWhiteSpace(actorName)) return "Medarbejderidentitet mangler.";
        if (method is not ("Card" or "Cash")) return "Vælg kort eller kontant.";
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        var invoice = await db.AuctionInvoices.SingleOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice is null) return "Fakturaen findes ikke.";
        if (amount != invoice.Total) return "Hele fakturabeløbet skal registreres som betalt.";
        if (invoice.PaidAt is not null)
            return invoice.PaymentMethod == method && invoice.PaidAmount == amount ? null : "Betalingen er allerede registreret med andre oplysninger.";
        invoice.PaidAt = clock.GetUtcNow().UtcDateTime;
        invoice.PaidByUserId = actor;
        invoice.PaidByName = actorName;
        invoice.PaidAmount = amount;
        invoice.PaymentMethod = method;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return null;
    }

    public async Task<string?> CollectAsync(Guid invoiceId, Guid lineId, Guid actor, string actorName, CancellationToken ct)
    {
        if (actor == Guid.Empty || string.IsNullOrWhiteSpace(actorName)) return "Medarbejderidentitet mangler.";
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        var line = await db.AuctionInvoiceLines.Include(l => l.Invoice)
            .SingleOrDefaultAsync(l => l.Id == lineId && l.InvoiceId == invoiceId, ct);
        if (line is null) return "Fakturalinjen findes ikke.";
        if (line.Invoice.PaidAt is null) return "Hele fakturaen skal være betalt før afhentning.";
        if (line.CollectedAt is not null) return null;
        line.CollectedAt = clock.GetUtcNow().UtcDateTime;
        line.CollectedByUserId = actor;
        line.CollectedByName = actorName;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return null;
    }

    private Task LockAsync(CancellationToken ct) => db.Database.IsNpgsql()
        ? db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(704291031)", ct)
        : Task.CompletedTask;
}
