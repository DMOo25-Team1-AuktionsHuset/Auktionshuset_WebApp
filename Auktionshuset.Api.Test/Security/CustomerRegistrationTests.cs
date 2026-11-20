using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Auktionshuset.Api.Endpoints.Admin.CreateAuction;
using Auktionshuset.Api.Endpoints.Admin.Employee;
using Auktionshuset.Api.Endpoints.Admin.Lots;
using Auktionshuset.Api.Endpoints.Auth;
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
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Service;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Auktionshuset.Api.Test.Security;

public sealed class CustomerRegistrationTests
{
    [Fact]
    public async Task RegisteredCustomerCanLogInWithCustomerRoleAndNoPermissions()
    {
        using TemporarySqliteDatabase database = new();
        using IHost host = await StartServerAsync(database.Path);
        using HttpClient client = host.GetTestClient();

        using HttpResponseMessage created = await client.PostAsJsonAsync("/api/auth/register", ValidRequest());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        RegisterCustomerResponse? registration =
            await created.Content.ReadFromJsonAsync<RegisterCustomerResponse>();
        Assert.NotNull(registration);

        using HttpResponseMessage login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "kunde@example.test",
            password = "adgangskode-123"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        LoginResponse response = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;

        Assert.Equal([SecurityRoles.Customer], response.User.Roles);
        Assert.Empty(response.User.Permissions);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);
        Assert.Contains(token.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == SecurityRoles.Customer);
        Assert.DoesNotContain(token.Claims, claim => claim.Type == SecurityPermissions.ClaimType);

