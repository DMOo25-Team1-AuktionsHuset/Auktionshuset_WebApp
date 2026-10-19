using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Auktionshuset.Contracts.Dto.Auth;
using Auktionshuset.Contracts.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Auktionshuset.Tests.Auth;

public sealed class SessionRefreshIntegrationTests
{
    [Theory]
    [InlineData(AdminRoles.SuperAdmin)]
    [InlineData(AdminRoles.AuctionAdmin)]
    public async Task AuctionRolesCanSeeEditAndDeleteWithoutPermissionClaims(string role)
    {
        using var factory = new FrontendFactory(role);
        using HttpClient client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client);
        Assert.Equal("/", login.Headers.Location!.OriginalString);
        using HttpResponseMessage page = await client.GetAsync("/admin/auctions");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        string html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Audit auction", html);
        Assert.DoesNotContain("Ingen handlinger", html);
        Assert.Contains("Rediger", html);
        Assert.Contains("Slet", html);
        Assert.DoesNotContain("Du har ikke rettighed til at se lageret", html);
    }

    [Theory]
    [InlineData(AdminRoles.SuperAdmin)]
    [InlineData(AdminRoles.LotAdmin)]
    public async Task LotRolesCanSeeCreateFormWithoutPermissionClaims(string role)
    {
        using var factory = new FrontendFactory(role);
        using HttpClient client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client);
        using HttpResponseMessage page = await client.GetAsync("/warehouse");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("lot-form-heading", await page.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ExpiredAccessTokenIsRotatedOnPageReloadAndLogoutRevokesReplacement()
    {
        using var factory = new FrontendFactory(AdminRoles.AuctionAdmin);
        using HttpClient client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client);
        string cookie = string.Join(";", login.Headers.GetValues("Set-Cookie"));
        Assert.DoesNotContain("audit-access", cookie);
        Assert.DoesNotContain("audit-refresh", cookie);
        factory.Clock.Advance(TimeSpan.FromMinutes(31));

        using HttpResponseMessage page = await client.GetAsync("/admin/auctions");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(1, factory.Backend.RefreshCalls);
        Assert.Equal("audit-access-1", factory.Backend.LastAccessToken);
        string html = await page.Content.ReadAsStringAsync();
        using HttpResponseMessage logout = await client.PostAsync("/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = ReadAntiforgeryToken(html) }));
        Assert.Equal("/login", logout.Headers.Location!.OriginalString);
        Assert.Equal("audit-refresh-1", factory.Backend.LoggedOutToken);
        using HttpResponseMessage afterLogout = await client.GetAsync("/");
        Assert.Equal("/login", afterLogout.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task RefreshedRolesApplyBeforePageAuthorization()
    {
        using var factory = new FrontendFactory(AdminRoles.SuperAdmin);
        using HttpClient client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client);
        factory.Backend.Role = AdminRoles.LotAdmin;
        factory.Clock.Advance(TimeSpan.FromMinutes(31));
        using HttpResponseMessage denied = await client.GetAsync("/admin/auctions");
        Assert.Equal("/?access=denied", denied.Headers.Location!.OriginalString);
        using HttpResponseMessage allowed = await client.GetAsync("/warehouse");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task RejectedRefreshEndsCookieSession()
    {
        using var factory = new FrontendFactory(AdminRoles.AuctionAdmin);
        using HttpClient client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client);
        factory.Backend.RejectRefresh = true;
        factory.Clock.Advance(TimeSpan.FromMinutes(31));
        using HttpResponseMessage page = await client.GetAsync("/");
        Assert.Equal("/login", page.Headers.Location!.OriginalString);
        Assert.Equal(1, factory.Backend.RefreshCalls);
    }

    [Fact]
    public async Task AbsoluteRefreshExpiryEndsSessionWithoutAnotherRotation()
    {
        using var factory = new FrontendFactory(AdminRoles.AuctionAdmin);
        using HttpClient client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client);
        factory.Clock.Advance(TimeSpan.FromDays(8));
        using HttpResponseMessage page = await client.GetAsync("/");
        Assert.Equal("/login", page.Headers.Location!.OriginalString);
        Assert.Equal(0, factory.Backend.RefreshCalls);
    }

    [Fact]
    public async Task RateLimitedLoginShowsDanishError()
    {
        using var factory = new FrontendFactory(AdminRoles.SuperAdmin);
        using HttpClient client = factory.CreateClient(new() { AllowAutoRedirect = false });
        factory.Backend.LimitLogin = true;
        using HttpResponseMessage login = await LoginAsync(client);
        Assert.Equal("/login?error=rate-limited", login.Headers.Location!.OriginalString);
        using HttpResponseMessage page = await client.GetAsync(login.Headers.Location);
        Assert.Contains("for mange loginforsøg", WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task BrowserCannotSpoofForwardedAddressInBackendLogin()
    {
        using var factory = new FrontendFactory(AdminRoles.SuperAdmin);
        using HttpClient client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.99");
        using HttpResponseMessage login = await LoginAsync(client);
        Assert.Equal("192.0.2.10", factory.Backend.ForwardedIp);
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client)
    {
        using HttpResponseMessage page = await client.GetAsync("/login");
        return await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = ReadAntiforgeryToken(await page.Content.ReadAsStringAsync()),
            ["email"] = "audit@example.test", ["password"] = "audit-only-password"
        }));
    }

    private static string ReadAntiforgeryToken(string html)
    {
        Match match = Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"");
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }

    private sealed class FrontendFactory(string role) : WebApplicationFactory<Program>
    {
        public TestClock Clock { get; } = new();
        public Backend Backend { get; private set; } = null!;
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Backend = new Backend(Clock, role);
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Api:BaseUrl"] = "https://backend.test/" }));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(Clock);
                services.AddSingleton<IStartupFilter, RemoteAddressFilter>();
                services.AddHttpClient("BackendAuthentication").ConfigurePrimaryHttpMessageHandler(() => Backend);
                services.AddHttpClient("BackendApi").ConfigurePrimaryHttpMessageHandler(() => Backend);
            });
        }
    }

    private sealed class RemoteAddressFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
                return nextMiddleware();
            });
            next(app);
        };
    }

    private sealed class Backend(TestClock clock, string role) : HttpMessageHandler
    {
        private readonly Guid userId = Guid.NewGuid();
        private DateTimeOffset absoluteExpiry;
        public int RefreshCalls;
        public string Role = role;
        public bool RejectRefresh, LimitLogin;
        public string? LoggedOutToken, LastAccessToken, ForwardedIp;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri!.AbsolutePath.TrimEnd('/');
            if (path.StartsWith("/api/auth/"))
            {
                ForwardedIp = request.Headers.TryGetValues("X-Forwarded-For", out IEnumerable<string>? addresses) ? addresses.Single() : null;
                if (path.EndsWith("/login"))
                {
                    if (LimitLogin) return new(HttpStatusCode.TooManyRequests);
                    absoluteExpiry = clock.GetUtcNow().AddDays(7);
                }
                else
                {
                    RefreshTokenRequest? body = await request.Content!.ReadFromJsonAsync<RefreshTokenRequest>(ct);
                    if (path.EndsWith("/logout"))
                    {
                        LoggedOutToken = body!.RefreshToken;
                        return new(HttpStatusCode.NoContent);
                    }
                    Assert.Equal($"audit-refresh-{RefreshCalls}", body!.RefreshToken);
                    RefreshCalls++;
                    if (RejectRefresh) return new(HttpStatusCode.Unauthorized);
                }
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new LoginResponse(
                    $"audit-access-{RefreshCalls}", "Bearer", clock.GetUtcNow().AddMinutes(30),
                    new AuthenticatedUserResponse(userId, "audit@example.test", [Role], []),
                    $"audit-refresh-{RefreshCalls}", absoluteExpiry)) };
            }
            LastAccessToken = request.Headers.Authorization?.Parameter;
            object bodyContent = path == "/api/auctions"
                ? new[] { new AuctionListItemResponse(Guid.NewGuid(), "Audit auction", "Kladde", DateTime.UtcNow.AddDays(1),
                    DateTime.UtcNow.AddDays(2), null, "Audit employee", 0, 0, []) }
                : Array.Empty<object>();
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(bodyContent) };
        }
    }
}
