using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Auktionshuset.Contracts.Dto.Auth;
using Auktionshuset.Contracts.Dto.Customer.Request;
using Auktionshuset.Contracts.Dto.Customer.Response;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Auktionshuset.Tests.Auth;

public sealed class CustomerRegistrationFlowTests
{
    [Fact]
    public async Task RegistrationPageIsPublicAndLinkedFromLogin()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using HttpResponseMessage login = await client.GetAsync("/login");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("Opret ny bruger", await login.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using HttpResponseMessage register = await client.GetAsync("/registrer");
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        string html = await register.Content.ReadAsStringAsync();
        Assert.Contains("Opret kunde", html, StringComparison.Ordinal);
        Assert.Contains("Tilbage til login", html, StringComparison.Ordinal);
        Assert.Contains("Fornavn", html, StringComparison.Ordinal);
        Assert.Contains("Fødselsdato", html, StringComparison.Ordinal);
        Assert.Contains("Gentag adgangskode", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisteredCustomerIsConfirmedSentToLoginAndLandsOnCustomerArea()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using HttpResponseMessage created = await RegisterAsync(client, "kunde-ny@example.test");
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Assert.Equal("/login?oprettet=1", RedirectPath(created));

        using HttpResponseMessage loginPage = await client.GetAsync("/login?oprettet=1");
        string loginHtml = WebUtility.HtmlDecode(await loginPage.Content.ReadAsStringAsync());
        Assert.Contains("Din konto er oprettet. Log ind med din email og adgangskode.", loginHtml, StringComparison.Ordinal);

        using HttpResponseMessage login = await LoginAsync(client, "kunde-ny@example.test", "correct");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/kunde", RedirectPath(login));
        Assert.Contains(login.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("Auktionshuset.Session=", StringComparison.Ordinal));

        using HttpResponseMessage customerHome = await client.GetAsync("/kunde");
        Assert.Equal(HttpStatusCode.OK, customerHome.StatusCode);
        string customerHtml = WebUtility.HtmlDecode(await customerHome.Content.ReadAsStringAsync());
        Assert.Contains("Igangværende auktioner", customerHtml, StringComparison.Ordinal);
        Assert.Contains("kunde-ny@example.test", customerHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"warehouse\"", customerHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"admin/auctions\"", customerHtml, StringComparison.Ordinal);

        // An already signed-in customer is sent to the customer area, not the login form.
        using HttpResponseMessage loginWhileSignedIn = await client.GetAsync("/login");
        Assert.Equal(HttpStatusCode.Redirect, loginWhileSignedIn.StatusCode);
        Assert.Equal("/kunde", RedirectPath(loginWhileSignedIn));

        // The administration dashboard redirects customers to their own area.
        using HttpResponseMessage administration = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, administration.StatusCode);
        Assert.Equal("/kunde", RedirectPath(administration));
    }

    [Fact]
    public async Task RegistrationValidatesInputWithDanishMessages()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using HttpResponseMessage mismatch = await RegisterAsync(client, "kunde-ny@example.test",
            password: "adgangskode-123", confirmPassword: "anden-adgangskode");
        Assert.Equal(HttpStatusCode.Redirect, mismatch.StatusCode);
        Assert.Equal("/registrer?error=password", RedirectPath(mismatch));
        using HttpResponseMessage mismatchPage = await client.GetAsync("/registrer?error=password");
        Assert.Contains("Adgangskoderne er ikke ens, eller adgangskoden er under 8 tegn.",
            WebUtility.HtmlDecode(await mismatchPage.Content.ReadAsStringAsync()), StringComparison.Ordinal);

        using HttpResponseMessage invalidEmail = await RegisterAsync(client, "not-an-email");
        Assert.Equal(HttpStatusCode.Redirect, invalidEmail.StatusCode);
        Assert.Equal("/registrer?error=form", RedirectPath(invalidEmail));
        using HttpResponseMessage formPage = await client.GetAsync("/registrer?error=form");
        Assert.Contains("Tjek de indtastede oplysninger",
            WebUtility.HtmlDecode(await formPage.Content.ReadAsStringAsync()), StringComparison.Ordinal);

