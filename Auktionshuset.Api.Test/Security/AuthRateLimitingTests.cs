using System.Net;
using Auktionshuset.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Auktionshuset.Api.Test.Security;

public sealed class AuthRateLimitingTests
{
    [Fact]
    public async Task TrustedFrontendGetsSeparateLimitForEachRealClient()
    {
        using IHost host = await StartAsync(trustFrontend: true);
        using HttpClient client = host.GetTestClient();
        for (int i = 0; i < 6; i++)
        {
            using HttpResponseMessage response = await LoginAsync(client, "192.0.2.10");
            Assert.Equal(i < 5 ? HttpStatusCode.NoContent : HttpStatusCode.TooManyRequests, response.StatusCode);
            if (i == 5) Assert.NotNull(response.Headers.RetryAfter);
        }
        using HttpResponseMessage otherClient = await LoginAsync(client, "192.0.2.20");
        Assert.Equal(HttpStatusCode.NoContent, otherClient.StatusCode);
    }

    [Theory]
    [InlineData(false, "127.0.0.1")]
    [InlineData(true, "203.0.113.50")]
    public async Task UntrustedCallerCannotBypassLimitByChangingForwardedHeader(bool trustFrontend, string remoteAddress)
    {
        using IHost host = await StartAsync(trustFrontend);
        using HttpClient client = host.GetTestClient();
        for (int i = 0; i < 6; i++)
        {
            using HttpResponseMessage response = await LoginAsync(client, $"192.0.2.{i + 1}", remoteAddress);
            Assert.Equal(i < 5 ? HttpStatusCode.NoContent : HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string clientAddress, string remoteAddress = "127.0.0.1")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/login");
        request.Headers.Add("X-Forwarded-For", clientAddress);
        request.Headers.Add("Test-Remote-Address", remoteAddress);
        return client.SendAsync(request);
    }

    private static Task<IHost> StartAsync(bool trustFrontend)
    {
        var values = new Dictionary<string, string?>();
        if (trustFrontend) values["Authentication:KnownProxies:0"] = "127.0.0.1";
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(services =>
            {
                services.AddRouting(); services.AddAuthRateLimiting(configuration);
            });
            web.Configure(app =>
            {
                app.Use((context, next) =>
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers["Test-Remote-Address"]!);
                    return next();
                });
                app.UseForwardedHeaders(); app.UseRouting(); app.UseRateLimiter();
                app.UseEndpoints(endpoints => endpoints.MapPost("/login", () => Results.NoContent())
                    .RequireRateLimiting(AuthRateLimitingExtensions.Login));
            });
        }).StartAsync();
    }
}
