using Auktionshuset.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Auktionshuset.Api.Test.Security;

public class SecurityServiceExtensionsTest
{
    [Fact]
    public async Task AddSecurityServices_ConfiguresAuthenticatedFallbackPolicy()
    {
        using ServiceProvider provider = CreateServiceProvider();
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        AuthorizationPolicy? fallbackPolicy = await policyProvider.GetFallbackPolicyAsync();

        Assert.NotNull(fallbackPolicy);
        Assert.Contains(
            fallbackPolicy.Requirements,
            requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task AddSecurityServices_ConfiguresAuctionPermissionPolicy()
    {
        using ServiceProvider provider = CreateServiceProvider();
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        AuthorizationPolicy? policy = await policyProvider.GetPolicyAsync(SecurityPolicies.CanCreateAuction);

        Assert.NotNull(policy);
        Assert.Contains(
            policy.Requirements,
            requirement => requirement is DenyAnonymousAuthorizationRequirement);

        var authorization = provider.GetRequiredService<IAuthorizationService>();
        foreach (string role in new[] { SecurityRoles.AuctionAdmin, SecurityRoles.Admin, SecurityRoles.LotAdmin, SecurityRoles.Customer })
        {
            var claims = SecurityRolePermissions.GetPermissions(role)
                .Select(permission => new Claim(SecurityPermissions.ClaimType, permission))
                .Append(new Claim(ClaimTypes.Role, role));
            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
            var result = await authorization.AuthorizeAsync(user, null, policy);
            Assert.Equal(role is SecurityRoles.AuctionAdmin or SecurityRoles.Admin, result.Succeeded);
        }

        var roleWithoutPermission = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, SecurityRoles.Admin)], "test"));
        Assert.False((await authorization.AuthorizeAsync(roleWithoutPermission, null, policy)).Succeeded);
    }

    [Fact]
    public async Task AddSecurityServices_EnforcesAuthorizationWhenConfigurationDisablesIt()
    {
        using ServiceProvider provider = CreateServiceProvider(enforceAuthorization: false);
        IAuthorizationService authorizationService = provider.GetRequiredService<IAuthorizationService>();
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        string[] policyNames =
        [
            SecurityPolicies.Admin,
            SecurityPolicies.CanCreateLot,
            SecurityPolicies.CanUpdateLot,
            SecurityPolicies.CanDeleteLot,
            SecurityPolicies.CanViewLots,
            SecurityPolicies.CanCreateAuction
        ];

        AuthorizationPolicy? fallbackPolicy = await policyProvider.GetFallbackPolicyAsync();
        Assert.NotNull(fallbackPolicy);
        Assert.Contains(
            fallbackPolicy.Requirements,
            requirement => requirement is DenyAnonymousAuthorizationRequirement);

        foreach (string policyName in policyNames)
        {
            AuthorizationPolicy? policy = await policyProvider.GetPolicyAsync(policyName);
            Assert.NotNull(policy);
            Assert.Contains(
                policy.Requirements,
                requirement => requirement is DenyAnonymousAuthorizationRequirement);

            AuthorizationResult result = await authorizationService.AuthorizeAsync(
                anonymousUser,
                resource: null,
                policyName);

            Assert.False(result.Succeeded, $"Policy '{policyName}' must reject anonymous access.");
        }
    }

    private static ServiceProvider CreateServiceProvider(bool enforceAuthorization = true)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:EnforceAuthorization"] = enforceAuthorization.ToString(),
                ["Authentication:SigningKey"] = "test-signing-key-with-at-least-32-characters",
                ["Authentication:Issuer"] = "Auktionshuset.Api.Test",
                ["Authentication:Audience"] = "Auktionshuset.Client.Test"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecurityServices(configuration);

        return services.BuildServiceProvider();
    }
}
