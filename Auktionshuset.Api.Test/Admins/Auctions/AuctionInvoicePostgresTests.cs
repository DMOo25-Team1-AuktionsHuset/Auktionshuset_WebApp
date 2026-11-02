using Auktionshuset.Application.Auction;
using Auktionshuset.Api.Test.Billing;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Service.Auctions;
using Auktionshuset.Infrastructure.Service.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public sealed partial class AuctionStockPostgresTests
{
    private static IConfiguration BillingConfig(string vat = "25") => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Billing:VatPercent"] = vat }).Build();
    private static AuctionInvoiceStore Billing(AHDBContext db, string vat = "25") => new(db, Clock, BillingConfig(vat));

    [PostgresStockFact]
    public async Task RealWinningBidBecomesOneInvoiceWithConcurrentPaymentAndCollection()
    {
        var auction = await ScheduledAuctionAsync();
        await using (var db = Db())
        {
            var bid = await new AuctionLotBiddingStore(db, Clock, new TestOutbox(db))
                .PlaceBidAsync(new PlaceBidCommand(auction.LineId, auction.CustomerId, null, Guid.NewGuid(), 1000m), default);
            Assert.True(bid.Accepted);
            await new AuctionLotBiddingStore(db, Clock, new TestOutbox(db)).CloseAuctionLotAsync(auction.LineId, default);
            await Billing(db).IssueAsync(auction.AuctionId);
            Assert.Empty(await db.AuctionInvoices.ToListAsync());
            await Lifecycle(db).CloseAsync(auction.AuctionId, default);
        }
        async Task Issue()
        {
            await using var db = Db(); await Billing(db).IssueAsync(auction.AuctionId);
        }
        await Task.WhenAll(Issue(), Issue(), Issue());
        await using var verify = Db();
        var invoice = Assert.Single(await verify.AuctionInvoices.AsNoTracking().Include(i => i.Lines).ToListAsync());
        Assert.Equal("AH1-01-01-2030-000001", invoice.Number);
        Assert.Equal(1100m, invoice.Total); Assert.Equal(220m, invoice.VatTotal);
        Assert.Equal(4, Assert.Single(invoice.Lines).Quantity);
        Assert.Equal(6, (await verify.Lot.SingleAsync()).Quantity);
        var actorA = Guid.NewGuid(); var actorB = Guid.NewGuid();
        async Task<string?> Pay(Guid actor, string method = "Cash")
        {
            await using var db = Db(); return await Billing(db).PayAsync(invoice.Id, method, 1100m, actor, actor.ToString(), default);
        }
        Assert.All(await Task.WhenAll(Pay(actorA), Pay(actorB)), Assert.Null);
        Assert.NotNull(await Pay(actorA, "Card"));
        async Task<string?> Collect(Guid actor)
        {
            await using var db = Db(); return await Billing(db).CollectAsync(invoice.Id, invoice.Lines[0].Id, actor, actor.ToString(), default);
        }
        Assert.All(await Task.WhenAll(Collect(actorA), Collect(actorB)), Assert.Null);
        await Issue(); // another fresh context, after all transitions
        verify.ChangeTracker.Clear();
        var paid = await verify.AuctionInvoices.AsNoTracking().Include(i => i.Lines).SingleAsync();
        Assert.Contains(paid.PaidByUserId!.Value, new[] { actorA, actorB });
        Assert.Contains(paid.Lines[0].CollectedByUserId!.Value, new[] { actorA, actorB });
        Assert.Equal(Now, paid.PaidAt); Assert.Equal(Now, paid.Lines[0].CollectedAt);
        Assert.Single(await verify.AuctionInvoiceLines.ToListAsync());
        Assert.Equal(1, (await verify.BillingSequences.SingleAsync()).LastInvoice);
        // Database uniqueness is a second line of defence, independent of store checks.
        verify.AuctionInvoiceLines.Add(new AuctionInvoiceLine { InvoiceId = invoice.Id,
            AuctionLotId = auction.LineId, WinningBidId = invoice.Lines[0].WinningBidId });
        await Assert.ThrowsAsync<DbUpdateException>(() => verify.SaveChangesAsync());
    }

    [PostgresStockFact]
    public async Task WorkerRetriesFailedInvoiceAtomicallyAndConsolidatesPerCustomerAndAuction()
    {
        Guid first, second, invalid;
        await using (var db = Db())
        {
            var customer = new Customer { CustomerId = Guid.NewGuid(), FirstName = "Faktura", LastName = "Kunde",
                Email = "billing@example.test", PhoneNumber = "12345678", Address = "Test", BirthDate = new(1990, 1, 1) };
            db.Customers.Add(customer); await db.SaveChangesAsync();
            first = await BillingTestData.SeedAsync(db, customer.CustomerId);
            second = await BillingTestData.SeedAsync(db, customer.CustomerId);
            invalid = await BillingTestData.SeedAsync(db, customer.CustomerId);
            var broken = await db.AuctionLot.SingleAsync(l => l.AuctionId == invalid && l.Quantity == 3);
            broken.Quantity = 0; await db.SaveChangesAsync();
        }
        ServiceProvider Services() => new ServiceCollection().AddScoped(_ => Db()).AddSingleton(Clock)
            .AddSingleton(BillingConfig()).AddScoped<AuctionInvoiceStore>().BuildServiceProvider();
        async Task Scan()
        {
            using var provider = Services();
            using var worker = new AuctionInvoiceWorker(provider.GetRequiredService<IServiceScopeFactory>(), Clock, NullLogger<AuctionInvoiceWorker>.Instance);
            await worker.ProcessAsync();
        }
        await Task.WhenAll(Scan(), Scan());
        await using (var db = Db())
        {
            var invoices = await db.AuctionInvoices.Include(i => i.Lines).ToListAsync();
            Assert.Equal(2, invoices.Count);
            Assert.All(invoices, i => { Assert.Equal(2, i.Lines.Count); Assert.Equal(1100.06m, i.Total); });
            Assert.Equal(new long[] { 1, 2 }, invoices.Select(i => i.Sequence).OrderBy(i => i));
            Assert.DoesNotContain(invoices, i => i.AuctionId == invalid);
            Assert.Null((await db.Auction.SingleAsync(a => a.AuctionId == invalid)).AuctionNumber);
            Assert.Equal(2, (await db.BillingSequences.SingleAsync()).LastInvoice);
            (await db.AuctionLot.SingleAsync(l => l.AuctionId == invalid && l.Quantity == 0)).Quantity = 3;
            await db.SaveChangesAsync();
        }
        await Scan(); await Scan();
        await using var final = Db();
        Assert.Equal(3, await final.AuctionInvoices.CountAsync());
        Assert.Equal(6, await final.AuctionInvoiceLines.CountAsync());
        Assert.Equal(3, (await final.BillingSequences.SingleAsync()).LastInvoice);
    }

    [PostgresStockFact]
    public async Task SeparateCustomersGetSeparateConsolidatedInvoicesAndLegacyInvoicesBlockReissue()
    {
        await using var db = Db();
        var customers = Enumerable.Range(1, 2).Select(n => new Customer { CustomerId = Guid.NewGuid(),
            FirstName = "Kunde", LastName = n.ToString(), Email = $"invoice{n}@example.test", PhoneNumber = "12345678",
            Address = "Test", BirthDate = new(1990, 1, 1) }).ToArray();
        db.Customers.AddRange(customers); await db.SaveChangesAsync();
        var auctionId = await BillingTestData.SeedAsync(db, customers[0].CustomerId);
        var secondBid = await db.Bids.SingleAsync(b => b.AuctionLot.AuctionId == auctionId && b.Amount == 0.05m);
        secondBid.CustomerId = customers[1].CustomerId;
        await db.SaveChangesAsync();
        await Billing(db).IssueAsync(auctionId);
        var invoices = await db.AuctionInvoices.Include(i => i.Lines).ToListAsync();
        Assert.Equal(2, invoices.Count);
        Assert.Equal(1100m, invoices.Single(i => i.CustomerId == customers[0].CustomerId).Total);
        Assert.Equal(0.06m, invoices.Single(i => i.CustomerId == customers[1].CustomerId).Total);
        Assert.All(invoices, i => Assert.Single(i.Lines));
        Assert.Equal(1, (await db.Auction.SingleAsync(a => a.AuctionId == auctionId)).AuctionNumber);
        Assert.Equal(2, invoices.Select(i => i.Number).Distinct().Count());

        var legacyAuction = await BillingTestData.SeedAsync(db, customers[0].CustomerId);
        var legacyLine = await db.AuctionLot.SingleAsync(l => l.AuctionId == legacyAuction && l.Quantity == 3);
        db.Set<Invoice>().Add(new Invoice { InvoiceId = Guid.NewGuid(), InvoiceNumber = 42,
            InvoiceDateTime = Now, Status = "Betalt", AuctionLotId = legacyLine.AuctionLotId,
            CustomerId = customers[0].CustomerId, WinningBidId = legacyLine.CurrentHighestBidId!.Value });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Billing(db).IssueAsync(legacyAuction));
        Assert.Equal(2, await db.AuctionInvoices.CountAsync());
        Assert.Equal(2, (await db.BillingSequences.SingleAsync()).LastInvoice);
    }

    [PostgresStockFact]
    public async Task InvoiceMigrationUpgradesPreviousSchemaAndPreservesData()
    {
        var name = "codex_invoice_upgrade_" + Guid.NewGuid().ToString("N");
        var source = Environment.GetEnvironmentVariable("AUCTION_STOCK_TEST_CONNECTION")!;
        await using var admin = new NpgsqlConnection(source); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(source) { Database = name, Pooling = false }.ConnectionString;
            await using var db = new AHDBContext(new DbContextOptionsBuilder<AHDBContext>().UseNpgsql(connection).Options);
            await db.GetService<IMigrator>().MigrateAsync("20261029100559_AddAuctionRegistrations");
            // Use raw SQL while the database deliberately has the previous model.
            var id = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AuctionHouse" ("AuctionHouseId", "AuctionHouseName", "Address", "CVRNumber", "PhoneNumber", "Email")
                VALUES ({id}, 'Migrationstest', 'Test', 12345678, '12345678', 'migration@example.test')
                """);
            var customer = Guid.NewGuid(); var user = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Customers" ("CustomerId", "FirstName", "LastName", "Address", "Email", "PhoneNumber", "BirthDate")
                VALUES ({customer}, 'Eksisterende', 'Kunde', 'Test', 'existing@example.test', '12345678', DATE '1990-01-01');
                INSERT INTO "LoginUsers" ("UserId", "CustomerId", "Email", "PasswordHash", "Role")
                VALUES ({user}, {customer}, 'existing@example.test', 'preserved-password-hash', 'Customer');
                """);
            await db.Database.MigrateAsync();
            Assert.Equal("Migrationstest", (await db.AuctionHouse.SingleAsync()).AuctionHouseName);
            Assert.Empty(await db.AuctionInvoices.ToListAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Contains("20261029123907_AddAuctionInvoices", await db.Database.GetAppliedMigrationsAsync());
            var login = await db.LoginUsers.SingleAsync();
            Assert.True(login.IsActive);
            Assert.Equal(customer, login.CustomerId);
            Assert.Null(login.EmployeeId);
            Assert.Equal("preserved-password-hash", login.PasswordHash);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
