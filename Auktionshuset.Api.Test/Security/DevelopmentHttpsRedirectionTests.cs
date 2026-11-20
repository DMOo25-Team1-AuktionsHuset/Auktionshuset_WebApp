using System.Net;
using Auktionshuset.Api.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Auktionshuset.Api.Test.Security;

public sealed class DevelopmentHttpsRedirectionTests
{
    [Theory]
    [InlineData(true, "/ws/device", "127.0.0.1", "127.0.0.1", HttpStatusCode.NoContent)]
    [InlineData(true, "/ws/device", "::1", "::1", HttpStatusCode.NoContent)]
    [InlineData(true, "/ws/device", "::ffff:127.0.0.1", "::ffff:127.0.0.1", HttpStatusCode.NoContent)]
    [InlineData(true, "/ws/device", "192.0.2.1", "127.0.0.1", HttpStatusCode.TemporaryRedirect)]
    [InlineData(true, "/ws/device", "127.0.0.1", "192.0.2.1", HttpStatusCode.TemporaryRedirect)]
    [InlineData(true, "/ws/device", null, "127.0.0.1", HttpStatusCode.TemporaryRedirect)]
    [InlineData(true, "/ws/device", "127.0.0.1", null, HttpStatusCode.TemporaryRedirect)]
    [InlineData(false, "/ws/device", "127.0.0.1", "127.0.0.1", HttpStatusCode.TemporaryRedirect)]
    [InlineData(true, "/ws/device/other", "127.0.0.1", "127.0.0.1", HttpStatusCode.TemporaryRedirect)]
    [InlineData(true, "/ws/device/test", "192.0.2.1", "192.0.2.2", HttpStatusCode.NoContent)]
    [InlineData(false, "/ws/device/test", "127.0.0.1", "127.0.0.1", HttpStatusCode.TemporaryRedirect)]
    public async Task RedirectsOnlyOutsideExistingAndLocalDevelopmentExceptions(
        bool development, string path, string? remote, string? local, HttpStatusCode expected)
    {
        using IHost host = await new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(services => services.AddHttpsRedirection(options => options.HttpsPort = 7061));
            web.Configure(app =>
            {
                app.Use((context, next) =>
                {
                    context.Connection.RemoteIpAddress = remote is null ? null : IPAddress.Parse(remote);
                    context.Connection.LocalIpAddress = local is null ? null : IPAddress.Parse(local);
                    return next();
                });
                app.UseDevelopmentAwareHttpsRedirection(development);
                app.Run(context => { context.Response.StatusCode = 204; return Task.CompletedTask; });
            });
        }).StartAsync();
        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(path);
        Assert.Equal(expected, response.StatusCode);
    }
}
