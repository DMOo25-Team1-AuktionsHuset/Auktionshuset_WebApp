using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Auktionshuset.Api.Endpoints.Auth;
using Auktionshuset.Api.Security;
using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Contracts.Dto.Auth;
using Auktionshuset.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Auktionshuset.Api.Test.Security;

public sealed class ConfiguredAuthUserStoreTests
{
   [Theory]
   [InlineData("LotAdmin", SecurityRoles.LotAdmin)]
   [InlineData("AuctionAdmin", SecurityRoles.AuctionAdmin)]
   [InlineData("Admin", SecurityRoles.Admin)]
   public async Task LoginIssuesRoleAndBackendMappedPermissionClaims(string configuredRole, string expectedRole)
   {
       IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
       {
           ["Authentication:Users:0:Email"] = "configured@example.test",
           ["Authentication:Users:0:Password"] = "test-password",
           ["Authentication:Users:0:Role"] = configuredRole,
           ["Authentication:SigningKey"] = "test-signing-key-with-at-least-32-characters",
           ["Authentication:Issuer"] = "Auktionshuset.Api.Test",
           ["Authentication:Audience"] = "Auktionshuset.Client.Test"
       });

       using IHost host = await StartLoginServerAsync(configuration);
       using HttpClient client = host.GetTestClient();
       using HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new
       {
           email = "configured@example.test",
           password = "test-password",
           role = "Admin",
           permissions = new[] { "employees.read" }
       });

       Assert.Equal(HttpStatusCode.OK, response.StatusCode);
       LoginResponse login = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
       Assert.Equal([expectedRole], login.User.Roles);
       Assert.Equal(SecurityRolePermissions.GetPermissions(expectedRole).Order(), login.User.Permissions.Order());

       JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);
       Assert.Contains(token.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == expectedRole);
       Assert.Equal(
           SecurityRolePermissions.GetPermissions(expectedRole).Order(),
           token.Claims.Where(claim => claim.Type == SecurityPermissions.ClaimType).Select(claim => claim.Value).Order());
       Assert.DoesNotContain(token.Claims, claim => claim.Type == SecurityPermissions.ClaimType && claim.Value == "employees.read"
           && expectedRole != SecurityRoles.Admin);

       using HttpResponseMessage refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh",
           new RefreshTokenRequest(login.RefreshToken!));
       Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
       LoginResponse refreshed = (await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>())!;
       Assert.Equal(login.User.UserId, refreshed.User.UserId);
       Assert.Equal(login.User.Roles, refreshed.User.Roles);
       Assert.Equal(login.User.Permissions.Order(), refreshed.User.Permissions.Order());
       Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
   }

   [Fact]
   public async Task BootstrapAdminKeepsFullPermissionsAndConfiguredEmailsAreCaseInsensitive()
   {
       var configuration = CreateConfiguration(new Dictionary<string, string?>
       {
           ["Authentication:BootstrapAdmin:Email"] = "root@example.test",
           ["Authentication:BootstrapAdmin:Password"] = "test-password",
           ["Authentication:Users:0:Email"] = "lot@example.test",
           ["Authentication:Users:0:Password"] = "another-password",
           ["Authentication:Users:0:Role"] = SecurityRoles.LotAdmin
       });
       var hasher = new PasswordHasher<AuthUser>();
       var store = new ConfiguredAuthUserStore(configuration, hasher);

       AuthUser bootstrap = (await store.FindByEmailAsync("ROOT@example.test", CancellationToken.None))!;
       AuthUser lotAdmin = (await store.FindByEmailAsync("lot@example.test", CancellationToken.None))!;

       Assert.Equal([SecurityRoles.Admin], bootstrap.Roles);
       Assert.Equal(SecurityPermissions.All.Order(), bootstrap.Permissions.Order());
       Assert.Equal(SecurityRolePermissions.GetPermissions(SecurityRoles.LotAdmin).Order(), lotAdmin.Permissions.Order());
       Assert.Equal(PasswordVerificationResult.Success,
           hasher.VerifyHashedPassword(bootstrap, bootstrap.PasswordHash, "test-password"));
       Assert.Same(bootstrap, await store.FindByIdAsync(bootstrap.UserId, CancellationToken.None));
       Assert.Same(lotAdmin, await store.FindByIdAsync(lotAdmin.UserId, CancellationToken.None));
       Assert.Null(await store.FindByIdAsync(Guid.NewGuid(), CancellationToken.None));

       using var cancellation = new CancellationTokenSource();
       cancellation.Cancel();
       await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
           store.FindByIdAsync(bootstrap.UserId, cancellation.Token));
   }

   [Fact]
   public void StoreRejectsDuplicateEmailsRegardlessOfCase()
   {
       IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
       {
           ["Authentication:BootstrapAdmin:Email"] = "Admin@example.test",
           ["Authentication:BootstrapAdmin:Password"] = "test-password",
           ["Authentication:Users:0:Email"] = "admin@EXAMPLE.test",
           ["Authentication:Users:0:Password"] = "another-password",
           ["Authentication:Users:0:Role"] = SecurityRoles.LotAdmin
       });

       InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
           new ConfiguredAuthUserStore(configuration, new PasswordHasher<AuthUser>()));

       Assert.Contains("duplicate email", exception.Message, StringComparison.OrdinalIgnoreCase);
   }

   [Theory]
   [InlineData("Nobody")]
   [InlineData("")]
   public void StoreRejectsUnknownOrMissingRoles(string role)
   {
       IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
       {
           ["Authentication:Users:0:Email"] = "user@example.test",
           ["Authentication:Users:0:Password"] = "test-password",
           ["Authentication:Users:0:Role"] = role
       });

       Assert.Throws<InvalidOperationException>(() =>
           new ConfiguredAuthUserStore(configuration, new PasswordHasher<AuthUser>()));
   }

   [Fact]
   public void StoreRejectsMissingRequiredUserValues()
   {
       IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
       {
           ["Authentication:Users:0:Email"] = "user@example.test",
           ["Authentication:Users:0:Role"] = SecurityRoles.AuctionAdmin
       });

       Assert.Throws<InvalidOperationException>(() =>
           new ConfiguredAuthUserStore(configuration, new PasswordHasher<AuthUser>()));
   }

   private static IConfiguration CreateConfiguration(IDictionary<string, string?> values) =>
       new ConfigurationBuilder().AddInMemoryCollection(values).Build();

   private static async Task<IHost> StartLoginServerAsync(IConfiguration configuration) =>
       await new HostBuilder()
           .ConfigureWebHost(webHost =>
           {
               webHost.UseTestServer();
               webHost.ConfigureServices(services =>
               {
                   services.AddRouting();
                   services.AddSingleton(configuration);
                   services.AddSecurityServices(configuration);
                   services.AddSingleton<IRefreshTokenStore, TestRefreshTokenStore>();
               });
               webHost.Configure(app =>
               {
                   app.UseRouting();
                   app.UseRateLimiter();
                   app.UseAuthentication();
                   app.UseAuthorization();
                   app.UseEndpoints(endpoints => endpoints.MapAuthEndpoints());
               });
           })
           .StartAsync();

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
}