        using HttpResponseMessage shortPassword = await RegisterAsync(client, "kunde-ny@example.test",
            password: "kort", confirmPassword: "kort");
        Assert.Equal("/registrer?error=password", RedirectPath(shortPassword));
    }

    [Fact]
    public async Task DuplicateEmailShowsDanishError()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using HttpResponseMessage duplicate = await RegisterAsync(client, "taken@example.test");

        Assert.Equal(HttpStatusCode.Redirect, duplicate.StatusCode);
        Assert.Equal("/registrer?error=duplicate", RedirectPath(duplicate));
        using HttpResponseMessage page = await client.GetAsync("/registrer?error=duplicate");
        Assert.Contains("Der findes allerede en konto med denne emailadresse.",
            WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CustomerIsDeniedAdministrationPagesWithoutApiCalls()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client, "kunde-ny@example.test", "correct");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        int apiCallsBefore = factory.ApiRequests.Count;

        foreach (string path in new[] { "/warehouse", "/admin/auctions", "/admin/auctions/create", "/admin/employees" })
        {
            using HttpResponseMessage denied = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.Equal("/kunde?access=denied", RedirectPath(denied));
            Assert.DoesNotContain("Genstande i lageret", await denied.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        Assert.Equal(apiCallsBefore, factory.ApiRequests.Count);

        using HttpResponseMessage customerHome = await client.GetAsync("/kunde?access=denied");
        Assert.Equal(HttpStatusCode.OK, customerHome.StatusCode);
        Assert.Contains("Du har ikke rettighed", await customerHome.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdministrativeUserStillLandsOnAdministrationAfterRegistrationFeature()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using HttpResponseMessage login = await LoginAsync(client, "configured-admin@example.test", "correct");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/", RedirectPath(login));
        string dashboard = await (await client.GetAsync("/")).Content.ReadAsStringAsync();
        Assert.Contains("href=\"admin/employees\"", dashboard, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BackButtonToLoginKeepsTheCustomerSignedInWithoutErrors()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client, "kunde-ny@example.test", "correct");
        Assert.Equal("/kunde", RedirectPath(login));

        // Simulate the browser back button repeatedly returning to /login.
        for (int i = 0; i < 3; i++)
        {
            using HttpResponseMessage back = await client.GetAsync("/login");
            Assert.Equal(HttpStatusCode.Redirect, back.StatusCode);
            Assert.Equal("/kunde", RedirectPath(back));
            using HttpResponseMessage customerHome = await client.GetAsync("/kunde");
            Assert.Equal(HttpStatusCode.OK, customerHome.StatusCode);
            Assert.Contains("kunde-ny@example.test", await customerHome.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task BackButtonToLoginKeepsAnAdministratorSignedInWithoutErrors()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client, "configured-admin@example.test", "correct");
        Assert.Equal("/", RedirectPath(login));

        for (int i = 0; i < 3; i++)
        {
            using HttpResponseMessage back = await client.GetAsync("/login");
            Assert.Equal(HttpStatusCode.Redirect, back.StatusCode);
            Assert.Equal("/", RedirectPath(back));
            using HttpResponseMessage dashboard = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
            Assert.Contains("href=\"admin/employees\"", await dashboard.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task LoginPageIsUsableAgainAfterLogout()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client, "kunde-ny@example.test", "correct");
        Assert.Equal("/kunde", RedirectPath(login));

        string logoutToken = ReadAntiforgeryToken(await (await client.GetAsync("/kunde")).Content.ReadAsStringAsync());
        using HttpResponseMessage logout = await client.PostAsync("/auth/logout", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = logoutToken }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/login", RedirectPath(logout));

        using HttpResponseMessage loginPage = await client.GetAsync("/login");
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        string loginHtml = await loginPage.Content.ReadAsStringAsync();
        Assert.Contains("id=\"login-form\"", loginHtml, StringComparison.Ordinal);

        using HttpResponseMessage secondLogin = await LoginAsync(client, "kunde-ny@example.test", "correct");
        Assert.Equal(HttpStatusCode.Redirect, secondLogin.StatusCode);
        Assert.Equal("/kunde", RedirectPath(secondLogin));
    }

    private static async Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email,
        string password = "adgangskode-123",
        string confirmPassword = "adgangskode-123")
    {
        using HttpResponseMessage registerPage = await client.GetAsync("/registrer");
        Assert.Equal(HttpStatusCode.OK, registerPage.StatusCode);
        string token = ReadAntiforgeryToken(await registerPage.Content.ReadAsStringAsync());
        return await client.PostAsync("/auth/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["firstName"] = "Anna",
            ["lastName"] = "Andreasen",
            ["email"] = email,
            ["phoneNumber"] = "8888 8888",
            ["birthDate"] = "1990-03-15",
            ["address"] = "Vestergade 1, 6000 Kolding",
            ["password"] = password,
            ["confirmPassword"] = confirmPassword
        }));
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
        public ConcurrentQueue<(string Email, string Path)> ApiRequests { get; } = new();

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

    private sealed class FakeAuthenticationHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/api/auth/register")
            {
                RegisterCustomerRequest? registration =
                    await request.Content!.ReadFromJsonAsync<RegisterCustomerRequest>(cancellationToken);
                if (registration is null || registration.Email.StartsWith("taken@", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(HttpStatusCode.Conflict);
                }
                if (registration.Password != registration.ConfirmPassword || registration.Password.Length < 8)
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = JsonContent.Create(new RegisterCustomerResponse(Guid.NewGuid(), registration.Email))
                };
            }

            if (request.RequestUri?.AbsolutePath == "/api/customer/me")
            {
                string? token = request.Headers.Authorization?.Parameter;
                string owner = token?.StartsWith("backend-jwt-", StringComparison.Ordinal) == true
                    ? token["backend-jwt-".Length..]
                    : string.Empty;
                if (request.Method == HttpMethod.Put)
                {
                    UpdateCustomerProfileRequest? update =
                        await request.Content!.ReadFromJsonAsync<UpdateCustomerProfileRequest>(cancellationToken);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(CustomerProfile(owner, update))
                    };
                }

                if (owner.StartsWith("kunde-linkless@", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(HttpStatusCode.Forbidden)
                    {
                        Content = JsonContent.Create(new { title = "Adgang nægtet", detail = "Din konto har ikke en gyldig kobling til en kundeprofil. Kontakt auktionshuset, hvis problemet fortsætter." })
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(CustomerProfile(owner, null))
                };
            }

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
            string[] permissions = email.StartsWith("kunde-", StringComparison.Ordinal)
                ? []
                : email switch
                {
                    "configured-lot-admin@example.test" => ["lots.read", "lots.create", "lots.update", "lots.delete"],
                    "configured-auction-admin@example.test" =>
                    ["lots.read", "lots.create", "lots.update", "lots.delete", "auctions.read", "auctions.create", "auctions.update", "auctions.delete"],
                    "configured-admin@example.test" =>
                    ["lots.read", "lots.create", "lots.update", "lots.delete", "auctions.read", "auctions.create", "auctions.update", "auctions.delete", "employees.read", "employees.create", "employees.update", "employees.delete"],
                    "auction-no-lots@example.test" => ["auctions.read", "auctions.create"],
                    _ when email.StartsWith("auction-", StringComparison.Ordinal) =>
                        ["auctions.read", "auctions.create", "auctions.update", "auctions.delete", "lots.read"],
                    _ when email.StartsWith("api-", StringComparison.Ordinal) => ["auctions.read", "auctions.create", "lots.read"],
                    _ when email.StartsWith("warehouse-", StringComparison.Ordinal) => ["lots.read"],
                    _ => []
                };
            string[] roles = email.StartsWith("kunde-", StringComparison.Ordinal)
                ? ["Customer"]
                : email switch
                {
                    "configured-lot-admin@example.test" => ["LotAdmin"],
                    "configured-auction-admin@example.test" => ["AuctionAdmin"],
                    "configured-admin@example.test" => ["Admin"],
                    _ when email.StartsWith("auction-", StringComparison.Ordinal) => ["AuctionAdmin"],
                    _ => ["Employee"]
                };
            var response = new LoginResponse(
                $"backend-jwt-{email}",
                "Bearer",
                email == "expiring@example.test"
                    ? DateTimeOffset.UtcNow.AddMilliseconds(700)
                    : DateTimeOffset.UtcNow.AddMinutes(30),
                new AuthenticatedUserResponse(Guid.NewGuid(), email, roles, permissions));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(response) };
        }

        private static CustomerProfileResponse CustomerProfile(string email, UpdateCustomerProfileRequest? update) =>
            new(
                Guid.NewGuid(),
                email,
                update?.FirstName ?? "Ny",
                update?.LastName ?? "Kunde",
                update?.PhoneNumber ?? "8888 8888",
                new DateOnly(1990, 3, 15),
                update?.Address ?? "Vestergade 1, 6000 Kolding");
    }

    private sealed class RecordingApiHandler(ConcurrentQueue<(string Email, string Path)> requests) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string token = request.Headers.Authorization?.Parameter ?? string.Empty;
            string owner = token.StartsWith("backend-jwt-", StringComparison.Ordinal)
                ? token["backend-jwt-".Length..]
                : string.Empty;
            requests.Enqueue((owner, request.RequestUri?.PathAndQuery ?? string.Empty));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<object>())
            });
        }
    }
}

