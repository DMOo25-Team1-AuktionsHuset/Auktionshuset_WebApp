using System.Security.Claims;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Api.Services;
using Auktionshuset.Infrastructure.Database;
using Microsoft.AspNetCore.TestHost;
using Auktionshuset.Api.Security;
using System.Net;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Auktionshuset.Api.Test.Security;

public sealed partial class CustomerProfileEndpointTests
{
    [Fact]
    public async Task CustomerHubChallengesAnonymousAndForbidsStaffSubscriptions()
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        using var client = host.GetTestClient();
        const string path = "/hubs/customer-auctions/negotiate?negotiateVersion=1";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(path, null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(host, SecurityRoles.Admin));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(path, null)).StatusCode);
        var (customer, token, _) = await CreateSignedInCustomerAsync(host);
        customer.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await customer.PostAsync(path, null)).StatusCode);
    }

    [Fact]
    public async Task CustomerHubRequiresActivePersistedCustomerLink()
    {
        using var database = new TemporarySqliteDatabase();
        using var host = await StartServerAsync(database.Path);
        await CreateSignedInCustomerAsync(host);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
        var login = await db.LoginUsers.SingleAsync();
        var hub = new CustomerAuctionHub(new CustomerProfileService(db));
        var context = new CustomerCaller(login.UserId);
        hub.Context = context;
        await hub.OnConnectedAsync();
        Assert.False(context.Aborted);

        // A valid token role alone cannot subscribe without the stored customer link.
        context = new CustomerCaller(Guid.NewGuid());
        hub.Context = context;
        await hub.OnConnectedAsync();
        Assert.True(context.Aborted);

        login.IsActive = false;
        await db.SaveChangesAsync();
        context = new CustomerCaller(login.UserId);
        hub.Context = context;
        await hub.OnConnectedAsync();
        Assert.True(context.Aborted);
    }

    private sealed class CustomerCaller(Guid userId) : HubCallerContext
    {
        public bool Aborted { get; private set; }
        public override string ConnectionId { get; } = Guid.NewGuid().ToString();
        public override string? UserIdentifier => userId.ToString();
        public override ClaimsPrincipal User { get; } = new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, "Customer")], "test"));
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => default;
        public override void Abort() => Aborted = true;
    }
}
