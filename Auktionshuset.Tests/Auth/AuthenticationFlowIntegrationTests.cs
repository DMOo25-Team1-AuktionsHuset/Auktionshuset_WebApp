using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Auktionshuset.Contracts.Dto.Auth;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Auktionshuset.Tests.Auth;

public sealed class AuthenticationFlowIntegrationTests
{
    [Fact]
    public async Task LoginEstablishesOpaqueCookieSessionThatSurvivesRefresh()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using HttpResponseMessage loginResponse = await LoginAsync(client, "basic@example.test", "correct");

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Equal("/", RedirectPath(loginResponse));
        string sessionCookie = Assert.Single(loginResponse.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("Auktionshuset.Session=", StringComparison.Ordinal));
        Assert.DoesNotContain("backend-jwt", sessionCookie, StringComparison.Ordinal);

        using HttpResponseMessage dashboard = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        string dashboardHtml = await dashboard.Content.ReadAsStringAsync();
        Assert.Contains("basic@example.test", dashboardHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("backend-jwt", dashboardHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"warehouse\"", dashboardHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"admin/auctions\"", dashboardHtml, StringComparison.Ordinal);

        using HttpResponseMessage loginRoute = await client.GetAsync("/login");
        Assert.Equal(HttpStatusCode.Redirect, loginRoute.StatusCode);
        Assert.Equal("/", RedirectPath(loginRoute));

        using HttpResponseMessage refresh = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    [Fact]
    public async Task UnavailableBackendShowsADanishLoginErrorWithoutCreatingASession()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using HttpResponseMessage login = await LoginAsync(client, "offline@example.test", "correct");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/login?error=unavailable", RedirectPath(login));
        using HttpResponseMessage loginPage = await client.GetAsync("/login?error=unavailable");
        string loginHtml = WebUtility.HtmlDecode(await loginPage.Content.ReadAsStringAsync());
        Assert.Contains("serveren ikke er tilgængelig", loginHtml, StringComparison.OrdinalIgnoreCase);
        using HttpResponseMessage protectedPage = await client.GetAsync("/");
        Assert.Equal("/login", RedirectPath(protectedPage));
    }

    [Fact]
    public async Task WrongPasswordDoesNotCreateSessionAndProtectedUrlsChallengeAnonymously()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using HttpResponseMessage failedLogin = await LoginAsync(client, "basic@example.test", "wrong");
        Assert.Equal(HttpStatusCode.Redirect, failedLogin.StatusCode);
        Assert.Equal("/login?error=invalid", RedirectPath(failedLogin));
        using HttpResponseMessage loginErrorPage = await client.GetAsync("/login?error=invalid");
        Assert.Contains("Email eller adgangskode er forkert", WebUtility.HtmlDecode(await loginErrorPage.Content.ReadAsStringAsync()), StringComparison.Ordinal);
        IEnumerable<string> failureCookies = failedLogin.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
            ? values
            : [];
        Assert.DoesNotContain(failureCookies,
            value => value.StartsWith("Auktionshuset.Session=", StringComparison.Ordinal));

