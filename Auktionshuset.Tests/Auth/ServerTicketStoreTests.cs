using System.Security.Claims;
using Auktionshuset.Security;
using Microsoft.AspNetCore.Authentication;
using Xunit;

namespace Auktionshuset.Tests.Auth;

public sealed class ServerTicketStoreTests
{
    [Fact]
    public async Task TicketsKeepEachUsersJwtServerSideAndExpireAtTicketExpiry()
    {
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var store = new ServerTicketStore(clock);
        string aliceToken = "jwt-for-alice";
        string bobToken = "jwt-for-bob";
        string aliceSession = await store.StoreAsync(CreateTicket("alice@example.test", aliceToken, clock.GetUtcNow().AddMinutes(1)));
        string bobSession = await store.StoreAsync(CreateTicket("bob@example.test", bobToken, clock.GetUtcNow().AddMinutes(2)));

        Assert.NotEqual(aliceSession, bobSession);
        Assert.DoesNotContain(aliceToken, aliceSession, StringComparison.Ordinal);
        Assert.Equal(aliceToken, ReadToken(store, aliceSession));
        Assert.Equal(bobToken, ReadToken(store, bobSession));

        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.False(store.IsValid(aliceSession));
        Assert.True(store.IsValid(bobSession));
    }

    private static AuthenticationTicket CreateTicket(string email, string jwt, DateTimeOffset expiresAt)
    {
        var claims = new[] { new Claim(ClaimTypes.Email, email) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var properties = new AuthenticationProperties { ExpiresUtc = expiresAt };
        properties.Items[ServerTicketStore.BackendTokenProperty] = jwt;
        return new AuthenticationTicket(principal, properties, "test");
    }

    private static string? ReadToken(ServerTicketStore store, string sessionId) =>
        store.GetValidTicket(sessionId)?.Properties.Items[ServerTicketStore.BackendTokenProperty];

    private sealed class TestTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        private DateTimeOffset currentTime = currentTime;

        public override DateTimeOffset GetUtcNow() => currentTime;

        public void Advance(TimeSpan amount) => currentTime += amount;
    }
}
