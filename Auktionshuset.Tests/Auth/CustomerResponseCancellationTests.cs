using System.Net;
using System.Security.Claims;
using System.Text;
using Auktionshuset.Security;
using Auktionshuset.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Auktionshuset.Tests.Auth;

public sealed class CustomerResponseCancellationTests
{
    [Theory]
    [InlineData("my-auctions", false)]
    [InlineData("my-auctions", true)]
    [InlineData("purchases", false)]
    [InlineData("purchases", true)]
    [InlineData("auctions", false)]
    [InlineData("auctions", true)]
    [InlineData("invoices", false)]
    [InlineData("invoices", true)]
    public async Task CancellationAfterBufferedResponseStopsReadingTheBody(string resource, bool failedResponse)
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new BufferedResponseHandler(cancellation, failedResponse);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://backend.test/") };
        var clock = TimeProvider.System;
        var tickets = new ServerTicketStore(clock);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "customer")], "test"));
        var properties = new AuthenticationProperties { ExpiresUtc = clock.GetUtcNow().AddDays(1) };
        properties.Items[ServerTicketStore.BackendTokenProperty] = "access-token";
        properties.Items[ServerTicketStore.AccessTokenExpiryProperty] = clock.GetUtcNow().AddHours(1).ToString("O");
        await tickets.StoreAsync(new AuthenticationTicket(principal, properties, "test"));
        var context = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        var sessions = new BackendSessionService(tickets, new ClientFactory(handler), clock, NullLogger<BackendSessionService>.Instance);
        var accessor = new BackendTokenAccessor(new TestAuthenticationStateProvider(principal), context, sessions);
        using var backend = new BackendApiClient(http, accessor, new TestNavigationManager(), context, new FrontendRedirectState());
        var auctions = new CustomerAuctionService(backend);
        var invoices = new InvoiceService(backend);

        Task operation = resource switch
        {
            "my-auctions" => auctions.GetMyAuctionsAsync(cancellation.Token),
            "purchases" => auctions.GetPurchasesAsync(cancellation.Token),
            "auctions" => auctions.GetAuctionsAsync(cancellation.Token),
            "invoices" => invoices.GetAsync(cancellationToken: cancellation.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(resource))
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.True(handler.ResponseBuffered);
    }

    private sealed class BufferedResponseHandler(CancellationTokenSource cancellation, bool failedResponse) : HttpMessageHandler
    {
        public bool ResponseBuffered { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(failedResponse ? HttpStatusCode.BadRequest : HttpStatusCode.OK)
            {
                Content = new StringContent(failedResponse ? "{\"detail\":\"Rejected\"}" : "[]", Encoding.UTF8, "application/json")
            };
            // Finish transport first, so only forwarding cancellation to body parsing stops the operation.
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            ResponseBuffered = true;
            await cancellation.CancelAsync();
            return response;
        }
    }

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("https://backend.test/") };
    }

    private sealed class TestAuthenticationStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://frontend.test/", "https://frontend.test/");
    }
}
