using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Contracts.Dto.Customer.Response;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;

namespace Auktionshuset.Api.Test.Security;

public sealed partial class CustomerProfileEndpointTests
{
    [Fact]
    public async Task TwoCustomersSeeOwnBidsAndOnlyPersistedResultsAndPurchases()
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        var (first, firstToken, firstCustomer) = await CreateSignedInCustomerAsync(host);
        var (second, secondToken, secondCustomer) = await CreateSignedInCustomerAsync(host, "second@example.test");
        first.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstToken);
        second.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondToken);
        var auctionId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var unsoldId = Guid.NewGuid();
        var winningBidId = Guid.NewGuid();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            var house = new AuctionHouse { AuctionHouseId = Guid.NewGuid(), AuctionHouseName = "Test",
                Address = "Test", CVRNumber = 12345678, PhoneNumber = "12345678", Email = "house@example.test" };
            var auction = new Auction { AuctionId = auctionId, AuctionHouseId = house.AuctionHouseId, AuctionHouse = house,
                Name = "Resultatauktion", AuctionStatus = AuctionStatuses.Live, StartsAt = DateTime.UtcNow.AddHours(-1),
                StartedAtTime = DateTime.UtcNow.AddHours(-1), EndedAt = DateTime.UtcNow.AddHours(1) };
            var lot = new Lot { LotId = Guid.NewGuid(), AuctionHouseId = house.AuctionHouseId, AuctionHouse = house,
                Name = "To stole", Description = "Samlet linje", Category = "Møbler", Quantity = 10, EstimatedValue = 100 };
            var unsoldLot = new Lot { LotId = Guid.NewGuid(), AuctionHouseId = house.AuctionHouseId, AuctionHouse = house,
                Name = "Bord", Description = "Uden bud", Category = "Møbler", Quantity = 1, EstimatedValue = 50 };
            db.AuctionLot.AddRange(
                new AuctionLot { AuctionLotId = lineId, AuctionId = auctionId, Auction = auction, LotId = lot.LotId,
                    Lot = lot, Quantity = 2, StartingPrice = 100, OpenForBids = true },
                new AuctionLot { AuctionLotId = unsoldId, AuctionId = auctionId, Auction = auction, LotId = unsoldLot.LotId,
                    Lot = unsoldLot, Quantity = 1, StartingPrice = 50, OpenForBids = true });
            await db.SaveChangesAsync();
        }
        foreach (var client in new[] { first, second })
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/registration", new { })).StatusCode);

        async Task<CustomerAuctionDetailResponse> Detail(HttpClient client) =>
            (await client.GetFromJsonAsync<CustomerAuctionDetailResponse>($"/api/customer/auctions/{auctionId}"))!;
        async Task<List<CustomerPurchaseResponse>> Purchases(HttpClient client) =>
            (await client.GetFromJsonAsync<List<CustomerPurchaseResponse>>("/api/customer/purchases"))!;
        async Task SaveBid(Guid customerId, decimal amount, int sequence, Guid bidId)
        {
            await using var scope = host.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            db.Bids.Add(new Bid { BidId = bidId, AuctionLotId = lineId, Amount = amount, SequenceNumber = sequence,
                PlacedAt = DateTime.UtcNow, CustomerId = customerId, Customer = null!, DeviceSessionId = null, DeviceSession = null });
            await db.SaveChangesAsync();
            (await db.AuctionLot.SingleAsync(l => l.AuctionLotId == lineId)).CurrentHighestBidId = bidId;
            await db.SaveChangesAsync();
        }

        Assert.All((await Detail(first)).Lots, l => { Assert.Equal("Ingen bud", l.BidStatus); Assert.Null(l.OwnHighestBid); });
        await SaveBid(firstCustomer.CustomerId, 110, 1, Guid.NewGuid());
        var leading = (await Detail(first)).Lots.Single(l => l.AuctionLotId == lineId);
        Assert.Equal("Du fører", leading.BidStatus);
        Assert.Equal(110, leading.OwnHighestBid);
        await SaveBid(firstCustomer.CustomerId, 120, 2, Guid.NewGuid());
        await SaveBid(secondCustomer.CustomerId, 140, 3, winningBidId);
        var outbid = (await Detail(first)).Lots.Single(l => l.AuctionLotId == lineId);
        Assert.Equal("Du er overbudt", outbid.BidStatus);
        Assert.Equal(120, outbid.OwnHighestBid);
        Assert.Equal(140, outbid.CurrentPrice);
        var otherLeader = (await Detail(second)).Lots.Single(l => l.AuctionLotId == lineId);
        Assert.Equal("Du fører", otherLeader.BidStatus);
        Assert.Equal(140, otherLeader.OwnHighestBid);
        Assert.Empty(await Purchases(second)); // A live highest-bid pointer is not a purchase.

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            (await db.Auction.SingleAsync(a => a.AuctionId == auctionId)).EndedAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        var pending = await Detail(second);
        Assert.Equal("Afventer resultat", pending.Status);
        Assert.False(pending.IsLive);
        Assert.All(pending.Lots, l => { Assert.False(l.IsClosed); Assert.False(l.OpenForBids); Assert.False(l.IsWinner); Assert.Null(l.HammerPrice); });
        Assert.Equal("Du fører", pending.Lots.Single(l => l.AuctionLotId == lineId).BidStatus);
        Assert.Empty(await Purchases(second));
        Assert.Equal("Afventer resultat", Assert.Single((await second.GetFromJsonAsync<List<CustomerAuctionResponse>>("/api/customer/my-auctions"))!).Status);

        // Persist a line result before the entire auction closes: it is already final.
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            var line = await db.AuctionLot.SingleAsync(l => l.AuctionLotId == lineId);
            line.ClosedAtTime = DateTime.UtcNow; line.OpenForBids = false;
            var unsold = await db.AuctionLot.SingleAsync(l => l.AuctionLotId == unsoldId);
            unsold.ClosedAtTime = DateTime.UtcNow; unsold.OpenForBids = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal("Tabt", (await Detail(first)).Lots.Single(l => l.AuctionLotId == lineId).BidStatus);
        var won = (await Detail(second)).Lots.Single(l => l.AuctionLotId == lineId);
        Assert.Equal("Vundet", won.BidStatus);
        Assert.Equal(140, won.HammerPrice);
        Assert.All(new[] { await Detail(first), await Detail(second) }, d =>
            Assert.Equal("Usolgt", d.Lots.Single(l => l.AuctionLotId == unsoldId).BidStatus));
        Assert.Empty(await Purchases(first));
        var purchase = Assert.Single(await Purchases(second));
        Assert.Equal(lineId, purchase.AuctionLotId);
        Assert.Equal(2, purchase.Quantity);
        Assert.Equal(140, purchase.HammerPrice);

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            var auction = await db.Auction.SingleAsync(a => a.AuctionId == auctionId);
            auction.ClosedAtTime = DateTime.UtcNow; auction.AuctionStatus = AuctionStatuses.Ended;
            await db.SaveChangesAsync();
        }
        foreach (var client in new[] { first, second })
        {
            Assert.Empty((await client.GetFromJsonAsync<List<CustomerAuctionResponse>>("/api/customer/auctions"))!);
            Assert.Equal(AuctionStatuses.Ended, Assert.Single((await client.GetFromJsonAsync<List<CustomerAuctionResponse>>("/api/customer/my-auctions"))!).Status);
            foreach (string path in new[] { $"auctions/{auctionId}", "my-auctions", "purchases" })
            {
                string json = await client.GetStringAsync($"/api/customer/{path}?customerId={secondCustomer.CustomerId}");
                foreach (string secret in new[] { firstCustomer.CustomerId.ToString(), secondCustomer.CustomerId.ToString(),
                    "kunde@example.test", "second@example.test", "Andreasen", "8888 8888", "Vestergade", "customerId", "winningBidId" })
                    Assert.DoesNotContain(secret, json, StringComparison.OrdinalIgnoreCase);
            }
        }
        // Query parameters cannot change whose history is returned.
        Assert.Empty((await first.GetFromJsonAsync<List<CustomerPurchaseResponse>>($"/api/customer/purchases?customerId={secondCustomer.CustomerId}"))!);
        var (third, thirdToken, _) = await CreateSignedInCustomerAsync(host, "third@example.test");
        third.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", thirdToken);
        Assert.Empty((await third.GetFromJsonAsync<List<CustomerAuctionResponse>>("/api/customer/my-auctions"))!);
        Assert.Empty(await Purchases(third));
        Assert.Equal(HttpStatusCode.Forbidden, (await third.GetAsync($"/api/customer/auctions/{auctionId}")).StatusCode);
    }

    [Theory]
    [InlineData("/api/customer/my-auctions")]
    [InlineData("/api/customer/purchases")]
    public async Task CustomerResultsRequireCustomerIdentity(string path)
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        using var client = host.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(host, "Employee"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(host, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }
}
