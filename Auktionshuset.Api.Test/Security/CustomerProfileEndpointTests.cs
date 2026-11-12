using Auktionshuset.Api.Endpoints.Admin.Bid;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Auktionshuset.Api.Endpoints.Admin.CreateAuction;
using Auktionshuset.Api.Endpoints.Admin.Employee;
using Auktionshuset.Api.Endpoints.Admin.Lots;
using Auktionshuset.Api.Endpoints.Auth;
using Auktionshuset.Api.Endpoints.Customer;
using Auktionshuset.Api.Security;
using Auktionshuset.Api.Services;
using Auktionshuset.Api.Test;
using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Auth;
using Auktionshuset.Contracts.Dto.Customer.Request;
using Auktionshuset.Contracts.Dto.Customer.Response;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Service;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Auktionshuset.Api.Test.Security;

public sealed partial class CustomerProfileEndpointTests
{
    [Fact]
    public async Task RegisteredCustomerCanReadOwnProfile()
    {
        using TemporarySqliteDatabase database = new();
        using IHost host = await StartServerAsync(database.Path);
        (HttpClient client, string token, RegisterCustomerResponse registration) = await CreateSignedInCustomerAsync(host);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await client.GetAsync("/api/customer/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CustomerProfileResponse profile = (await response.Content.ReadFromJsonAsync<CustomerProfileResponse>())!;
        Assert.Equal(registration.CustomerId, profile.CustomerId);
        Assert.Equal("kunde@example.test", profile.Email);
        Assert.Equal("Anna", profile.FirstName);
        Assert.Equal("Andreasen", profile.LastName);
        Assert.Equal("8888 8888", profile.PhoneNumber);
        Assert.Equal(new DateOnly(1990, 3, 15), profile.BirthDate);
        Assert.Equal("Vestergade 1, 6000 Kolding", profile.Address);
    }

    [Fact]
    public async Task RegisteredCustomerCanUpdateOwnProfile()
    {
        using TemporarySqliteDatabase database = new();
        using IHost host = await StartServerAsync(database.Path);
        (HttpClient client, string token, RegisterCustomerResponse registration) = await CreateSignedInCustomerAsync(host);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var update = new UpdateCustomerProfileRequest
        {
            FirstName = "Anna-Lise",
            LastName = "Andreasen-Pedersen",
            PhoneNumber = "2222 2222",
            Address = "Østergade 5, 6000 Kolding"
        };
        using HttpResponseMessage updated = await client.PutAsJsonAsync("/api/customer/me", update);

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        CustomerProfileResponse profile = (await updated.Content.ReadFromJsonAsync<CustomerProfileResponse>())!;
        Assert.Equal(registration.CustomerId, profile.CustomerId);
        Assert.Equal("kunde@example.test", profile.Email);
        Assert.Equal("Anna-Lise", profile.FirstName);
        Assert.Equal("Andreasen-Pedersen", profile.LastName);
        Assert.Equal("2222 2222", profile.PhoneNumber);
        Assert.Equal("Østergade 5, 6000 Kolding", profile.Address);

        using HttpResponseMessage readBack = await client.GetAsync("/api/customer/me");
        CustomerProfileResponse reloaded = (await readBack.Content.ReadFromJsonAsync<CustomerProfileResponse>())!;
        Assert.Equal("Anna-Lise", reloaded.FirstName);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        AHDBContext db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
        Auktionshuset.Domain.Entities.Customer stored = await db.Customers.AsNoTracking()
            .SingleAsync(entity => entity.CustomerId == registration.CustomerId);
        Assert.Equal("Anna-Lise", stored.FirstName);
        Assert.Equal("kunde@example.test", stored.Email);
    }

    [Fact]
    public async Task ProfileEndpointsChallengeAnonymousRequests()
    {
        using TemporarySqliteDatabase database = new();
        using IHost host = await StartServerAsync(database.Path);
        using HttpClient client = host.GetTestClient();

        using HttpResponseMessage read = await client.GetAsync("/api/customer/me");
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        using HttpResponseMessage write = await client.PutAsJsonAsync("/api/customer/me", ValidUpdate());
        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
    }

    [Fact]
    public async Task AdministrativeUsersAreForbiddenOnCustomerEndpoints()
    {
        using TemporarySqliteDatabase database = new();
        using IHost host = await StartServerAsync(database.Path);
        using HttpClient client = host.GetTestClient();
        string adminToken = CreateToken(host, SecurityRoles.Admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using HttpResponseMessage read = await client.GetAsync("/api/customer/me");
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        using HttpResponseMessage write = await client.PutAsJsonAsync("/api/customer/me", ValidUpdate());
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task CustomerTokenWithoutCustomerLinkIsForbidden()
    {
        using TemporarySqliteDatabase database = new();
        using IHost host = await StartServerAsync(database.Path);
        using HttpClient client = host.GetTestClient();
        // A token with the Customer role but no stored login user (no customer link).
        string token = CreateToken(host, SecurityRoles.Customer);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage read = await client.GetAsync("/api/customer/me");
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        string body = await read.Content.ReadAsStringAsync();
        Assert.Contains("Adgang nægtet", body, StringComparison.Ordinal);
        Assert.Contains("kobling til en kundeprofil", body, StringComparison.Ordinal);

        using HttpResponseMessage write = await client.PutAsJsonAsync("/api/customer/me", ValidUpdate());
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Theory]
    [InlineData("firstName", "", "Fornavn skal udfyldes.")]
    [InlineData("phoneNumber", "123", "Indtast et gyldigt telefonnummer.")]
    [InlineData("address", "", "Adresse skal udfyldes.")]
    public async Task ProfileUpdateRejectsInvalidInputWithDanishMessages(
        string changedField, string value, string expectedMessage)
    {
        using TemporarySqliteDatabase database = new();
        using IHost host = await StartServerAsync(database.Path);
        (HttpClient client, string token, _) = await CreateSignedInCustomerAsync(host);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        UpdateCustomerProfileRequest request = ValidUpdate();
        switch (changedField)
        {
            case "firstName": request.FirstName = value; break;
            case "phoneNumber": request.PhoneNumber = value; break;
            case "address": request.Address = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(changedField));
        }

        using HttpResponseMessage response = await client.PutAsJsonAsync("/api/customer/me", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedMessage, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileUpdateIgnoresProtectedFields()
    {
        using TemporarySqliteDatabase database = new();
        using IHost host = await StartServerAsync(database.Path);
        (HttpClient client, string token, RegisterCustomerResponse registration) = await CreateSignedInCustomerAsync(host);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Dictionary<string, object?> payload = new()
        {
            ["firstName"] = "Anna",
            ["lastName"] = "Andreasen",
            ["phoneNumber"] = "8888 8888",
            ["address"] = "Vestergade 1, 6000 Kolding",
            ["email"] = "hacker@example.test",
            ["role"] = "Admin",
            ["permissions"] = new[] { "employees.read" },
            ["customerId"] = Guid.NewGuid(),
            ["userId"] = Guid.NewGuid()
        };
        using HttpResponseMessage response = await client.PutAsJsonAsync("/api/customer/me", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CustomerProfileResponse profile = (await response.Content.ReadFromJsonAsync<CustomerProfileResponse>())!;
        Assert.Equal("kunde@example.test", profile.Email);
        Assert.Equal(registration.CustomerId, profile.CustomerId);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        AHDBContext db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
        Auktionshuset.Domain.Entities.Customer stored = await db.Customers.AsNoTracking()
            .SingleAsync(entity => entity.CustomerId == registration.CustomerId);
        Assert.Equal("kunde@example.test", stored.Email);
        Auktionshuset.Domain.Entities.LoginUser loginUser = await db.LoginUsers.AsNoTracking().SingleAsync();
        Assert.Equal(SecurityRoles.Customer, loginUser.Role);
    }

    [Fact]
    public async Task CustomerCannotAccessAnotherCustomersProfile()
    {
        using TemporarySqliteDatabase database = new();
        using IHost host = await StartServerAsync(database.Path);
        (HttpClient anna, string annaToken, RegisterCustomerResponse annaRegistration) =
            await CreateSignedInCustomerAsync(host, "kunde@example.test");

        using HttpClient anders = host.GetTestClient();
        await anders.PostAsJsonAsync("/api/auth/register", ValidRequest("kunde-anders@example.test"));
        using HttpResponseMessage andersLogin = await anders.PostAsJsonAsync("/api/auth/login", new
        {
            email = "kunde-anders@example.test",
            password = "adgangskode-123"
        });
        LoginResponse andersSession = (await andersLogin.Content.ReadFromJsonAsync<LoginResponse>())!;

        // Anna updates her own profile.
        anna.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", annaToken);
        UpdateCustomerProfileRequest annaUpdate = ValidUpdate();
        annaUpdate.FirstName = "Anna-Lise";
        using HttpResponseMessage updated = await anna.PutAsJsonAsync("/api/customer/me", annaUpdate);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        // There is no endpoint that takes a customer id; a manipulated URL is not mapped.
        anders.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", andersSession.AccessToken);
        using HttpResponseMessage byId = await anders.GetAsync($"/api/customer/{annaRegistration.CustomerId}");
        Assert.Equal(HttpStatusCode.NotFound, byId.StatusCode);

        // Anders still sees and keeps his own data after Anna's update.
        using HttpResponseMessage andersProfile = await anders.GetAsync("/api/customer/me");
        Assert.Equal(HttpStatusCode.OK, andersProfile.StatusCode);
        CustomerProfileResponse profile = (await andersProfile.Content.ReadFromJsonAsync<CustomerProfileResponse>())!;
        Assert.Equal("kunde-anders@example.test", profile.Email);
        Assert.Equal("Anna", profile.FirstName);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        AHDBContext db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
        Auktionshuset.Domain.Entities.Customer andersStored = await db.Customers.AsNoTracking()
            .SingleAsync(entity => entity.Email == "kunde-anders@example.test");
        Assert.Equal("Anna", andersStored.FirstName);
    }

    private static UpdateCustomerProfileRequest ValidUpdate() => new()
    {
        FirstName = "Anna",
        LastName = "Andreasen",
        PhoneNumber = "8888 8888",
        Address = "Vestergade 1, 6000 Kolding"
    };

    private static Auktionshuset.Contracts.Dto.Auth.RegisterCustomerRequest ValidRequest(string email = "kunde@example.test") => new()
    {
        FirstName = "Anna",
        LastName = "Andreasen",
        Email = email,
        PhoneNumber = "8888 8888",
        BirthDate = new DateOnly(1990, 3, 15),
        Address = "Vestergade 1, 6000 Kolding",
        Password = "adgangskode-123",
        ConfirmPassword = "adgangskode-123"
    };

    private static async Task<(HttpClient Client, string Token, RegisterCustomerResponse Registration)>
        CreateSignedInCustomerAsync(IHost host, string email = "kunde@example.test")
    {
        HttpClient client = host.GetTestClient();
        using HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/auth/register", ValidRequest(email));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        RegisterCustomerResponse registration =
            (await created.Content.ReadFromJsonAsync<RegisterCustomerResponse>())!;
        using HttpResponseMessage login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "adgangskode-123"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        LoginResponse session = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        return (client, session.AccessToken, registration);
    }

    private static string CreateToken(IHost host, string role)
    {
        IAccessTokenService tokenService = host.Services.GetRequiredService<IAccessTokenService>();
        var user = host.Services.GetRequiredService<ConfiguredAuthUserStore>()
            .FindByEmailAsync($"{role.ToLowerInvariant()}-role@example.test", default).GetAwaiter().GetResult()!;
        return tokenService.Issue(user).Value;
    }

    private static IConfiguration TestConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:BootstrapAdmin:Email"] = "admin@example.test",
            ["Authentication:BootstrapAdmin:Password"] = "test-password",
            ["Authentication:SigningKey"] = "test-signing-key-with-at-least-32-characters",
            ["Authentication:Issuer"] = "Auktionshuset.Api.Test",
            ["Authentication:Audience"] = "Auktionshuset.Client.Test",
            ["UsbStation:StationId"] = "11111111-1111-1111-1111-111111111111",
            ["UsbStation:ApprovedOrigin"] = "https://staff.example.test",
            ["UsbStation:GrantKeyHex"] = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF",
                ["Authentication:Users:0:Email"] = "admin-role@example.test",
                ["Authentication:Users:0:Password"] = "test-password",
                ["Authentication:Users:0:Role"] = "Admin",
                ["Authentication:Users:1:Email"] = "auctionadmin-role@example.test",
                ["Authentication:Users:1:Password"] = "test-password",
                ["Authentication:Users:1:Role"] = "AuctionAdmin",
                ["Authentication:Users:2:Email"] = "lotadmin-role@example.test",
                ["Authentication:Users:2:Password"] = "test-password",
                ["Authentication:Users:2:Role"] = "LotAdmin",
                ["Authentication:Users:3:Email"] = "employee-role@example.test",
                ["Authentication:Users:3:Password"] = "test-password",
                ["Authentication:Users:3:Role"] = "Employee",
                ["Authentication:Users:4:Email"] = "customer-role@example.test",
                ["Authentication:Users:4:Password"] = "test-password",
                ["Authentication:Users:4:Role"] = "Customer"
        })
        .Build();

    private sealed class TemporarySqliteDatabase : IDisposable
    {
        public TemporarySqliteDatabase()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"auction-profile-{Guid.NewGuid():N}.db");
        }

        public string Path { get; }

        public void Dispose()
        {
            try { System.IO.File.Delete(Path); }
            catch (IOException) { }
        }
    }

    private static async Task<IHost> StartServerAsync(string databasePath)
    {
        IConfiguration configuration = TestConfiguration();
        IHost host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<IPlaceBidStore, CustomerAuctionBidStore>();
                    services.AddApiServices();
                    services.AddScoped<Auktionshuset.Infrastructure.Service.Billing.AuctionInvoiceStore>();
                    services.AddSingleton(TimeProvider.System);
                    services.AddScoped<Auktionshuset.Infrastructure.Repositories.PostgresDeviceRepository>();
                    services.AddScoped<Auktionshuset.Application.Abstraction.Auth.IDeviceRepository>(s => s.GetRequiredService<Auktionshuset.Infrastructure.Repositories.PostgresDeviceRepository>());
                    services.AddScoped<Auktionshuset.Application.Abstraction.Auth.IDeviceAssignmentRepository>(s => s.GetRequiredService<Auktionshuset.Infrastructure.Repositories.PostgresDeviceRepository>());
                    services.AddScoped<Auktionshuset.Application.Abstraction.Auth.IDeviceSecretService, Auktionshuset.Infrastructure.Service.Auth.DeviceSecretService>();
                    services.AddSignalR();
                    services.AddDbContext<AHDBContext>(options =>
                        options.UseSqlite($"Data Source={databasePath}"));
                    services.AddSingleton(configuration);
                    services.AddSecurityServices(configuration);
                    services.AddSingleton<IRefreshTokenStore, TestRefreshTokenStore>();
                    services.AddSingleton<IAuctionRepository, InMemoryAuctionRepository>();
                    services.AddSingleton<IEmployeeRepository, InMemoryEmployeeRepository>();
                    services.AddSingleton<ILotRepository, InMemoryLotRepository>();
                    services.AddSingleton<IIntegrationEventPublisher, RecordingEventPublisher>();
                    services.AddSingleton<IOutboxWriter, RecordingEventPublisher>();
                    services.AddSingleton<IUnitOfWork, RecordingUnitOfWork>();
                    services.AddSingleton<IAuctionWriteSessionFactory, RecordingAuctionWriteSessionFactory>();
                    services.AddSingleton<ILotImageStore>(_ => new TestLotImageStore());
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapAuthEndpoints();
                        endpoints.MapCustomerEndpoints();
                        endpoints.MapHub<Auktionshuset.Api.Hubs.CustomerAuctionHub>("/hubs/customer-auctions")
                            .RequireAuthorization(SecurityPolicies.Customer);
                        Auktionshuset.Api.Endpoints.Billing.InvoiceEndpoints.MapInvoiceEndpoints(endpoints);
                        Auktionshuset.Api.Endpoints.Device.DeviceAdministrationEndpoints.MapDeviceAdministrationEndpoints(endpoints);
                        endpoints.MapBidEndpoints();
                        endpoints.MapEmployeeEndpoints();
                        endpoints.MapLotEndpoints();
                        endpoints.MapAuctionEndpoints();
                    });
                });
            })
            .StartAsync();

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        AHDBContext db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
        await db.Database.EnsureCreatedAsync();
        return host;
    }

    private sealed class TestRefreshTokenStore : IRefreshTokenStore
    {
        private readonly Dictionary<string, RefreshToken> tokens = [];

        public Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
        {
            tokens.Add(token.TokenHash, token);
            return Task.CompletedTask;
        }

        public Task<RefreshToken?> FindAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(tokens.GetValueOrDefault(tokenHash));

        public Task<RefreshRotationResult> RotateAsync(string tokenHash, RefreshToken replacement,
            DateTimeOffset now, CancellationToken cancellationToken)
        {
            if (!tokens.TryGetValue(tokenHash, out RefreshToken? current) || current.ExpiresAt <= now)
                return Task.FromResult(RefreshRotationResult.Invalid);
            if (current.RevokedAt is not null)
                return Task.FromResult(RefreshRotationResult.Replay);
            current.RevokedAt = now;
            current.ReplacedByTokenId = replacement.Id;
            tokens.Add(replacement.TokenHash, replacement);
            return Task.FromResult(RefreshRotationResult.Rotated);
        }

        public Task RevokeFamilyAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
        {
            if (tokens.TryGetValue(tokenHash, out RefreshToken? current))
                foreach (RefreshToken token in tokens.Values.Where(token => token.FamilyId == current.FamilyId))
                    token.RevokedAt = now;
            return Task.CompletedTask;
        }
    }

    private sealed class TestLotImageStore : ILotImageStore
    {
        public Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken) =>
            Task.FromResult($"{Guid.NewGuid():N}{extension}");

        public Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<LotImageContent?> GetAsync(string fileName, CancellationToken cancellationToken) =>
            Task.FromResult<LotImageContent?>(null);
    }
}


