using System.Net;
using System.Net.Http.Json;
using Auktionshuset.Api.Endpoints.Admin.Auction.CloseAuction;
using Auktionshuset.Api.Endpoints.Admin.Auction.CreateAuction;
using Auktionshuset.Api.Endpoints.Admin.Auction.Lifecycle;
using Auktionshuset.Api.Endpoints.Auth;
using Auktionshuset.Api.Endpoints.Customer;
using Auktionshuset.Api.Security;
using Auktionshuset.Api.Services;
using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Auction;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Auktionshuset.Contracts.Dto.Auth;
using Auktionshuset.Contracts.Dto.Customer.Response;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Messaging;
using Auktionshuset.Infrastructure.Repositories;
using Auktionshuset.Infrastructure.Service.Auctions;
using Auktionshuset.Infrastructure.Service.Lots;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public sealed partial class AuctionStockPostgresTests
{
    // Real HTTP endpoints, authentication, PostgreSQL locks, bid receipts and transactional outbox.
    // The clock is controlled to exercise exact deadlines without a wall-clock race.
    [PostgresStockFact]
    public async Task TwoCustomerJourneyPersistsRealBidsManualAndTimedResultsAndProtectsAdminOperations()
    {
        var origin = DateTime.UtcNow;
        var clock = new MutableClock(origin);
        using var host = await StartJourneyHostAsync(clock);
        using var admin = host.GetTestClient();
        using var anonymous = host.GetTestClient();
        async Task<string> Login(HttpClient client, string email, string password)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        }
        admin.DefaultRequestHeaders.Authorization = new("Bearer", await Login(admin, "admin@example.test", "test-password"));
        async Task<HttpClient> Customer(string email)
        {
            var client = host.GetTestClient();
            var created = await client.PostAsJsonAsync("/api/auth/register", new RegisterCustomerRequest
            {
                FirstName = "Anna", LastName = "Test", Email = email, PhoneNumber = "12345678",
                Address = "Testvej 1", BirthDate = new(1990, 1, 1),
                Password = "test-password-123", ConfirmPassword = "test-password-123"
            });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            client.DefaultRequestHeaders.Authorization = new("Bearer", await Login(client, email, "test-password-123"));
            return client;
        }
        using var first = await Customer("first@example.test");
        using var second = await Customer("second@example.test");
        Guid automaticLotId = Guid.NewGuid(), unsoldLotId = Guid.NewGuid();
        await using (var db = Db())
        {
            foreach (var id in new[] { automaticLotId, unsoldLotId })
                db.Lot.Add(new Lot { LotId = id, AuctionHouseId = houseId, Name = "Ekstra " + id,
                    Category = "Test", Description = "Test", Quantity = 5, EstimatedValue = 50 });
            await db.SaveChangesAsync();
        }
        var create = await admin.PostAsJsonAsync("/api/auctions/", new CreateAuctionRequest
        {
            Name = "Samlet PostgreSQL-forløb", AuctionHouseId = houseId,
            StartsAt = new DateTimeOffset(origin.AddMinutes(1)), EndsAt = new DateTimeOffset(origin.AddMinutes(2)),
            Lots = [new(lotId, 2, 100), new(automaticLotId, 3, 50), new(unsoldLotId, 1, 25)]
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var auctionId = (await create.Content.ReadFromJsonAsync<CreateAuctionResponse>())!.AuctionId;
        Guid manualId, automaticId, unsoldId;
        await using (var db = Db())
        {
            var lines = await db.AuctionLot.AsNoTracking().ToListAsync();
            Assert.Equal(3, lines.Count);
            manualId = lines.Single(l => l.LotId == lotId).AuctionLotId;
            automaticId = lines.Single(l => l.LotId == automaticLotId).AuctionLotId;
            unsoldId = lines.Single(l => l.LotId == unsoldLotId).AuctionLotId;
            Assert.Equal(100, lines.Single(l => l.AuctionLotId == manualId).StartingPrice);
        }
        foreach (var client in new[] { first, second })
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/registration", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/registration", new { })).StatusCode);
        }
        async Task<PlaceBidResult> Bid(HttpClient client, Guid line, decimal amount, Guid? requestId = null)
        {
            var response = await client.PostAsJsonAsync($"/api/customer/lots/{line}/bids", new { amount, requestId = requestId ?? Guid.NewGuid() });
            Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<PlaceBidResult>())!;
        }
        var protectedPaths = new[] { $"/api/auctions/{auctionId}/start", $"/api/auctions/{auctionId}/close", $"/api/auction-lots/{manualId}" };
        foreach (var path in protectedPaths)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(path, null)).StatusCode);
            foreach (var customer in new[] { first, second })
                Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsync(path, null)).StatusCode);
        }
        Assert.Equal("BiddingClosed", (await Bid(first, manualId, 100)).ErrorCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync(protectedPaths[0], null)).StatusCode);
        clock.Now = origin.AddMinutes(1);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync(protectedPaths[0], null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync(protectedPaths[0], null)).StatusCode);
        Assert.Equal("BidTooLow", (await Bid(first, manualId, 99)).ErrorCode);
        Assert.True((await Bid(first, manualId, 100)).Accepted);
        Assert.True((await Bid(second, manualId, 110)).Accepted);
        // Equal concurrent bids: exactly one can win the next sequence number.
        PlaceBidResult[] race;
        await using (var owner = Db())
        await using (var held = await Sessions(owner).BeginAsync(auctionId, default))
        {
            var bidA = Bid(first, manualId, 120);
            var bidB = Bid(second, manualId, 120);
            await Task.Delay(150);
            Assert.False(bidA.IsCompleted);
            Assert.False(bidB.IsCompleted);
            await held.CommitAsync(default);
            race = await Task.WhenAll(bidA, bidB);
        }
        Assert.Single(race, b => b.Accepted);
        Assert.Equal("BidTooLow", Assert.Single(race, b => !b.Accepted).ErrorCode);
        Assert.True((await Bid(first, manualId, 130)).Accepted);
        var duplicateId = Guid.NewGuid();
        var duplicates = await Task.WhenAll(Bid(second, automaticId, 60, duplicateId), Bid(second, automaticId, 60, duplicateId));
        Assert.All(duplicates, b => Assert.True(b.Accepted));
        Assert.Single(duplicates, b => b.Duplicate);
        Assert.Equal(duplicates[0].BidId, duplicates[1].BidId);
        Assert.Equal("RequestIdReuseWithDifferentPayload", (await Bid(second, automaticId, 61, duplicateId)).ErrorCode);

        async Task<CustomerAuctionDetailResponse> Detail(HttpClient client) =>
            (await client.GetFromJsonAsync<CustomerAuctionDetailResponse>($"/api/customer/auctions/{auctionId}"))!;
        async Task<List<CustomerPurchaseResponse>> Purchases(HttpClient client) =>
            (await client.GetFromJsonAsync<List<CustomerPurchaseResponse>>("/api/customer/purchases"))!;
        Assert.Equal("Du fører", (await Detail(first)).Lots.Single(l => l.AuctionLotId == manualId).BidStatus);
        Assert.Equal("Du er overbudt", (await Detail(second)).Lots.Single(l => l.AuctionLotId == manualId).BidStatus);
        Assert.Empty(await Purchases(first));
        var close = await admin.PostAsync(protectedPaths[2], null);
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync(protectedPaths[2], null)).StatusCode);
        Assert.Equal("BiddingClosed", (await Bid(second, manualId, 140)).ErrorCode);
        var manualPurchase = Assert.Single(await Purchases(first));
        Assert.Equal(130, manualPurchase.HammerPrice);
        Assert.Equal(2, manualPurchase.Quantity);
        Assert.Equal("Tabt", (await Detail(second)).Lots.Single(l => l.AuctionLotId == manualId).BidStatus);
        clock.Now = origin.AddMinutes(2).AddTicks(-10); // One PostgreSQL microsecond before end.
        Assert.True((await Bid(first, automaticId, 70)).Accepted);
        Assert.True((await Bid(second, automaticId, 80)).Accepted);
        clock.Now = origin.AddMinutes(2);
        Assert.Equal("BiddingClosed", (await Bid(first, automaticId, 90)).ErrorCode);
        Assert.Equal("Afventer resultat", (await Detail(second)).Status);
        Assert.Empty(await Purchases(second));
        using var worker = new AuctionExpirationWorker(host.Services.GetRequiredService<IServiceScopeFactory>(), clock,
            NullLogger<AuctionExpirationWorker>.Instance);
        await worker.ProcessDueAsync(default);
        await worker.ProcessDueAsync(default);
        Assert.Equal("BiddingClosed", (await Bid(first, automaticId, 100)).ErrorCode);
        Assert.Equal("Vundet", (await Detail(first)).Lots.Single(l => l.AuctionLotId == manualId).BidStatus);
        Assert.Equal("Tabt", (await Detail(first)).Lots.Single(l => l.AuctionLotId == automaticId).BidStatus);
        Assert.Equal("Vundet", (await Detail(second)).Lots.Single(l => l.AuctionLotId == automaticId).BidStatus);
        Assert.Equal("Usolgt", (await Detail(second)).Lots.Single(l => l.AuctionLotId == unsoldId).BidStatus);
        var automaticPurchase = Assert.Single(await Purchases(second));
        Assert.Equal(80, automaticPurchase.HammerPrice);
        Assert.Equal(3, automaticPurchase.Quantity);
        foreach (var client in new[] { first, second })
        {
            Assert.Empty((await client.GetFromJsonAsync<List<CustomerAuctionResponse>>("/api/customer/auctions"))!);
            Assert.Equal(AuctionStatuses.Ended, Assert.Single((await client.GetFromJsonAsync<List<CustomerAuctionResponse>>("/api/customer/my-auctions"))!).Status);
        }
        await using (var db = Db())
        {
            Assert.Equal(2, await db.AuctionRegistrations.CountAsync());
            Assert.Equal(7, await db.Bids.CountAsync());
            Assert.Equal(8, (await db.Lot.SingleAsync(l => l.LotId == lotId)).Quantity);
            Assert.Equal(2, (await db.Lot.SingleAsync(l => l.LotId == automaticLotId)).Quantity);
            Assert.Equal(5, (await db.Lot.SingleAsync(l => l.LotId == unsoldLotId)).Quantity);
            Assert.All(await db.AuctionLot.ToListAsync(), l => { Assert.NotNull(l.ClosedAtTime); Assert.False(l.OpenForBids); });
            Assert.Null((await db.AuctionLot.SingleAsync(l => l.AuctionLotId == unsoldId)).CurrentHighestBidId);
            Assert.Empty(await new EFAuctionRepository(db).GetReservedQuantitiesAsync(null, default));
            Assert.Equal(3, await db.OutboxMessages.CountAsync(m => m.EventType == "auction-lot.closed.v1"));
        }
    }

    private Task<IHost> StartJourneyHostAsync(TimeProvider clock) => new HostBuilder().ConfigureWebHost(web =>
    {
        web.UseTestServer();
        web.ConfigureServices(services =>
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:BootstrapAdmin:Email"] = "admin@example.test",
                ["Authentication:BootstrapAdmin:Password"] = "test-password",
                ["Authentication:SigningKey"] = "Postgres-journey-test-signing-key-at-least-32-bytes",
                ["Authentication:Issuer"] = "test", ["Authentication:Audience"] = "test"
            }).Build();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddRouting();
            services.AddSecurityServices(configuration);
            services.AddSingleton(clock);
            services.AddScoped(_ => Db());
            services.AddApiServices();
            services.AddScoped<IRefreshTokenStore, PostgresRefreshTokenStore>();
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();
            services.AddScoped<IAuctionRepository, EFAuctionRepository>();
            services.AddScoped<ILotRepository, EFLotRepo>();
            services.AddScoped<IEmployeeRepository, EFEmployeeRepository>();
            services.AddSingleton<EventContractRegistry>();
            services.AddScoped<IOutboxWriter, EfOutboxWriter>();
            services.AddScoped<IAuctionWriteSessionFactory, PostgresAuctionWriteSessionFactory>();
            services.AddScoped<IAuctionLifeCycleStore, AuctionLifeCycleStore>();
            services.AddScoped<AuctionLifeCycleStore>();
            services.AddScoped<IPlaceBidStore, AuctionLotBiddingStore>();
            services.AddScoped<ICloseAuctionLotStore, AuctionLotBiddingStore>();
        });
        web.Configure(app =>
        {
            app.UseRouting(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAuthEndpoints();
                endpoints.MapCustomerEndpoints();
                var auctions = endpoints.MapGroup("/api/auctions").RequireAuthorization();
                auctions.MapCreateAuction();
                auctions.MapAuctionLifecycle();
                endpoints.MapCloseAuctionLotEndpoint();
            });
        });
    }).StartAsync();
}
