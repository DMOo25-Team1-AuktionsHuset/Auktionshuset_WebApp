using System.Collections.Concurrent;
using Microsoft.AspNetCore.DataProtection;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Auktionshuset.Contracts.Dto.Auth;
using Auktionshuset.Contracts.Dto.Customer.Request;
using Auktionshuset.Contracts.Dto.Customer.Response;
using Auktionshuset.Contracts.Dto.Billing;
using Auktionshuset.Contracts.Dto.Auction;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Auktionshuset.Tests.Auth;

public sealed class CustomerAreaFlowTests
{
    [Fact]
    public async Task CustomerAreaShowsOverviewNavigationAndProfile()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client, "kunde-ny@example.test", "correct");
        Assert.Equal("/kunde", RedirectPath(login));

        using HttpResponseMessage overview = await client.GetAsync("/kunde");
        Assert.Equal(HttpStatusCode.OK, overview.StatusCode);
        string overviewHtml = WebUtility.HtmlDecode(await overview.Content.ReadAsStringAsync());
        Assert.DoesNotContain("Kundeområde", overviewHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Igangværende auktioner", overviewHtml, StringComparison.Ordinal);
        Assert.Contains("Ingen auktioner i gang", overviewHtml, StringComparison.Ordinal);
        Assert.Contains("Ny Kunde", overviewHtml, StringComparison.Ordinal);
        Assert.Contains("href=\"kunde/profil\"", overviewHtml, StringComparison.Ordinal);
        Assert.Contains("href=\"kunde/mine-auktioner\"", overviewHtml, StringComparison.Ordinal);
        Assert.Contains("href=\"kunde/mine-koeb\"", overviewHtml, StringComparison.Ordinal);
        Assert.Contains("Log ud", overviewHtml, StringComparison.Ordinal);

        using HttpResponseMessage profile = await client.GetAsync("/kunde/profil");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        string profileHtml = await profile.Content.ReadAsStringAsync();
        Assert.Contains("value=\"Ny\"", profileHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"Kunde\"", profileHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"8888 8888\"", profileHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"Vestergade 1, 6000 Kolding\"", profileHtml, StringComparison.Ordinal);
        Assert.Contains("kunde-ny@example.test", profileHtml, StringComparison.Ordinal);
        Assert.Contains("Gem ændringer", profileHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnonymousUsersAreSentToLoginFromTheCustomerArea()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        foreach (string path in new[] { "/kunde", "/kunde/profil", "/kunde/mine-auktioner", "/kunde/mine-koeb" })
        {
            using HttpResponseMessage response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/login", RedirectPath(response));
        }
    }

    [Fact]
    public async Task AdministrativeUsersAreDeniedTheCustomerArea()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client, "configured-admin@example.test", "correct");
        Assert.Equal("/", RedirectPath(login));

        using HttpResponseMessage denied = await client.GetAsync("/kunde");

        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Equal("/?access=denied", RedirectPath(denied));
    }

    [Fact]
    public async Task MissingCustomerLinkShowsDanishErrorWithoutRedirectLoop()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client, "kunde-linkless@example.test", "correct");
        Assert.Equal("/kunde", RedirectPath(login));

        using HttpResponseMessage overview = await client.GetAsync("/kunde");

        // The page renders with a Danish error instead of redirecting, so a broken
        // account cannot create a redirect loop.
        Assert.Equal(HttpStatusCode.OK, overview.StatusCode);
        Assert.Contains("Du har ikke adgang til denne side",
            WebUtility.HtmlDecode(await overview.Content.ReadAsStringAsync()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CustomerCanEditOwnProfile()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, "kunde-ny@example.test", "correct");

        using HttpResponseMessage profilePage = await client.GetAsync("/kunde/profil");
        string profileHtml = await profilePage.Content.ReadAsStringAsync();
        (string Token, string Handler) form = ReadFormFields(profileHtml);
        using HttpResponseMessage saved = await client.PostAsync("/kunde/profil", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = form.Token,
                ["_handler"] = form.Handler,
                ["Model.FirstName"] = "Anna-Lise",
                ["Model.LastName"] = "Kunde-Pedersen",
                ["Model.PhoneNumber"] = "2222 2222",
                ["Model.Address"] = "Østergade 5, 6000 Kolding"
            }));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        string savedHtml = await saved.Content.ReadAsStringAsync();
        Assert.Contains("Dine oplysninger er gemt.", savedHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"Anna-Lise\"", savedHtml, StringComparison.Ordinal);

        UpdateCustomerProfileRequest? sent = factory.ProfileUpdates.Single();
        Assert.Equal("Anna-Lise", sent.FirstName);
        Assert.Equal("Kunde-Pedersen", sent.LastName);
        Assert.Equal("2222 2222", sent.PhoneNumber);
        Assert.Equal("Østergade 5, 6000 Kolding", sent.Address);
    }

    [Fact]
    public async Task ProfileFormValidatesWithDanishMessagesWithoutCallingTheApi()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, "kunde-ny@example.test", "correct");

        using HttpResponseMessage profilePage = await client.GetAsync("/kunde/profil");
        (string Token, string Handler) form = ReadFormFields(await profilePage.Content.ReadAsStringAsync());
        using HttpResponseMessage invalid = await client.PostAsync("/kunde/profil", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = form.Token,
                ["_handler"] = form.Handler,
                ["Model.FirstName"] = "",
                ["Model.LastName"] = "Kunde",
                ["Model.PhoneNumber"] = "ikke-et-nummer",
                ["Model.Address"] = "Vestergade 1, 6000 Kolding"
            }));

        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        string html = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("Fornavn skal udfyldes.", html, StringComparison.Ordinal);
        Assert.Contains("Indtast et gyldigt telefonnummer.", html, StringComparison.Ordinal);
        Assert.Empty(factory.ProfileUpdates);
    }

    [Fact]
    public async Task ProfileFormCannotChangeProtectedFields()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, "kunde-ny@example.test", "correct");

        using HttpResponseMessage profilePage = await client.GetAsync("/kunde/profil");
        (string Token, string Handler) form = ReadFormFields(await profilePage.Content.ReadAsStringAsync());
        using HttpResponseMessage saved = await client.PostAsync("/kunde/profil", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = form.Token,
                ["_handler"] = form.Handler,
                ["Model.FirstName"] = "Anna-Lise",
                ["Model.LastName"] = "Kunde",
                ["Model.PhoneNumber"] = "8888 8888",
                ["Model.Address"] = "Vestergade 1, 6000 Kolding",
                ["Model.Email"] = "hacker@example.test",
                ["Model.Role"] = "Admin",
                ["Model.CustomerId"] = Guid.NewGuid().ToString()
            }));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        UpdateCustomerProfileRequest? sent = factory.ProfileUpdates.Single();
        Assert.Equal("Anna-Lise", sent.FirstName);
        Assert.Equal("kunde-ny@example.test", factory.LastProfileEmail);
        // The request contract has no email, role, or customer id fields to abuse.
        Assert.DoesNotContain("Role", nameof(UpdateCustomerProfileRequest), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogoutRevokesAccessToCustomerAreaAndApi()
    {
        using var factory = new FrontendFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpResponseMessage login = await LoginAsync(client, "kunde-ny@example.test", "correct");
        Assert.Equal("/kunde", RedirectPath(login));

        string logoutToken = ReadAntiforgeryToken(await (await client.GetAsync("/kunde")).Content.ReadAsStringAsync());
        int apiCallsBeforeLogout = factory.ApiRequests.Count;
        using HttpResponseMessage logout = await client.PostAsync("/auth/logout", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = logoutToken }));
        Assert.Equal("/login", RedirectPath(logout));

        using HttpResponseMessage afterLogout = await client.GetAsync("/kunde");
        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
        Assert.Equal("/login", RedirectPath(afterLogout));
        Assert.Equal(apiCallsBeforeLogout, factory.ApiRequests.Count);
    }

    [Fact]
    public async Task RegisteredAuctionShowsLinkLotsAndBidForm()
    {
        using var factory = new FrontendFactory { ShowAuctions = true };
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, "kunde-ny@example.test", "correct");
        string html = await client.GetStringAsync("/kunde");
        Assert.Contains("Testauktion", html, StringComparison.Ordinal);
        Assert.Contains("Gå til auktion og byd", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        string bidding = WebUtility.HtmlDecode(await client.GetStringAsync($"/kunde/auktioner/{factory.AuctionId}"));
        Assert.Contains("Testauktion", bidding, StringComparison.Ordinal);
        Assert.Contains("Teststol", bidding, StringComparison.Ordinal);
        Assert.Contains("Dit bud for hele linjen i kr.", bidding, StringComparison.Ordinal);
        Assert.Contains("Afgiv bud", bidding, StringComparison.Ordinal);
        Assert.DoesNotContain("Kundeområde", bidding, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("125.50", true)]
    [InlineData("0", false)]
    public async Task BidFormPostTargetsSelectedLotAndValidatesAmount(string amount, bool valid)
    {
        var first = new CustomerAuctionLotResponse(Guid.NewGuid(), "Første stol", "En stol", 1, 100, 100, false, true);
        var second = new CustomerAuctionLotResponse(Guid.NewGuid(), "Anden stol", "En stol", 1, 100, 100, false, true);
        using var factory = new FrontendFactory { ShowAuctions = true, BidLots = [first, second] };
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, "kunde-ny@example.test", "correct");
        string path = $"/kunde/auktioner/{factory.AuctionId}";
        string html = await client.GetStringAsync(path);
        string[] forms = Regex.Matches(html, "<form\\b[^>]*>.*?</form>", RegexOptions.Singleline)
            .Select(match => match.Value).Where(form => form.Contains("Afgiv bud", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, forms.Length);
        var firstForm = ReadFormFields(forms[0]);
        var selected = ReadFormFields(forms[1]);
        Assert.NotEqual(firstForm.Handler, selected.Handler);
        using var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = selected.Token,
            ["_handler"] = selected.Handler,
            ["model.Amount"] = amount
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string result = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        if (valid)
        {
            var bid = Assert.Single(factory.Bids);
            Assert.Equal(second.AuctionLotId, bid.LotId);
            Assert.Equal(125.50m, bid.Request.Amount);
            Assert.NotEqual(Guid.Empty, bid.Request.RequestId);
            Assert.Contains("er modtaget", result);
        }
        else
        {
            Assert.Empty(factory.Bids);
            Assert.Contains("Indtast et gyldigt bud større end 0 kr.", result);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task UpcomingAuctionShowsRegistrationAndPreviewWithoutBidForm(bool pastStart, bool registered)
    {
        using var factory = new FrontendFactory { ShowAuctions = true, Upcoming = true, Registered = registered,
            StartsAt = pastStart ? new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc)
                : new DateTime(2099, 7, 15, 12, 0, 0, DateTimeKind.Utc) };
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, "kunde-ny@example.test", "correct");
        string overview = WebUtility.HtmlDecode(await client.GetStringAsync("/kunde"));
        Assert.Contains("Kommende auktioner", overview, StringComparison.Ordinal);
        Assert.Contains("Igangværende auktioner", overview, StringComparison.Ordinal);
        Assert.Contains("Ingen auktioner i gang", overview, StringComparison.Ordinal);
        Assert.Contains("Status: Kommende", overview, StringComparison.Ordinal);
        Assert.Contains(pastStart ? "15-01-2026 13:00" : "15-07-2099 14:00", overview, StringComparison.Ordinal);
        Assert.Contains(registered ? "Du er tilmeldt" : "Du er ikke tilmeldt", overview, StringComparison.Ordinal);
        Assert.Contains(registered ? "Se auktionens genstande" : "Tilmeld auktion", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("Gå til auktion og byd", overview, StringComparison.Ordinal);
        Assert.Equal(pastStart, overview.Contains("Starttidspunktet er passeret", StringComparison.Ordinal));
        if (!registered) return;
        string preview = WebUtility.HtmlDecode(await client.GetStringAsync($"/kunde/auktioner/{factory.AuctionId}"));
        Assert.Contains("Teststol", preview, StringComparison.Ordinal);
        Assert.Contains("Status: Kommende", preview, StringComparison.Ordinal);
        Assert.Contains("Du er tilmeldt", preview, StringComparison.Ordinal);
        Assert.Contains("auktionen er endnu ikke startet", preview, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(pastStart, preview.Contains("Starttidspunktet er passeret", StringComparison.Ordinal));
        Assert.DoesNotContain("Auktionen er afsluttet", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("Afgiv bud", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("Dit bud for hele linjen i kr.", preview, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, null, false, false, false, null, "Ingen bud")]
    [InlineData(true, 110, true, false, false, null, "Du fører")]
    [InlineData(true, 100, false, false, false, null, "Du er overbudt")]
    [InlineData(true, 110, true, true, true, 110, "Vundet")]
    [InlineData(true, 100, false, true, false, 110, "Tabt")]
    [InlineData(false, null, false, true, false, null, "Usolgt")]
    public async Task BidCardShowsOwnStatusAndSavedHammerPrice(bool bids, int? own, bool leading,
        bool closed, bool winner, int? hammer, string status)
    {
        using var factory = new FrontendFactory { ShowAuctions = true, ResultStatus = closed ? "Afsluttet" : "Afventer resultat",
            ResultLot = new CustomerAuctionLotResponse(Guid.NewGuid(), "Teststol", "En stol", 2, 100, 110,
                bids, false, own, leading, closed, winner, hammer) };
        using var client = factory.CreateClient();
        await LoginAsync(client, "kunde-ny@example.test", "correct");
        string html = WebUtility.HtmlDecode(await client.GetStringAsync($"/kunde/auktioner/{factory.AuctionId}"));
        Assert.Matches($"<strong[^>]*>{Regex.Escape(status)}</strong>", html);
        Assert.Contains("Dit højeste bud:", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Afgiv bud", html, StringComparison.Ordinal);
        Assert.Equal(hammer.HasValue, html.Contains("Hammerslagspris for hele linjen:", StringComparison.Ordinal));
        if (!closed)
        {
            Assert.Contains("Budperioden er afsluttet", html, StringComparison.Ordinal);
            Assert.Contains("Vinderen er endnu ikke fastlagt", html, StringComparison.Ordinal);
            Assert.DoesNotMatch("<strong[^>]*>Vundet</strong>", html);
        }
    }

    [Fact]
    public async Task CustomerHistoryKeepsClosedAuctionLinksAndShowsPurchaseQuantityAndHammerPrice()
    {
        using var factory = new FrontendFactory { ShowAuctions = true };
        using var client = factory.CreateClient();
        await LoginAsync(client, "kunde-ny@example.test", "correct");
        string history = WebUtility.HtmlDecode(await client.GetStringAsync("/kunde/mine-auktioner"));
        Assert.Contains("Mine auktioner", history, StringComparison.Ordinal);
        Assert.Contains("Status: Afsluttet", history, StringComparison.Ordinal);
        Assert.Contains($"kunde/auktioner/{factory.AuctionId}", history, StringComparison.Ordinal);
        string purchases = WebUtility.HtmlDecode(await client.GetStringAsync("/kunde/mine-koeb"));
        Assert.Contains("Mine køb", purchases, StringComparison.Ordinal);
        Assert.Contains("Vundet · Antal: 2", purchases, StringComparison.Ordinal);
        Assert.Contains("Hammerslagspris:", purchases, StringComparison.Ordinal);
        Assert.Contains("Faktura afventer afslutning og behandling af hele auktionen", purchases, StringComparison.Ordinal);
        Assert.Contains($"kunde/auktioner/{factory.AuctionId}", purchases, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PurchasesShowInvoiceTotalsAndPaidLineReceipts()
    {
        var now = DateTime.UtcNow;
        var line = new InvoiceLineResponse(Guid.NewGuid(), Guid.NewGuid(), "Historiske stole", "Tre samlet", 3,
            1000m, 100m, 200m, 20m, 1100m, now, "Betalt", null, null, null);
        var invoice = new InvoiceResponse(Guid.NewGuid(), "AH1-29-10-2026-000001", Guid.NewGuid(), "Afsluttet auktion",
            now, "Ny Kunde", "Historisk adresse", "kunde-ny@example.test", "Testhus", "Sælgervej", "seller@example.test",
            12345678, "DKK", "Kort eller kontant. Fuld betaling før afhentning.", 10m, 25m, 1000m, 100m, 220m, 1100m,
            "Betalt", now, "Card", 1100m, Guid.NewGuid(), "medarbejder@example.test", [line]);
        using var factory = new FrontendFactory { Invoice = invoice };
        using var client = factory.CreateClient();
        await LoginAsync(client, "kunde-ny@example.test", "correct");
        var purchases = WebUtility.HtmlDecode(await client.GetStringAsync("/kunde/mine-koeb"));
        Assert.Contains(invoice.Number, purchases);
        Assert.Contains("Samlet beløb inkl. moms:", purchases);
        Assert.Contains("Historiske stole · Antal: 3 · Betalt", purchases);
        Assert.Contains($"kunde/fakturaer/{invoice.Id}/kvittering/{line.Id}", purchases);
        var document = WebUtility.HtmlDecode(await client.GetStringAsync($"/kunde/fakturaer/{invoice.Id}"));
        Assert.Contains("Historisk adresse", document); Assert.Contains("Heraf moms:", document);
        Assert.Contains("Registreret af medarbejder@example.test", document);
        var receipt = WebUtility.HtmlDecode(await client.GetStringAsync($"/kunde/fakturaer/{invoice.Id}/kvittering/{line.Id}"));
        Assert.Contains("Betalingskvittering pr. auktionslinje", receipt);
        Assert.Contains("Betalt for denne linje:", receipt);
    }

    [Theory]
    [InlineData("/api/customer/invoices", false, "Fakturaer:")]
    [InlineData("/api/customer/invoices", true, "Fakturaer: Serveren svarede ikke")]
    [InlineData("/api/customer/purchases", false, "Køb:")]
    [InlineData("/api/customer/purchases", true, "Køb: Serveren svarede ikke")]
    public async Task PurchaseAndInvoiceFailuresShowSpecificRetryableError(string path, bool timeout, string expected)
    {
        using var factory = new FrontendFactory { ShowAuctions = true, FailedPath = path, Timeout = timeout };
        using var client = factory.CreateClient();
        await LoginAsync(client, "kunde-ny@example.test", "correct");
        string html = WebUtility.HtmlDecode(await client.GetStringAsync("/kunde/mine-koeb"));
        Assert.Contains(expected, html);
        Assert.Contains("Prøv igen / opdatér", html);
        if (path.EndsWith("invoices")) Assert.Contains("Teststol", html);
        Assert.Contains(factory.ApiRequests, r => r.Path == "/api/customer/purchases");
        Assert.Contains(factory.ApiRequests, r => r.Path == "/api/customer/invoices");
    }

    [Fact]
    public async Task EmptyPurchasesAndLiveFirstOverviewHaveClearStates()
    {
        using var factory = new FrontendFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "kunde-ny@example.test", "correct");
        string purchases = WebUtility.HtmlDecode(await client.GetStringAsync("/kunde/mine-koeb"));
        Assert.Contains("Du har ingen køb endnu", purchases);
        string overview = WebUtility.HtmlDecode(await client.GetStringAsync("/kunde"));
        Assert.True(overview.IndexOf("Igangværende auktioner", StringComparison.Ordinal) < overview.IndexOf("Kommende auktioner", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvoiceAdministrationAllowsEmployeesAndDeniesCustomers()
    {
        using var factory = new FrontendFactory();
        using var employee = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(employee, "employee@example.test", "correct");
        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync("/admin/invoices")).StatusCode);
        using var customer = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(customer, "kunde-ny@example.test", "correct");
        Assert.Equal(HttpStatusCode.Redirect, (await customer.GetAsync("/admin/invoices")).StatusCode);
        Assert.DoesNotContain(factory.ApiRequests, r => r.Email == "kunde-ny@example.test" && r.Path.StartsWith("/api/staff/"));
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

    private static (string Token, string Handler) ReadFormFields(string html)
    {
        Match token = Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"");
        Assert.True(token.Success, "The form should render an antiforgery token.");
        Match handler = Regex.Match(html, "name=\"_handler\" value=\"([^\"]+)\"");
        Assert.True(handler.Success, "The form should render a handler field.");
        return (WebUtility.HtmlDecode(token.Groups[1].Value), WebUtility.HtmlDecode(handler.Groups[1].Value));
    }

    private sealed class FrontendFactory : WebApplicationFactory<Program>
    {
        public ConcurrentQueue<(string Email, string Path)> ApiRequests { get; } = new();
        public ConcurrentQueue<UpdateCustomerProfileRequest> ProfileUpdates { get; } = new();
        public string? LastProfileEmail { get; set; }
        public bool ShowAuctions { get; set; }
        public bool Upcoming { get; set; }
        public bool Registered { get; set; } = true;
        public DateTime StartsAt { get; set; } = DateTime.UtcNow;
        public Guid AuctionId { get; } = Guid.NewGuid();
        public CustomerAuctionLotResponse? ResultLot { get; set; }
        public List<CustomerAuctionLotResponse>? BidLots { get; set; }
        public ConcurrentQueue<(Guid LotId, PlaceBidRequest Request)> Bids { get; } = new();
        public InvoiceResponse? Invoice { get; set; }
        public string? FailedPath { get; set; }
        public bool Timeout { get; set; }
        public string? ResultStatus { get; set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Api:BaseUrl"] = "https://backend.test/" }));
            builder.ConfigureTestServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddHttpClient("BackendAuthentication")
                    .ConfigurePrimaryHttpMessageHandler(() => new FakeAuthenticationHandler());
                services.AddHttpClient("BackendApi")
                    .ConfigurePrimaryHttpMessageHandler(() => new RecordingApiHandler(this));
            });
        }
    }

    private sealed class FakeAuthenticationHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LoginRequest? login = await request.Content!.ReadFromJsonAsync<LoginRequest>(cancellationToken);
            if (login?.Password == "wrong")
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            string email = login!.Email;
            string[] roles = email.StartsWith("kunde-", StringComparison.Ordinal) ? ["Customer"]
                : email switch
                {
                    "configured-admin@example.test" => ["Admin"],
                    _ => ["Employee"]
                };
            string[] permissions = email.StartsWith("kunde-", StringComparison.Ordinal) ? [] : ["lots.read"];
            var response = new LoginResponse(
                $"backend-jwt-{email}",
                "Bearer",
                DateTimeOffset.UtcNow.AddMinutes(30),
                new AuthenticatedUserResponse(Guid.NewGuid(), email, roles, permissions));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(response) };
        }
    }

    private sealed class RecordingApiHandler(FrontendFactory factory) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string token = request.Headers.Authorization?.Parameter ?? string.Empty;
            string owner = token.StartsWith("backend-jwt-", StringComparison.Ordinal)
                ? token["backend-jwt-".Length..]
                : string.Empty;
            string path = request.RequestUri?.PathAndQuery ?? string.Empty;
            factory.ApiRequests.Enqueue((owner, path));
            if (path == factory.FailedPath)
            {
                if (factory.Timeout) throw new TaskCanceledException("Simulated timeout");
                return new(HttpStatusCode.InternalServerError) { Content = JsonContent.Create(new { detail = "Databasen kunne ikke læses." }) };
            }
            if (request.Method == HttpMethod.Post && path.StartsWith("/api/customer/lots/", StringComparison.Ordinal) && path.EndsWith("/bids", StringComparison.Ordinal))
            {
                var bid = await request.Content!.ReadFromJsonAsync<PlaceBidRequest>(cancellationToken);
                factory.Bids.Enqueue((Guid.Parse(path.Split('/')[4]), bid!));
                return new(HttpStatusCode.OK);
            }
            if (factory.Invoice is { } invoice)
            {
                if (path == "/api/customer/invoices")
                    return new(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { invoice }) };
                if (path.StartsWith($"/api/customer/invoices/{invoice.Id}", StringComparison.Ordinal))
                    return new(HttpStatusCode.OK) { Content = JsonContent.Create(invoice) };
            }

            if (path.StartsWith("/api/customer/", StringComparison.Ordinal) && owner.StartsWith("kunde-linkless@", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = JsonContent.Create(new { detail = "Du har ikke adgang til denne side." }) };

            if (path.EndsWith("/api/customer/me", StringComparison.Ordinal))
            {
                if (owner.StartsWith("kunde-linkless@", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(HttpStatusCode.Forbidden)
                    {
                        Content = JsonContent.Create(new
                        {
                            title = "Adgang nægtet",
                            detail = "Du har ikke adgang til denne side. Kontakt auktionshuset, hvis problemet fortsætter."
                        })
                    };
                }

                if (request.Method == HttpMethod.Put)
                {
                    UpdateCustomerProfileRequest? update =
                        await request.Content!.ReadFromJsonAsync<UpdateCustomerProfileRequest>(cancellationToken);
                    factory.ProfileUpdates.Enqueue(update!);
                }

                factory.LastProfileEmail = owner;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new CustomerProfileResponse(
                        Guid.NewGuid(),
                        owner,
                        factory.ProfileUpdates.IsEmpty ? "Ny" : factory.ProfileUpdates.Last().FirstName,
                        factory.ProfileUpdates.IsEmpty ? "Kunde" : factory.ProfileUpdates.Last().LastName,
                        factory.ProfileUpdates.IsEmpty ? "8888 8888" : factory.ProfileUpdates.Last().PhoneNumber,
                        new DateOnly(1990, 3, 15),
                        factory.ProfileUpdates.IsEmpty ? "Vestergade 1, 6000 Kolding" : factory.ProfileUpdates.Last().Address))
                };
            }

            if (factory.ShowAuctions && path == "/api/customer/auctions")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] {
                    new CustomerAuctionResponse(factory.AuctionId, "Testauktion", factory.StartsAt, null, 1,
                        factory.Registered, factory.Upcoming ? "Kommende" : "Live") }) };
            if (factory.ShowAuctions && path == "/api/customer/my-auctions")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] {
                    new CustomerAuctionResponse(factory.AuctionId, "Testauktion", factory.StartsAt, null, 1, true, "Afsluttet") }) };
            if (factory.ShowAuctions && path == "/api/customer/purchases")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] {
                    new CustomerPurchaseResponse(factory.AuctionId, "Testauktion", Guid.NewGuid(), "Teststol", 2, 110, DateTime.UtcNow) }) };
            if (factory.ShowAuctions && path == $"/api/customer/auctions/{factory.AuctionId}")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(
                    new CustomerAuctionDetailResponse(factory.AuctionId, "Testauktion", !factory.Upcoming && factory.ResultStatus is null, factory.BidLots ?? [
                        factory.ResultLot ?? new CustomerAuctionLotResponse(Guid.NewGuid(), "Teststol", "En stol", 1, 100, 100, false, !factory.Upcoming)],
                        factory.StartsAt, factory.ResultStatus ?? (factory.Upcoming ? "Kommende" : "Live"))) };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<object>())
            };
        }
    }
}



