using Microsoft.AspNetCore.TestHost;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Auction;
using Auktionshuset.Contracts.Dto.Auction;
using Auktionshuset.Contracts.Dto.Customer.Response;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Auktionshuset.Api.Test.Security;

public sealed partial class CustomerProfileEndpointTests
{
    [Fact]
    public async Task LiveAuctionsRequireOwnRegistrationBeforeViewingAndBidding()
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        var (client, token, registration) = await CreateSignedInCustomerAsync(host);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Guid auctionId = Guid.NewGuid(), lotId = Guid.NewGuid();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            var house = new AuctionHouse { AuctionHouseId = Guid.NewGuid(), AuctionHouseName = "Test",
                Address = "Test", CVRNumber = 12345678, PhoneNumber = "12345678", Email = "test@example.test" };
            var auction = new Auction { AuctionId = auctionId, AuctionHouseId = house.AuctionHouseId,
                AuctionHouse = house, Name = "Live testauktion", AuctionStatus = AuctionStatuses.Live,
                StartsAt = DateTime.UtcNow.AddHours(-1), StartedAtTime = DateTime.UtcNow.AddHours(-1) };
            var item = new Lot { LotId = Guid.NewGuid(), AuctionHouseId = house.AuctionHouseId, AuctionHouse = house,
                Name = "Stol", Category = "Møbler", Description = "En stol", Quantity = 1, EstimatedValue = 100 };
            db.AuctionLot.Add(new AuctionLot { AuctionLotId = lotId, AuctionId = auctionId, Auction = auction,
                LotId = item.LotId, Lot = item, Quantity = 1, StartingPrice = 100, OpenForBids = true });
            db.Auction.Add(new Auction { AuctionId = Guid.NewGuid(), AuctionHouseId = house.AuctionHouseId,
                AuctionHouse = house, Name = "Kommende", AuctionStatus = AuctionStatuses.Upcoming,
                StartsAt = DateTime.UtcNow.AddDays(1) });
            await db.SaveChangesAsync();
        }
        var list = (await client.GetFromJsonAsync<List<CustomerAuctionResponse>>("/api/customer/auctions"))!;
        Assert.Equal(2, list.Count);
        Assert.Equal(AuctionStatuses.Upcoming, Assert.Single(list, a => a.Name == "Kommende").Status);
        Assert.False(list.Single(a => a.AuctionId == auctionId).IsRegistered);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/customer/auctions/{auctionId}")).StatusCode);
        var bid = new PlaceBidRequest(Guid.NewGuid(), 110);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/customer/lots/{lotId}/bids", bid)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/bids/{lotId}", bid)).StatusCode);
        Assert.Empty(((CustomerAuctionBidStore)host.Services.GetRequiredService<IPlaceBidStore>()).Commands);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/registration", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/registration", new { })).StatusCode);
        var detail = (await client.GetFromJsonAsync<CustomerAuctionDetailResponse>($"/api/customer/auctions/{auctionId}"))!;
        Assert.True(detail.IsLive);
        Assert.Equal(100, Assert.Single(detail.Lots).CurrentPrice);
        Assert.True(detail.Lots[0].OpenForBids);
        Assert.True((await client.GetFromJsonAsync<List<CustomerAuctionResponse>>("/api/customer/auctions"))!
            .Single(a => a.AuctionId == auctionId).IsRegistered);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/customer/lots/{lotId}/bids", bid)).StatusCode);
        var command = Assert.Single(((CustomerAuctionBidStore)host.Services.GetRequiredService<IPlaceBidStore>()).Commands);
        Assert.Equal(registration.CustomerId, command.CustomerId);
        Assert.Equal(110, command.Amount);
        var (other, otherToken, _) = await CreateSignedInCustomerAsync(host, "other@example.test");
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/customer/auctions/{auctionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync($"/api/customer/lots/{lotId}/bids", bid)).StatusCode);
        await using var finalScope = host.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AHDBContext>();
        Assert.Single(await finalDb.AuctionRegistrations.ToListAsync());
        var finished = await finalDb.Auction.SingleAsync(a => a.AuctionId == auctionId);
        finished.ClosedAtTime = DateTime.UtcNow;
        finished.AuctionStatus = AuctionStatuses.Ended;
        await finalDb.SaveChangesAsync();
        Assert.Equal("Kommende", Assert.Single((await client.GetFromJsonAsync<List<CustomerAuctionResponse>>("/api/customer/auctions"))!).Name);
        Assert.Equal(HttpStatusCode.Conflict, (await other.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/registration", new { })).StatusCode);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(-60)] // Passing the planned start never starts an auction.
    public async Task UpcomingAuctionsAllowOwnIdempotentRegistrationAndPreview(int startMinutes)
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        var (client, token, customer) = await CreateSignedInCustomerAsync(host);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Guid auctionId = Guid.NewGuid();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            var house = new AuctionHouse { AuctionHouseId = Guid.NewGuid(), AuctionHouseName = "Test",
                Address = "Test", CVRNumber = 12345678, PhoneNumber = "12345678", Email = "test@example.test" };
            var auction = new Auction { AuctionId = auctionId, AuctionHouseId = house.AuctionHouseId,
                AuctionHouse = house, Name = "Forhåndstilmelding", AuctionStatus = AuctionStatuses.Upcoming,
                StartsAt = DateTime.UtcNow.AddMinutes(startMinutes) };
            var item = new Lot { LotId = Guid.NewGuid(), AuctionHouseId = house.AuctionHouseId, AuctionHouse = house,
                Name = "Stol", Category = "Møbler", Description = "En stol", Quantity = 1, EstimatedValue = 100 };
            db.AuctionLot.Add(new AuctionLot { AuctionLotId = Guid.NewGuid(), AuctionId = auctionId, Auction = auction,
                LotId = item.LotId, Lot = item, Quantity = 1, StartingPrice = 100, OpenForBids = true });
            await db.SaveChangesAsync();
        }
        var summary = Assert.Single((await client.GetFromJsonAsync<List<CustomerAuctionResponse>>("/api/customer/auctions"))!);
        Assert.Equal(AuctionStatuses.Upcoming, summary.Status);
        Assert.False(summary.IsRegistered);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/customer/auctions/{auctionId}")).StatusCode);
        var (other, otherToken, otherCustomer) = await CreateSignedInCustomerAsync(host, "preview-other@example.test");
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        for (int i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/registration",
                new { CustomerId = otherCustomer.CustomerId })).StatusCode);
        var detail = (await client.GetFromJsonAsync<CustomerAuctionDetailResponse>($"/api/customer/auctions/{auctionId}"))!;
        Assert.Equal(summary.StartsAt, detail.StartsAt);
        Assert.Equal(AuctionStatuses.Upcoming, detail.Status);
        Assert.False(detail.IsLive);
        Assert.False(Assert.Single(detail.Lots).OpenForBids);
        Assert.Equal("Stol", detail.Lots[0].Name);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/customer/auctions/{auctionId}")).StatusCode);
        Assert.False(Assert.Single((await other.GetFromJsonAsync<List<CustomerAuctionResponse>>("/api/customer/auctions"))!).IsRegistered);
        Assert.True(Assert.Single((await client.GetFromJsonAsync<List<CustomerAuctionResponse>>("/api/customer/auctions"))!).IsRegistered);
        await using var finalScope = host.Services.CreateAsyncScope();
        var registration = Assert.Single(await finalScope.ServiceProvider.GetRequiredService<AHDBContext>().AuctionRegistrations.ToListAsync());
        Assert.Equal(customer.CustomerId, registration.CustomerId);
    }

    [Theory]
    [InlineData("Kommende", false, false)]
    [InlineData("Live", false, false)]
    [InlineData("Afsluttet", true, true)]
    [InlineData("Kommende", false, true)]
    public async Task ClosedOrExpiredAuctionsAreHiddenAndRejectRegistration(string status, bool started, bool closed)
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        var (client, token, _) = await CreateSignedInCustomerAsync(host);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Guid id = Guid.NewGuid();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            var house = new AuctionHouse { AuctionHouseId = Guid.NewGuid(), AuctionHouseName = "Test",
                Address = "Test", CVRNumber = 12345678, PhoneNumber = "12345678", Email = "test@example.test" };
            db.Auction.Add(new Auction { AuctionId = id, AuctionHouseId = house.AuctionHouseId, AuctionHouse = house,
                Name = "Lukket", AuctionStatus = status, StartsAt = DateTime.UtcNow.AddHours(-2),
                StartedAtTime = started || status == AuctionStatuses.Live ? DateTime.UtcNow.AddHours(-2) : null,
                ClosedAtTime = closed ? DateTime.UtcNow.AddMinutes(-1) : null,
                EndedAt = closed ? DateTime.UtcNow.AddHours(1) : DateTime.UtcNow.AddMinutes(-1) });
            await db.SaveChangesAsync();
        }
        Assert.Empty((await client.GetFromJsonAsync<List<CustomerAuctionResponse>>("/api/customer/auctions"))!);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/customer/auctions/{id}/registration", new { })).StatusCode);
        await using var checkScope = host.Services.CreateAsyncScope();
        Assert.Empty(await checkScope.ServiceProvider.GetRequiredService<AHDBContext>().AuctionRegistrations.ToListAsync());
    }

    [Fact]
    public async Task CustomerAuctionEndpointsChallengeAnonymousUsers()
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        using var client = host.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/customer/auctions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/customer/auctions/{Guid.NewGuid()}/registration", new { })).StatusCode);
    }

    [Fact]
    public async Task UnregistrationIsOwnIdempotentAndRejectsAnyEarlierBidEvenWhenOutbid()
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        var (first, token, customer) = await CreateSignedInCustomerAsync(host);
        var (second, otherToken, other) = await CreateSignedInCustomerAsync(host, "other-unregister@example.test");
        first.DefaultRequestHeaders.Authorization = new("Bearer", token);
        second.DefaultRequestHeaders.Authorization = new("Bearer", otherToken);
        Guid auctionId = Guid.NewGuid(), lineId = Guid.NewGuid();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            var house = new AuctionHouse { AuctionHouseId = Guid.NewGuid(), AuctionHouseName = "Test", Address = "Test", CVRNumber = 12345678, PhoneNumber = "12345678", Email = "house@example.test" };
            var auction = new Auction { AuctionId = auctionId, AuctionHouseId = house.AuctionHouseId, AuctionHouse = house,
                Name = "Framelding", AuctionStatus = AuctionStatuses.Live, StartsAt = DateTime.UtcNow.AddHours(-1), StartedAtTime = DateTime.UtcNow.AddHours(-1) };
            var lot = new Lot { LotId = Guid.NewGuid(), AuctionHouseId = house.AuctionHouseId, AuctionHouse = house,
                Name = "Stol", Category = "Test", Description = "Test", Quantity = 1, EstimatedValue = 100 };
            db.AuctionLot.Add(new AuctionLot { AuctionLotId = lineId, AuctionId = auctionId, Auction = auction, LotId = lot.LotId, Lot = lot, Quantity = 1, StartingPrice = 100, OpenForBids = true });
            await db.SaveChangesAsync();
        }
        string registration = $"/api/customer/auctions/{auctionId}/registration";
        foreach (var client in new[] { first, second }) Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(registration, new { })).StatusCode);
        for (int i = 0; i < 2; i++) Assert.Equal(HttpStatusCode.NoContent, (await first.DeleteAsync(registration)).StatusCode);
        Assert.Empty((await first.GetFromJsonAsync<List<CustomerAuctionResponse>>("/api/customer/my-auctions"))!);
        Assert.Single((await second.GetFromJsonAsync<List<CustomerAuctionResponse>>("/api/customer/my-auctions"))!);
        Assert.Equal(HttpStatusCode.Forbidden, (await first.GetAsync($"/api/customer/auctions/{auctionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await first.PostAsJsonAsync(registration, new { })).StatusCode);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            db.Bids.AddRange(new Bid { BidId = Guid.NewGuid(), AuctionLotId = lineId, CustomerId = customer.CustomerId,
                Customer = null!, DeviceSession = null, DeviceSessionId = null, Amount = 110, PlacedAt = DateTime.UtcNow, SequenceNumber = 1 },
                new Bid { BidId = Guid.NewGuid(), AuctionLotId = lineId, CustomerId = other.CustomerId,
                Customer = null!, DeviceSession = null, DeviceSessionId = null, Amount = 120, PlacedAt = DateTime.UtcNow, SequenceNumber = 2 });
            await db.SaveChangesAsync();
        }
        using var rejected = await first.DeleteAsync(registration);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Contains("også når du er blevet overbudt", await rejected.Content.ReadAsStringAsync());
        Assert.Single((await first.GetFromJsonAsync<List<CustomerAuctionResponse>>("/api/customer/my-auctions"))!);
        using var anonymous = host.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync(registration)).StatusCode);
    }

    private sealed class CustomerAuctionBidStore : IPlaceBidStore
    {
        public List<PlaceBidCommand> Commands { get; } = [];
        public Task<PlaceBidResult> PlaceBidAsync(PlaceBidCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(new PlaceBidResult(true, false, Guid.NewGuid(), 1, command.Amount, null));
        }
    }
}


