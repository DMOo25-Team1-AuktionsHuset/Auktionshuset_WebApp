using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Auktionshuset.Api.Security;
using Auktionshuset.Api.Test.Billing;
using Auktionshuset.Contracts.Dto.Billing;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Service.Billing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Auktionshuset.Api.Test.Security;

public sealed partial class CustomerProfileEndpointTests
{
    [Fact]
    public async Task InvoiceOwnershipPaymentReceiptsCollectionAndRestartAreProtected()
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        var (owner, token, customer) = await CreateSignedInCustomerAsync(host);
        var (other, otherToken, _) = await CreateSignedInCustomerAsync(host, "other@example.test");
        owner.DefaultRequestHeaders.Authorization = new("Bearer", token);
        other.DefaultRequestHeaders.Authorization = new("Bearer", otherToken);
        Guid auctionId;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            auctionId = await BillingTestData.SeedAsync(db, customer.CustomerId, closed: false);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Billing:VatPercent"] = "25" }).Build();
            var store = new AuctionInvoiceStore(db, TimeProvider.System, config);
            await store.IssueAsync(auctionId);
            Assert.Empty(await db.AuctionInvoices.ToListAsync()); // Final lines, whole auction still live.
            var auction = await db.Auction.SingleAsync(a => a.AuctionId == auctionId);
            auction.ClosedAtTime = DateTime.UtcNow; auction.AuctionStatus = "Afsluttet";
            await db.SaveChangesAsync();
            await store.IssueAsync(auctionId);
            await store.IssueAsync(auctionId);
        }
        var invoice = Assert.Single((await owner.GetFromJsonAsync<List<InvoiceResponse>>("/api/customer/invoices"))!);
        Assert.Equal(2, invoice.Lines.Count);
        Assert.Equal(1000.05m, invoice.HammerTotal);
        Assert.Equal(100.01m, invoice.PremiumTotal);
        Assert.Equal(1100.06m, invoice.Total);
        Assert.Equal(220.01m, invoice.VatTotal);
        Assert.Equal(3, invoice.Lines.Single(l => l.HammerPrice == 1000m).Quantity);
        Assert.Equal("Afventer betaling", invoice.Status);
        Assert.Matches(@"^AH1-\d{2}-\d{2}-\d{4}-000001$", invoice.Number);
        var url = $"/api/customer/invoices/{invoice.Id}";
        var lineId = invoice.Lines[0].Id;
        var receipt = $"{url}/lines/{lineId}/receipt";
        Assert.Empty((await other.GetFromJsonAsync<List<InvoiceResponse>>("/api/customer/invoices"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(receipt)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.GetAsync(receipt)).StatusCode);
        using var staff = host.GetTestClient();
        staff.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(host, SecurityRoles.Employee));
        var payUrl = $"/api/staff/invoices/{invoice.Id}/payment";
        var collectUrl = $"/api/staff/invoices/{invoice.Id}/lines/{lineId}/collection";
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync(collectUrl, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(payUrl, new RegisterInvoicePayment("Card", invoice.Total))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(collectUrl, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync(payUrl, new RegisterInvoicePayment("Card", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync(payUrl, new RegisterInvoicePayment("Online", invoice.Total))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsJsonAsync(payUrl, new RegisterInvoicePayment("Card", invoice.Total))).StatusCode);
        var paid = (await owner.GetFromJsonAsync<InvoiceResponse>(url))!;
        Assert.Equal("Betalt", paid.Status); Assert.NotNull(paid.PaidAt); Assert.NotNull(paid.PaidByUserId);
        Assert.Equal("employee-role@example.test", paid.PaidByName);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsJsonAsync(payUrl, new RegisterInvoicePayment("Card", invoice.Total))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync(payUrl, new RegisterInvoicePayment("Cash", invoice.Total))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(receipt)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsJsonAsync(collectUrl, new { })).StatusCode);
        var partial = (await owner.GetFromJsonAsync<InvoiceResponse>(url))!;
        Assert.Equal("Betalt", partial.Status);
        Assert.Equal("Afhentet", partial.Lines.Single(l => l.Id == lineId).Status);
        Assert.Equal("Betalt", partial.Lines.Single(l => l.Id != lineId).Status);
        var collected = partial.Lines.Single(l => l.Id == lineId);
        Assert.NotNull(collected.CollectedAt); Assert.NotNull(collected.CollectedByUserId);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsJsonAsync(collectUrl, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync($"/api/staff/invoices/{Guid.NewGuid()}/lines/{lineId}/collection", new { })).StatusCode);
        foreach (var line in invoice.Lines)
            Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsJsonAsync($"/api/staff/invoices/{invoice.Id}/lines/{line.Id}/collection", new { })).StatusCode);

        // Fresh host and contexts: payment, audit and frozen values survive a process restart.
        using var restarted = await StartServerAsync(database.Path);
        await using (var scope = restarted.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            foreach (var lot in await db.Lot.ToListAsync()) { lot.Name = "Ændret katalog"; lot.Description = "Ændret"; }
            (await db.Customers.SingleAsync(c => c.CustomerId == customer.CustomerId)).Address = "Ny adresse";
            await db.SaveChangesAsync();
            // Missing VAT configuration must not matter when an invoice already exists.
            await new AuctionInvoiceStore(db, TimeProvider.System, new ConfigurationBuilder().Build()).IssueAsync(auctionId);
            Assert.Single(await db.AuctionInvoices.ToListAsync());
        }
        using var after = restarted.GetTestClient(); after.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var final = (await after.GetFromJsonAsync<InvoiceResponse>(url))!;
        Assert.Equal("Afhentet", final.Status); Assert.Equal(paid.PaidAt, final.PaidAt);
        Assert.Equal(collected.CollectedAt, final.Lines.Single(l => l.Id == lineId).CollectedAt);
        Assert.Equal(invoice.CustomerAddress, final.CustomerAddress);
        Assert.Equal(invoice.Total, final.Total);
        Assert.DoesNotContain(final.Lines, l => l.Name == "Ændret katalog");
        var purchases = (await after.GetFromJsonAsync<List<Auktionshuset.Contracts.Dto.Customer.Response.CustomerPurchaseResponse>>("/api/customer/purchases"))!;
        Assert.Equal(2, purchases.Count);
        Assert.Contains(purchases, p => p.Name == "Tre stole" && p.Quantity == 3 && p.HammerPrice == 1000m);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(SecurityRoles.Customer, HttpStatusCode.Forbidden)]
    [InlineData(SecurityRoles.Employee, HttpStatusCode.OK)]
    [InlineData(SecurityRoles.LotAdmin, HttpStatusCode.OK)]
    [InlineData(SecurityRoles.AuctionAdmin, HttpStatusCode.OK)]
    [InlineData(SecurityRoles.Admin, HttpStatusCode.OK)]
    public async Task BillingStaffEndpointsRequireEmployeeRoles(string? role, HttpStatusCode expected)
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        using var client = host.GetTestClient();
        if (role is not null) client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(host, role));
        Assert.Equal(expected, (await client.GetAsync("/api/staff/invoices/")).StatusCode);
        if (role is null)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/customer/invoices")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/staff/invoices/{Guid.NewGuid()}/payment", new RegisterInvoicePayment("Cash", 1))).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/staff/invoices/{Guid.NewGuid()}/lines/{Guid.NewGuid()}/collection", new { })).StatusCode);
        }
    }
}
