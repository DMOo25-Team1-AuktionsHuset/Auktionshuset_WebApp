using Auktionshuset.Application.Admin.Employees;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Auktionshuset.Api.Endpoints.Customer;
using Auktionshuset.Api.Services;
using Auktionshuset.Application.Auction;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service.Auctions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Auktionshuset.Api.Endpoints.Admin.Employee;
using Auktionshuset.Api.Security;
using Auktionshuset.Contracts.Dto.Admin.Employee;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public sealed partial class AuctionStockPostgresTests
{
    [PostgresStockFact]
    public async Task ConcurrentAdminDemotionsKeepOneActiveAdministrator()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        await using (var db = Db())
        {
            foreach (var id in new[] { first, second })
            {
                db.Employee.Add(new Employee { EmployeeId = id, AuctionHouseId = houseId,
                    FirstName = "Admin", LastName = "Test", BirthDate = new(1990, 1, 1), Address = "Test" });
                db.LoginUsers.Add(new LoginUser { UserId = Guid.NewGuid(), CustomerId = null, EmployeeId = id,
                    Email = $"{id}@example.test", Role = "Admin", PasswordHash = "unused" });
            }
            await db.SaveChangesAsync();
        }
        async Task<IResult> Demote(Guid id)
        {
            await using var db = Db();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(db);
            services.AddSecurityServices(new ConfigurationBuilder().Build());
            await using var provider = services.BuildServiceProvider();
            return await EmployeeLoginEndpoints.SaveAsync(id, new SaveEmployeeLoginRequest($"{id}@example.test", null, "Employee", true), provider.GetRequiredService<EmployeeLoginHandler>(), default);
        }
        var results = await Task.WhenAll(Demote(first), Demote(second));
        Assert.Equal(new int?[] { 200, 409 }, results.Select(r => ((IStatusCodeHttpResult)r).StatusCode).Order().ToArray());
        await using var verify = Db();
        Assert.Equal(1, await verify.LoginUsers.CountAsync(u => u.Role == "Admin" && u.IsActive));
    }

    [PostgresStockFact]
    public async Task ConcurrentUnregistrationAndBidNeverLeaveABidWithoutRegistration()
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var auction = await ScheduledAuctionAsync(1);
            var userId = Guid.NewGuid();
            await using (var seed = Db())
            {
                seed.LoginUsers.Add(new LoginUser { UserId = userId, CustomerId = auction.CustomerId,
                    Email = $"race-{attempt}@example.test", Role = "Customer", PasswordHash = "unused" });
                await seed.SaveChangesAsync();
            }
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"));
            await using var bidding = Db();
            await using var leaving = Db();
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<Auktionshuset.Application.Auction.PlaceBidResult> Bid()
            {
                await gate.Task;
                return await new AuctionLotBiddingStore(bidding, Clock, new TestOutbox(bidding)).PlaceBidAsync(
                    new PlaceBidCommand(auction.LineId, auction.CustomerId, null, Guid.NewGuid(), 10), default);
            }
            async Task<IResult> Leave()
            {
                await gate.Task;
                return await CustomerAuctionEndpoints.UnregisterAsync(auction.AuctionId, principal, new CustomerProfileService(leaving), leaving, default);
            }
            var bid = Bid(); var leave = Leave(); gate.SetResult();
            await Task.WhenAll(bid, leave);
            await using var verify = Db();
            bool registered = await verify.AuctionRegistrations.AnyAsync(r => r.AuctionId == auction.AuctionId && r.CustomerId == auction.CustomerId);
            if (bid.Result.Accepted)
            {
                Assert.True(registered);
                Assert.Equal(409, ((IStatusCodeHttpResult)leave.Result).StatusCode);
                Assert.Single(await verify.Bids.Where(b => b.AuctionLotId == auction.LineId).ToListAsync());
            }
            else
            {
                Assert.Equal("NotRegistered", bid.Result.ErrorCode);
                Assert.False(registered);
                Assert.Equal(204, ((IStatusCodeHttpResult)leave.Result).StatusCode);
                Assert.Empty(await verify.Bids.Where(b => b.AuctionLotId == auction.LineId).ToListAsync());
            }
        }
    }
}