        // The refresh flow must keep working for the stored customer across requests.
        using HttpResponseMessage refresh = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(response.RefreshToken!));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        LoginResponse refreshed = (await refresh.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal([SecurityRoles.Customer], refreshed.User.Roles);
        Assert.Empty(refreshed.User.Permissions);
    }

    [Fact]
    public async Task RegisteredAccountIsStoredPermanentlyAndLinkedToItsCustomer()
    {
        using TemporarySqliteDatabase database = new();
        Guid customerId;
        Guid userId;
        using (IHost firstHost = await StartServerAsync(database.Path))
        {
            using HttpClient client = firstHost.GetTestClient();
            using HttpResponseMessage created = await client.PostAsJsonAsync("/api/auth/register", ValidRequest());
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            RegisterCustomerResponse registration =
                (await created.Content.ReadFromJsonAsync<RegisterCustomerResponse>())!;
            customerId = registration.CustomerId;

            await using AsyncServiceScope scope = firstHost.Services.CreateAsyncScope();
            AHDBContext db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
            LoginUser loginUser = await db.LoginUsers.AsNoTracking().SingleAsync();
            Customer customer = await db.Customers.AsNoTracking()
                .SingleAsync(entity => entity.CustomerId == customerId);
            userId = loginUser.UserId;

            Assert.Equal("kunde@example.test", loginUser.Email);
            Assert.Equal(SecurityRoles.Customer, loginUser.Role);
            Assert.Equal(customer.CustomerId, loginUser.CustomerId);
            Assert.False(string.IsNullOrEmpty(loginUser.PasswordHash));
            Assert.Equal("kunde@example.test", customer.Email);
            Assert.Equal("Anna", customer.FirstName);
            Assert.Equal("Andreasen", customer.LastName);
            Assert.Equal("8888 8888", customer.PhoneNumber);
            Assert.Equal("Vestergade 1, 6000 Kolding", customer.Address);
            Assert.Equal(new DateOnly(1990, 3, 15), customer.BirthDate);

            var hasher = new PasswordHasher<AuthUser>();
            var verificationUser = new AuthUser
            {
                UserId = loginUser.UserId,
                Email = loginUser.Email!,
                PasswordHash = loginUser.PasswordHash!,
                Roles = [loginUser.Role],
                Permissions = []
            };
            Assert.Equal(PasswordVerificationResult.Success,
                hasher.VerifyHashedPassword(verificationUser, loginUser.PasswordHash, "adgangskode-123"));
        }

        // A brand new host against the same database proves the account survives a restart.
        using IHost secondHost = await StartServerAsync(database.Path);
        using HttpClient restartedClient = secondHost.GetTestClient();
        using HttpResponseMessage login = await restartedClient.PostAsJsonAsync("/api/auth/login", new
        {
            email = "KUNDE@example.test",
            password = "adgangskode-123"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        LoginResponse response = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal(userId, response.User.UserId);
        Assert.Equal([SecurityRoles.Customer], response.User.Roles);

        await using AsyncServiceScope restartedScope = secondHost.Services.CreateAsyncScope();
        AHDBContext restartedDb = restartedScope.ServiceProvider.GetRequiredService<AHDBContext>();
        Assert.True(await restartedDb.LoginUsers.AsNoTracking().AnyAsync(user => user.UserId == userId));
        Assert.True(await restartedDb.Customers.AsNoTracking().AnyAsync(entity => entity.CustomerId == customerId));
    }

    [Fact]
    public async Task DuplicateEmailIsRejectedRegardlessOfCase()
    {
        using TemporarySqliteDatabase database = new();
        using IHost host = await StartServerAsync(database.Path);
        using HttpClient client = host.GetTestClient();

        using HttpResponseMessage first = await client.PostAsJsonAsync("/api/auth/register", ValidRequest());
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using HttpResponseMessage duplicate = await client.PostAsJsonAsync("/api/auth/register",
            ValidRequest() with { FirstName = "Anden" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using HttpResponseMessage differentCase = await client.PostAsJsonAsync("/api/auth/register",
            ValidRequest() with { Email = "KUNDE@EXAMPLE.TEST" });
        Assert.Equal(HttpStatusCode.Conflict, differentCase.StatusCode);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        AHDBContext db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
        Assert.Equal(1, await db.Customers.AsNoTracking().CountAsync());
        Assert.Equal(1, await db.LoginUsers.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData("firstName", "", "Fornavn skal udfyldes.")]
    [InlineData("email", "not-an-email", "Indtast en gyldig emailadresse.")]
    [InlineData("phoneNumber", "123", "Indtast et gyldigt telefonnummer.")]
    [InlineData("address", "", "Adresse skal udfyldes.")]
    [InlineData("password", "kort", "Adgangskoden skal være mindst 8 tegn.")]
    public async Task InvalidRegistrationInputIsRejectedWithDanishMessages(
        string changedField, string value, string expectedMessage)
    {
        using TemporarySqliteDatabase database = new();
        using IHost host = await StartServerAsync(database.Path);
        using HttpClient client = host.GetTestClient();

        RegisterCustomerRequest request = changedField switch
        {
            "firstName" => ValidRequest() with { FirstName = value },
            "email" => ValidRequest() with { Email = value },
            "phoneNumber" => ValidRequest() with { PhoneNumber = value },
            "address" => ValidRequest() with { Address = value },
            "password" => ValidRequest() with { Password = value },
            _ => throw new ArgumentOutOfRangeException(nameof(changedField))
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedMessage, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MismatchedPasswordConfirmationIsRejectedWithDanishMessage()
    {
        using TemporarySqliteDatabase database = new();
        using IHost host = await StartServerAsync(database.Path);
        using HttpClient client = host.GetTestClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/register",
            ValidRequest() with { ConfirmPassword = "anden-adgangskode" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Adgangskoderne er ikke ens.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FutureBirthDateIsRejectedWithDanishMessage()
    {
        using TemporarySqliteDatabase database = new();
        using IHost host = await StartServerAsync(database.Path);
        using HttpClient client = host.GetTestClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/register",
            ValidRequest() with { BirthDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Indtast en gyldig fødselsdato.",
            await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegistrationIgnoresSelfAssignedRolesPermissionsAndCustomerId()
    {
        using TemporarySqliteDatabase database = new();
        using IHost host = await StartServerAsync(database.Path);
        using HttpClient client = host.GetTestClient();

        Guid suppliedCustomerId = Guid.NewGuid();
        Dictionary<string, object?> payload = new()
        {
            ["firstName"] = "Anna",
            ["lastName"] = "Andreasen",
            ["email"] = "kunde@example.test",
            ["phoneNumber"] = "8888 8888",
            ["birthDate"] = "1990-03-15",
            ["address"] = "Vestergade 1, 6000 Kolding",
            ["password"] = "adgangskode-123",
            ["confirmPassword"] = "adgangskode-123",
            ["role"] = "Admin",
            ["permissions"] = new[] { "employees.read", "lots.delete" },
            ["customerId"] = suppliedCustomerId
        };
        using HttpResponseMessage created = await client.PostAsJsonAsync("/api/auth/register", payload);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using HttpResponseMessage login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "kunde@example.test",
            password = "adgangskode-123"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        LoginResponse response = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;

        Assert.Equal([SecurityRoles.Customer], response.User.Roles);
        Assert.Empty(response.User.Permissions);
        Assert.NotEqual(suppliedCustomerId, response.User.UserId);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        AHDBContext db = scope.ServiceProvider.GetRequiredService<AHDBContext>();
        LoginUser loginUser = await db.LoginUsers.AsNoTracking().SingleAsync();
        Assert.Equal(SecurityRoles.Customer, loginUser.Role);
        Assert.NotEqual(suppliedCustomerId, loginUser.CustomerId);
    }

    [Fact]
    public async Task RegisteredCustomerIsForbiddenOnAdministrativeEndpoints()
    {
        using TemporarySqliteDatabase database = new();
        using IHost host = await StartServerAsync(database.Path);
        using HttpClient client = host.GetTestClient();

        using HttpResponseMessage created = await client.PostAsJsonAsync("/api/auth/register", ValidRequest());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using HttpResponseMessage login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "kunde@example.test",
            password = "adgangskode-123"
        });
        LoginResponse response = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", response.AccessToken);

        foreach (string path in new[] { "/api/lots/", "/api/auctions/", "/api/employee/" })
        {
            using HttpResponseMessage forbidden = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
    }

    [Fact]
    public async Task ConfiguredAdminCanStillLogInAgainstTheSameServer()
    {
        using TemporarySqliteDatabase database = new();
        IConfiguration configuration = TestConfiguration();
        using IHost host = await StartServerAsync(database.Path, configuration);
        using HttpClient client = host.GetTestClient();

        using HttpResponseMessage login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@example.test",
            password = "test-password"
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        LoginResponse response = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal([SecurityRoles.Admin], response.User.Roles);
        Assert.Equal(SecurityPermissions.All.Order(), response.User.Permissions.Order());
    }

    private static RegisterCustomerRequest ValidRequest() => new()
    {
        FirstName = "Anna",
        LastName = "Andreasen",
        Email = "kunde@example.test",
        PhoneNumber = "8888 8888",
        BirthDate = new DateOnly(1990, 3, 15),
        Address = "Vestergade 1, 6000 Kolding",
        Password = "adgangskode-123",
        ConfirmPassword = "adgangskode-123"
    };

    private static IConfiguration TestConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:BootstrapAdmin:Email"] = "admin@example.test",
            ["Authentication:BootstrapAdmin:Password"] = "test-password",
            ["Authentication:SigningKey"] = "test-signing-key-with-at-least-32-characters",
            ["Authentication:Issuer"] = "Auktionshuset.Api.Test",
            ["Authentication:Audience"] = "Auktionshuset.Client.Test"
        })
        .Build();

    private sealed class TemporarySqliteDatabase : IDisposable
    {
        public TemporarySqliteDatabase()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"auction-registration-{Guid.NewGuid():N}.db");
        }

        public string Path { get; }

        public void Dispose()
        {
            try { System.IO.File.Delete(Path); }
            catch (IOException) { }
        }
    }

    private static Task<IHost> StartServerAsync(string databasePath) =>
        StartServerAsync(databasePath, TestConfiguration());

    private static async Task<IHost> StartServerAsync(string databasePath, IConfiguration configuration)
    {
        IHost host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddApiServices();
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
