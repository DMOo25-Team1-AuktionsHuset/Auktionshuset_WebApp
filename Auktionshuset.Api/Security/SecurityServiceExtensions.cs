using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;

namespace Auktionshuset.Api.Security;

public static class SecurityServiceExtensions
{
    private const string EnforceAuthorizationKey = "Security:EnforceAuthorization";

    public static IServiceCollection AddSecurityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        bool enforceAuthorization = configuration.GetValue(EnforceAuthorizationKey, true);

        if (enforceAuthorization)
        {
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options => ConfigureJwtBearer(options, configuration));
        }
        else
        {
            services.AddAuthentication();
        }

        services.AddAuthorization(options =>
        {
            if (!enforceAuthorization)
            {
                ConfigureDevelopmentBypass(options);
                return;
            }

            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy(SecurityPolicies.Admin, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Admin");
            });

            AddPermissionPolicy(options, SecurityPolicies.CanCreateLot, SecurityPermissions.CreateLot);
            AddPermissionPolicy(options, SecurityPolicies.CanUpdateLot, SecurityPermissions.UpdateLot);
            AddPermissionPolicy(options, SecurityPolicies.CanDeleteLot, SecurityPermissions.DeleteLot);
            AddPermissionPolicy(options, SecurityPolicies.CanViewLots, SecurityPermissions.ViewLots);
            AddPermissionPolicy(options, SecurityPolicies.CanCreateAuction, SecurityPermissions.CreateAuction);
        });

        return services;
    }

    private static void ConfigureDevelopmentBypass(AuthorizationOptions options)
    {
        AuthorizationPolicy allowAnonymousPolicy = new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();

        options.DefaultPolicy = allowAnonymousPolicy;
        options.FallbackPolicy = allowAnonymousPolicy;

        options.AddPolicy(SecurityPolicies.Admin, allowAnonymousPolicy);
        options.AddPolicy(SecurityPolicies.CanCreateLot, allowAnonymousPolicy);
        options.AddPolicy(SecurityPolicies.CanUpdateLot, allowAnonymousPolicy);
        options.AddPolicy(SecurityPolicies.CanDeleteLot, allowAnonymousPolicy);
        options.AddPolicy(SecurityPolicies.CanViewLots, allowAnonymousPolicy);
        options.AddPolicy(SecurityPolicies.CanCreateAuction, allowAnonymousPolicy);
    }

    private static void ConfigureJwtBearer(
        JwtBearerOptions options,
        IConfiguration configuration)
    {
        string signingKey = configuration["Authentication:SigningKey"]
            ?? throw new InvalidOperationException(
                "Missing configuration value 'Authentication:SigningKey'.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration["Authentication:Issuer"],
            ValidateAudience = true,
            ValidAudience = configuration["Authentication:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                StringValues accessToken = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    }

    private static void AddPermissionPolicy(
        AuthorizationOptions options,
        string policyName,
        string permission)
    {
        options.AddPolicy(policyName, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim(SecurityPermissions.ClaimType, permission);
        });
    }
}
