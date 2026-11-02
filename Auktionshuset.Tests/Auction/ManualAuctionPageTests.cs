using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Auktionshuset.Contracts.Dto.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Auktionshuset.Tests.Auctions;

public class ManualAuctionPageTests
{
    [Theory]
    [InlineData("Kommende", -1, null, true, true)]
    [InlineData("Kommende", 1, null, true, false)]
    [InlineData("Kommende", -2, -1, true, false)]
    [InlineData("Kommende", -1, null, false, false)]
    [InlineData("Live", -1, null, true, false)]
    [InlineData("Live", -1, null, false, false)]
    [InlineData("Afsluttet", -1, null, true, false)]
    public async Task ButtonsFollowStoredStatusTimeAndPermissions(string status, int startHours, int? endHours, bool update, bool canStart)
    {
        using var factory = new Factory(status, startHours, endHours, update);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        string loginPage = await client.GetStringAsync("/login");
        string csrf = WebUtility.HtmlDecode(Regex.Match(loginPage, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value);
        using var login = await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = csrf, ["email"] = "admin@example.test", ["password"] = "correct" }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        using var response = await client.GetAsync($"/admin/auctions/{factory.Id}/run");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = Regex.Replace(WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()), "<!--[\\s\\S]*?-->", "");
        Assert.Contains("Testgenstand", html);
        Assert.Contains("Startpris for hele linjen", html);
        Assert.Contains("35,50", html);
        Assert.Contains("sælges samlet til én vinder", html);
        Assert.Contains("bud gælder hele linjen", html);
        Assert.Matches($"<strong[^>]*>{status}</strong>", html);
        Assert.Equal(update && status == "Kommende", html.Contains(">Start auktion</button>"));
        if (update && status == "Kommende")
        {
            var button = Regex.Match(html, "<button[^>]*>Start auktion</button>").Value;
            Assert.Equal(!canStart, button.Contains("disabled"));
        }
        Assert.Equal(update && status == "Live", html.Contains(">Afslut auktion</button>"));
        Assert.Equal(update && status == "Live", html.Contains(">Luk genstand</button>"));
        Assert.DoesNotContain("Ja, afslut auktion", html); // Confirmation is a separate step.
        Assert.Equal(!update, html.Contains("kun læseadgang"));
    }

    private sealed class Factory(string status, int startHours, int? endHours, bool update) : WebApplicationFactory<Program>
    {
        public Guid Id { get; } = Guid.NewGuid();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["Api:BaseUrl"] = "https://backend.test/" }));
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient("BackendAuthentication").ConfigurePrimaryHttpMessageHandler(() => new Handler(_ =>
                    new LoginResponse("test-token", "Bearer", DateTimeOffset.UtcNow.AddMinutes(30),
                        new AuthenticatedUserResponse(Guid.NewGuid(), "admin@example.test", ["AuctionAdmin"],
                            update ? ["auctions.read", "auctions.update"] : ["auctions.read"]))));
                services.AddHttpClient("BackendApi").ConfigurePrimaryHttpMessageHandler(() => new Handler(_ =>
                    new AuctionDetailResponse(Id, "Testauktion", status, DateTime.UtcNow.AddHours(startHours),
                        endHours is int end ? DateTime.UtcNow.AddHours(end) : null, null, "Ikke tildelt", 1, 1,
                        [new AuctionLotResponse(Guid.NewGuid(), "Testgenstand", "Møbler", 1, 100, null, Guid.NewGuid(),
                            status == "Live", 100, 0, null, status == "Afsluttet" ? DateTime.UtcNow : null, StartingPrice: 35.50m)])));
            });
        }
    }
    private sealed class Handler(Func<HttpRequestMessage, object> body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body(request)) });
    }
}