        foreach (string path in new[] { "/", "/warehouse", "/admin/auctions", "/admin/auctions/create", "/admin/employees" })
        {
            using HttpResponseMessage response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/login", RedirectPath(response));
        }
    }

    [Fact]
    public async Task PermissionDenialIsHandledBeforeWarehouseRenderingOrDataLoading()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client, "basic@example.test", "correct");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        int before = factory.ApiRequests.Count;

        using HttpResponseMessage denied = await client.GetAsync("/warehouse");

        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Equal("/?access=denied", RedirectPath(denied));
        Assert.DoesNotContain("Genstande i lageret", await denied.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(before, factory.ApiRequests.Count);
        foreach (string path in new[] { "/admin/auctions", "/admin/auctions/create", "/admin/employees" })
        {
            using HttpResponseMessage adminDenied = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, adminDenied.StatusCode);
            Assert.Equal("/?access=denied", RedirectPath(adminDenied));
            Assert.Equal(before, factory.ApiRequests.Count);
        }

        using HttpResponseMessage dashboard = await client.GetAsync("/?access=denied");
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        Assert.Contains("Du har ikke rettighed", await dashboard.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WarehouseReadPermissionDoesNotExposeWriteActions()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client, "warehouse-viewer@example.test", "correct");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        using HttpResponseMessage page = await client.GetAsync("/warehouse");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        string html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Genstande i lageret", html, StringComparison.Ordinal);
        Assert.DoesNotContain("lot-form-heading", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Opret genstand", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Rediger", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Slet", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuctionCreationDoesNotFetchLotsWithoutLotReadPermission()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client, "auction-no-lots@example.test", "correct");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        using HttpResponseMessage page = await client.GetAsync("/admin/auctions");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        string html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Du har ikke rettighed til at se lageret", html, StringComparison.Ordinal);
        Assert.DoesNotContain(factory.ApiRequests, request =>
            request.Owner == "auction-no-lots@example.test" && request.Path.StartsWith("/api/lots", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("api-401@example.test", HttpStatusCode.Redirect, "/login")]
    [InlineData("api-403@example.test", HttpStatusCode.Redirect, "/?access=denied")]
    public async Task ApiAuthorizationResponsesRedirectWithoutCreatingLoops(
        string email,
        HttpStatusCode expectedStatus,
        string expectedLocation)
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client, email, "correct");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        using HttpResponseMessage response = await client.GetAsync("/admin/auctions");

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(expectedLocation, RedirectPath(response));
        Assert.DoesNotContain("Alle auktioner", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        if (email.StartsWith("api-401", StringComparison.Ordinal))
        {
            using HttpResponseMessage afterInvalidation = await client.GetAsync("/");
            Assert.Equal("/login", RedirectPath(afterInvalidation));
        }
        else
        {
            using HttpResponseMessage dashboard = await client.GetAsync(expectedLocation);
            Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        }
    }

    [Fact]
    public async Task ExpiredBackendTokenExpiresTheCookieSession()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client, "expiring@example.test", "correct");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        await Task.Delay(TimeSpan.FromMilliseconds(1200));

        using HttpResponseMessage expired = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, expired.StatusCode);
        Assert.Equal("/login", expired.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ApiCallsUseEachUsersJwtAndLogoutInvalidatesTheSession()
    {
        using var factory = new FrontendFactory();
        using HttpClient alice = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpClient bob = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpResponseMessage aliceLogin = await LoginAsync(alice, "auction-alice@example.test", "correct");
        using HttpResponseMessage bobLogin = await LoginAsync(bob, "auction-bob@example.test", "correct");
        Assert.Equal(HttpStatusCode.Redirect, aliceLogin.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, bobLogin.StatusCode);

        using HttpResponseMessage alicePage = await alice.GetAsync("/admin/auctions");
        using HttpResponseMessage bobPage = await bob.GetAsync("/admin/auctions");

        Assert.Equal(HttpStatusCode.OK, alicePage.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bobPage.StatusCode);
        Assert.Contains(factory.ApiRequests, request => request.Token == "backend-jwt-auction-alice@example.test");
        Assert.Contains(factory.ApiRequests, request => request.Token == "backend-jwt-auction-bob@example.test");
        Assert.DoesNotContain(factory.ApiRequests, request =>
            request.Token == "backend-jwt-auction-alice@example.test" && request.Owner == "auction-bob@example.test");

        string dashboard = await (await alice.GetAsync("/")).Content.ReadAsStringAsync();
        string logoutToken = ReadAntiforgeryToken(dashboard);
        using var missingTokenContent = new FormUrlEncodedContent(new Dictionary<string, string>());
        using HttpResponseMessage rejectedLogout = await alice.PostAsync("/auth/logout", missingTokenContent);
        Assert.Equal(HttpStatusCode.BadRequest, rejectedLogout.StatusCode);

        using var logoutContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = logoutToken
        });
        using HttpResponseMessage logout = await alice.PostAsync("/auth/logout", logoutContent);

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/login", RedirectPath(logout));
        int requestCountAtLogout = factory.ApiRequests.Count;
        using HttpResponseMessage afterLogout = await alice.GetAsync("/");
        Assert.Equal(requestCountAtLogout, factory.ApiRequests.Count);
        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
        Assert.Equal("/login", RedirectPath(afterLogout));
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
    {
        using HttpResponseMessage loginPage = await client.GetAsync("/login");
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        string token = ReadAntiforgeryToken(await loginPage.Content.ReadAsStringAsync());
        return await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["email"] = email,
            ["password"] = password
        }));
    }

    private static string? RedirectPath(HttpResponseMessage response)
    {
        Uri? location = response.Headers.Location;
        return location is { IsAbsoluteUri: true } ? location.PathAndQuery : location?.OriginalString;
    }

    private static string ReadAntiforgeryToken(string html)
    {
        Match match = Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"");
        Assert.True(match.Success, "The page should render an antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private sealed class FrontendFactory : WebApplicationFactory<Program>
    {
        public ConcurrentQueue<ApiRequest> ApiRequests { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Api:BaseUrl"] = "https://backend.test/" }));
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient("BackendAuthentication")
                    .ConfigurePrimaryHttpMessageHandler(() => new FakeAuthenticationHandler());
                services.AddHttpClient("BackendApi")
                    .ConfigurePrimaryHttpMessageHandler(() => new RecordingApiHandler(ApiRequests));
            });
        }
    }

    public sealed record ApiRequest(string Owner, string? Token, string Path);

    private sealed class FakeAuthenticationHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LoginRequest? login = await request.Content!.ReadFromJsonAsync<LoginRequest>(cancellationToken);
            if (login?.Password == "wrong")
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
            if (login?.Email == "offline@example.test")
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            string email = login!.Email;
            string[] permissions = email.StartsWith("auction-", StringComparison.Ordinal)
                ? email == "auction-no-lots@example.test" ? ["auctions.create"] : ["auctions.create", "lots.read"]
                : email.StartsWith("api-", StringComparison.Ordinal)
                    ? ["auctions.create", "lots.read"]
                    : email.StartsWith("warehouse-", StringComparison.Ordinal) ? ["lots.read"] : [];
            var response = new LoginResponse(
                $"backend-jwt-{email}",
                "Bearer",
                email == "expiring@example.test"
                    ? DateTimeOffset.UtcNow.AddMilliseconds(700)
                    : DateTimeOffset.UtcNow.AddMinutes(30),
                new AuthenticatedUserResponse(Guid.NewGuid(), email, [], permissions));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(response) };
        }
    }

    private sealed class RecordingApiHandler(ConcurrentQueue<ApiRequest> requests) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string token = request.Headers.Authorization?.Parameter ?? string.Empty;
            string owner = token.StartsWith("backend-jwt-", StringComparison.Ordinal)
                ? token["backend-jwt-".Length..]
                : string.Empty;
            requests.Enqueue(new ApiRequest(owner, request.Headers.Authorization?.Parameter, request.RequestUri?.PathAndQuery ?? string.Empty));
            HttpStatusCode statusCode = owner.StartsWith("api-401", StringComparison.Ordinal)
                ? HttpStatusCode.Unauthorized
                : owner.StartsWith("api-403", StringComparison.Ordinal)
                    ? HttpStatusCode.Forbidden
                    : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = JsonContent.Create(Array.Empty<object>())
            });
        }
    }
}
