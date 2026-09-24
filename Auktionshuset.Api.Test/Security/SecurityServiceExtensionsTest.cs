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
    public async Task AddSecurityServices_ConfiguresCreateAuctionPermissionPolicy()
    {
        using ServiceProvider provider = CreateServiceProvider();
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        AuthorizationPolicy? policy = await policyProvider.GetPolicyAsync(SecurityPolicies.CanCreateAuction);

        Assert.NotNull(policy);
        Assert.Contains(
            policy.Requirements,
            requirement => requirement is DenyAnonymousAuthorizationRequirement);

        ClaimsAuthorizationRequirement claimRequirement = Assert.Single(
            policy.Requirements.OfType<ClaimsAuthorizationRequirement>());
        Assert.Equal(SecurityPermissions.ClaimType, claimRequirement.ClaimType);
        Assert.Equal([SecurityPermissions.CreateAuction], claimRequirement.AllowedValues);
    }

    [Fact]
    public async Task AddSecurityServices_WhenAuthorizationIsDisabled_AllowsAnonymousAccess()
    {
        using ServiceProvider provider = CreateServiceProvider(enforceAuthorization: false);
        IAuthorizationService authorizationService = provider.GetRequiredService<IAuthorizationService>();
        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        string[] policyNames = new[]
        {
            SecurityPolicies.Admin,
            SecurityPolicies.CanCreateLot,
            SecurityPolicies.CanUpdateLot,
            SecurityPolicies.CanDeleteLot,
            SecurityPolicies.CanViewLots,
            SecurityPolicies.CanCreateAuction
        };

        foreach (string? policyName in policyNames)
        {
            AuthorizationResult result = await authorizationService.AuthorizeAsync(
                anonymousUser,
                resource: null,
                policyName);

            Assert.True(result.Succeeded, $"Policy '{policyName}' should allow anonymous development access.");
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
