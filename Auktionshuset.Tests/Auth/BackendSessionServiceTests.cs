using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Auktionshuset.Contracts.Dto.Auth;
using Auktionshuset.Contracts.Security;
using Auktionshuset.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Auktionshuset.Tests.Auth;

public sealed class BackendSessionServiceTests
{
    [Fact]
    public async Task ConcurrentCallersRotateOnceAndKeepAbsoluteExpiryAndNewRoles()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        fixture.Backend.BlockRefresh = true;
        Task<string?> first = fixture.Sessions.GetAccessTokenAsync(fixture.Id);
        await fixture.Backend.RefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<string?>[] others = Enumerable.Range(0, 12)
            .Select(_ => fixture.Sessions.GetAccessTokenAsync(fixture.Id)).ToArray();
        fixture.Backend.ReleaseRefresh.TrySetResult();

        Assert.All(await Task.WhenAll(others.Append(first)), token => Assert.Equal("new-access", token));
        Assert.Equal(1, fixture.Backend.RefreshCalls);
        AuthenticationTicket ticket = fixture.Tickets.GetValidTicket(fixture.Id)!;
        Assert.Equal(fixture.AbsoluteExpiry, ticket.Properties.ExpiresUtc);
        Assert.Equal("new-refresh", ticket.Properties.Items[ServerTicketStore.RefreshTokenProperty]);
        Assert.Equal(fixture.Id, ticket.Principal.FindFirstValue(ServerTicketStore.SessionIdClaim));
        Assert.True(ticket.Principal.IsInRole(AdminRoles.LotAdmin));
        Assert.False(ticket.Principal.IsInRole(AdminRoles.SuperAdmin));
        Assert.Equal("192.0.2.10", fixture.Backend.ForwardedIp);
    }

    [Fact]
    public async Task LogoutDuringRefreshRevokesTheReplacementAndCannotResurrectTheSession()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        fixture.Backend.BlockRefresh = true;
        Task<string?> refresh = fixture.Sessions.GetAccessTokenAsync(fixture.Id);
        await fixture.Backend.RefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task logout = fixture.Sessions.LogoutAsync(fixture.Id);
        fixture.Backend.ReleaseRefresh.TrySetResult();
        await Task.WhenAll(refresh, logout);

        Assert.Equal("new-refresh", fixture.Backend.LoggedOutToken);
        Assert.False(fixture.Tickets.IsValid(fixture.Id));
        Assert.Null(await fixture.Sessions.GetAccessTokenAsync(fixture.Id));
        Assert.Equal(1, fixture.Backend.RefreshCalls);
    }

    [Fact]
    public async Task CallerCancellationDoesNotAbandonAnInFlightSingleUseToken()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        fixture.Backend.BlockRefresh = true;
        using var cancellation = new CancellationTokenSource();
        Task<string?> refresh = fixture.Sessions.GetAccessTokenAsync(fixture.Id, cancellation.Token);
        await fixture.Backend.RefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        fixture.Backend.ReleaseRefresh.TrySetResult();
        Assert.Equal("new-access", await refresh);
        Assert.Equal("new-access", await fixture.Sessions.GetAccessTokenAsync(fixture.Id));
        Assert.Equal(1, fixture.Backend.RefreshCalls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task FailedRefreshInvalidatesSession(HttpStatusCode status)
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Backend.RefreshStatus = status;
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Null(await fixture.Sessions.GetAccessTokenAsync(fixture.Id));
        Assert.False(fixture.Tickets.IsValid(fixture.Id));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RefreshCannotChangeUserOrExtendAbsoluteExpiry(bool changeUser, bool extendExpiry)
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Backend.ChangeUser = changeUser;
        fixture.Backend.ExtendExpiry = extendExpiry;
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Null(await fixture.Sessions.GetAccessTokenAsync(fixture.Id));
        Assert.False(fixture.Tickets.IsValid(fixture.Id));
    }

    [Fact]
    public async Task RefreshAcceptsPostgresMicrosecondPrecisionWithoutExtendingSession()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Backend.TruncateTimestamp = true;
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal("new-access", await fixture.Sessions.GetAccessTokenAsync(fixture.Id));
        Assert.Equal(fixture.AbsoluteExpiry, fixture.Tickets.GetValidTicket(fixture.Id)!.Properties.ExpiresUtc);
    }

    [Fact]
    public async Task TransportFailureInvalidatesSessionWithoutRetryingRefresh()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Backend.Offline = true;
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Null(await fixture.Sessions.GetAccessTokenAsync(fixture.Id));
        fixture.Backend.Offline = false;
        Assert.Null(await fixture.Sessions.GetAccessTokenAsync(fixture.Id));
        Assert.Equal(0, fixture.Backend.RefreshCalls);
    }

    [Fact]
    public async Task ExpiredAbsoluteSessionNeverCallsRefresh()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Clock.Advance(TimeSpan.FromDays(8));
        Assert.Null(await fixture.Sessions.GetAccessTokenAsync(fixture.Id));
        Assert.Equal(0, fixture.Backend.RefreshCalls);
    }

    [Fact]
    public async Task LogoutClearsLocalSessionWhenBackendIsUnavailable()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Backend.Offline = true;
        await fixture.Sessions.LogoutAsync(fixture.Id);
        Assert.False(fixture.Tickets.IsValid(fixture.Id));
    }

    private sealed record Fixture(TestClock Clock, ServerTicketStore Tickets, Backend Backend,
        BackendSessionService Sessions, string Id, DateTimeOffset AbsoluteExpiry)
    {
        public static async Task<Fixture> CreateAsync()
        {
            var clock = new TestClock();
            var tickets = new ServerTicketStore(clock);
            var backend = new Backend(clock);
            var properties = new AuthenticationProperties { ExpiresUtc = backend.Expiry };
            properties.Items[ServerTicketStore.BackendTokenProperty] = "old-access";
            properties.Items[ServerTicketStore.RefreshTokenProperty] = "old-refresh";
            properties.Items[ServerTicketStore.AccessTokenExpiryProperty] = clock.GetUtcNow().AddMinutes(1).ToString("O");
            properties.Items[ServerTicketStore.RefreshTokenExpiryProperty] = backend.Expiry.ToString("O");
            properties.Items[ServerTicketStore.ClientIpProperty] = "::ffff:192.0.2.10";
            var principal = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, backend.UserId.ToString()),
                new Claim(ClaimTypes.Role, AdminRoles.SuperAdmin)], "test"));
            string id = await tickets.StoreAsync(new AuthenticationTicket(principal, properties, "test"));
            return new(clock, tickets, backend,
                new BackendSessionService(tickets, new ClientFactory(backend), clock, NullLogger<BackendSessionService>.Instance),
                id, properties.ExpiresUtc!.Value);
        }
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.Parse("2026-10-19T10:00:00.1234567Z", CultureInfo.InvariantCulture);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }

    private sealed class ClientFactory(Backend backend) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(backend, disposeHandler: false)
        { BaseAddress = new Uri("https://backend.test/"), Timeout = TimeSpan.FromSeconds(5) };
    }

    private sealed class Backend(TestClock clock) : HttpMessageHandler
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public DateTimeOffset Expiry { get; } = clock.GetUtcNow().AddDays(7);
        public HttpStatusCode RefreshStatus = HttpStatusCode.OK;
        public bool BlockRefresh, Offline, ChangeUser, ExtendExpiry, TruncateTimestamp;
        public int RefreshCalls;
        public string? LoggedOutToken, ForwardedIp;
        public TaskCompletionSource RefreshStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseRefresh = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (Offline) throw new HttpRequestException("offline");
            ForwardedIp = request.Headers.GetValues("X-Forwarded-For").Single();
            RefreshTokenRequest? body = await request.Content!.ReadFromJsonAsync<RefreshTokenRequest>(ct);
            if (request.RequestUri!.AbsolutePath.EndsWith("logout"))
            {
                LoggedOutToken = body!.RefreshToken;
                return new(HttpStatusCode.NoContent);
            }
            Interlocked.Increment(ref RefreshCalls);
            Assert.Equal("old-refresh", body!.RefreshToken);
            RefreshStarted.TrySetResult();
            if (BlockRefresh) await ReleaseRefresh.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            DateTimeOffset expiry = TruncateTimestamp
                ? new DateTimeOffset(Expiry.UtcTicks - Expiry.UtcTicks % TimeSpan.TicksPerMicrosecond, TimeSpan.Zero) : Expiry;
            return new(RefreshStatus) { Content = JsonContent.Create(new LoginResponse(
                "new-access", "Bearer", clock.GetUtcNow().AddMinutes(30),
                new AuthenticatedUserResponse(ChangeUser ? Guid.NewGuid() : UserId, "audit@example.test", [AdminRoles.LotAdmin], []),
                "new-refresh", ExtendExpiry ? expiry.AddDays(1) : expiry)) };
        }
    }
}
