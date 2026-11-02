using Auktionshuset.Api.Endpoints.Admin.CreateAuction;
using Auktionshuset.Api.Endpoints.Admin.Employee;
using Auktionshuset.Api.Endpoints.Admin.Lots;
using Auktionshuset.Api.Endpoints.Admin.Auction.CloseAuction;
using Auktionshuset.Api.Endpoints.Auth;
using Auktionshuset.Api.Endpoints.Customer;
using Auktionshuset.Api.Endpoints.Billing;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Api.Security;
using Auktionshuset.Api.Services;
using Auktionshuset.Contracts.Dto.Auth;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Service;
using Auktionshuset.Infrastructure.Service.Vault;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using VaultSharp;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public sealed class BrowserAcceptanceFactAttribute : FactAttribute
{
    public BrowserAcceptanceFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("AH_BROWSER_ACCEPTANCE") != "1")
            Skip = "Opt-in local browser host; see docs/customer-staff-acceptance.md.";
    }
}

public sealed partial class AuctionStockPostgresTests
{
    // Uses only the fixture's isolated database and a dedicated local RabbitMQ instance.
    // No Vault, production credentials or project database are used.
    [BrowserAcceptanceFact]
    public async Task BrowserAcceptanceHost()
    {
        var source = new NpgsqlConnectionStringBuilder(connectionString);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Default"] = "Warning",
            ["Database:Host"] = source.Host, ["Database:Port"] = source.Port.ToString(), ["Database:Name"] = source.Database,
            ["RabbitMQ:HostName"] = "127.0.0.1", ["RabbitMQ:Port"] = "55679",
            ["Authentication:BootstrapAdmin:Email"] = "admin@example.test",
            ["Authentication:BootstrapAdmin:Password"] = "test-password-123",
            ["Authentication:SigningKey"] = "isolated-browser-acceptance-key-at-least-32-bytes",
            ["Authentication:Issuer"] = "acceptance", ["Authentication:Audience"] = "acceptance"
        });
        builder.Services.AddProblemDetails();
        builder.Services.AddSignalR();
        builder.Services.AddSecurityServices(builder.Configuration);
        builder.Services.AddApiServices();
        builder.Services.AddInfrastructure(builder.Configuration, new DatabaseCredentials(source.Username!, source.Password!),
            new RabbitMqCredentials("codex", "codex-isolated-test"), new VaultClient(new VaultClientSettings("http://127.0.0.1:1", new VaultSharp.V1.AuthMethods.Token.TokenAuthMethodInfo("unused-acceptance-only"))));
        await using var app = builder.Build();
        string? disconnectFile = Environment.GetEnvironmentVariable("AH_BROWSER_DISCONNECT_FILE");
        app.Use(async (context, next) =>
        {
            if (disconnectFile is not null && File.Exists(disconnectFile)
                && context.Request.Path.StartsWithSegments("/hubs/customer-auctions"))
            { context.Response.StatusCode = 503; return; }
            await next(context);
        });
        app.UseRouting(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
        app.MapAuthEndpoints(); app.MapCustomerEndpoints(); app.MapInvoiceEndpoints();
        app.MapAuctionEndpoints(); app.MapEmployeeEndpoints(); app.MapLotEndpoints(); app.MapCloseAuctionLotEndpoint();
        app.MapHub<CustomerAuctionHub>("/hubs/customer-auctions", o => o.CloseOnAuthenticationExpiration = true).RequireAuthorization(SecurityPolicies.Customer);
        app.MapHub<AuctionHub>("/hubs/auction", o => o.CloseOnAuthenticationExpiration = true).RequireAuthorization(SecurityPolicies.CanViewAuctions);
        app.MapHub<EmployeeHub>("/hubs/employee", o => o.CloseOnAuthenticationExpiration = true).RequireAuthorization(SecurityPolicies.CanViewEmployees);
        app.MapHub<LotHub>("/hubs/lot", o => o.CloseOnAuthenticationExpiration = true).RequireAuthorization(SecurityPolicies.CanViewLots);
        Guid[] customerLogins;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            var registration = scope.ServiceProvider.GetRequiredService<CustomerRegistrationService>();
            foreach (var name in new[] { "first", "second" })
                await registration.RegisterAsync(new RegisterCustomerRequest { FirstName = name, LastName = "Accepttest",
                    Email = name + "@example.test", PhoneNumber = "12345678", BirthDate = new(1990, 1, 1), Address = "Testvej 1",
                    Password = "test-password-123", ConfirmPassword = "test-password-123" }, default);
            foreach (var role in new[] { "Admin", "AuctionAdmin", "LotAdmin", "Employee" })
            {
                var employee = new Employee { EmployeeId = Guid.NewGuid(), AuctionHouseId = houseId, FirstName = role, LastName = "Accepttest", Address = "Testvej", BirthDate = new(1990, 1, 1) };
                db.Employee.Add(employee);
                db.LoginUsers.Add(new LoginUser { UserId = Guid.NewGuid(), CustomerId = null, EmployeeId = employee.EmployeeId,
                    Email = role.ToLowerInvariant() + "-staff@example.test", PasswordHash = PersistentAuthUserStore.HashPassword(
                        scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<AuthUser>>(), "test-password-123"), Role = role });
            }
            foreach (bool live in new[] { true, false })
            {
                var auction = new Auction { AuctionId = Guid.NewGuid(), AuctionHouseId = houseId, Name = live ? "Live accepttest" : "Kommende accepttest",
                    AuctionStatus = live ? "Live" : "Kommende", StartsAt = DateTime.UtcNow.AddMinutes(live ? -60 : -1), StartedAtTime = live ? DateTime.UtcNow.AddHours(-1) : null };
                db.AuctionLot.Add(new AuctionLot { AuctionLotId = Guid.NewGuid(), Auction = auction, AuctionId = auction.AuctionId,
                    LotId = lotId, Quantity = 1, StartingPrice = 100, OpenForBids = live });
            }
            db.Employee.Add(new Employee { EmployeeId = Guid.NewGuid(), AuctionHouseId = houseId, FirstName = "Ny", LastName = "Medarbejder", Address = "Testvej", BirthDate = new(1990, 1, 1) });
            await db.SaveChangesAsync();
            customerLogins = await db.LoginUsers.Where(u => u.Role == "Customer").Select(u => u.UserId).ToArrayAsync();
        }
        app.Urls.Add("http://127.0.0.1:55133");
        await app.StartAsync();
        string? stopFile = Environment.GetEnvironmentVariable("AH_BROWSER_STOP_FILE");
        try
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            while (!limit.IsCancellationRequested && (stopFile is null || !File.Exists(stopFile)))
            {
                if (disconnectFile is not null && File.Exists(disconnectFile))
                    foreach (var id in customerLogins) app.Services.GetRequiredService<LoginConnections>().Disconnect(id);
                await Task.Delay(1000, limit.Token);
            }
        }
        finally { await app.StopAsync(); }
    }
}
