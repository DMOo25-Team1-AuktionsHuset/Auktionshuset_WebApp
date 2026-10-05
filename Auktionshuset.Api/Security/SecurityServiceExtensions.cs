using Auktionshuset.Contracts.Security;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;


namespace Auktionshuset.Api.Security;

public static class SecurityServiceExtensions
{

    public static IServiceCollection AddSecurityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<
            IPasswordHasher<AuthUser>,
            PasswordHasher<AuthUser>>();
        
        services.AddSingleton<
            IAuthUserStore,
            ConfiguredAuthUserStore>();
        services.AddSingleton<
            IAccessTokenService,
            JwtTokenService>();
        services.AddScoped<RefreshTokenService>();
        services.AddAuthRateLimiting(configuration);


        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => ConfigureJwtBearer(options, configuration));

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy(SecurityPolicies.Admin, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(SecurityRoles.SuperAdmin);
            });

            options.AddPolicy(SecurityPolicies.CanReadEmployees, policy =>
                policy.RequireAuthenticatedUser().RequireRole(SecurityRoles.AuctionAdmin, SecurityRoles.SuperAdmin));
            AddPermissionPolicy(options, SecurityPolicies.CanCreateLot, SecurityPermissions.CreateLot);
            AddPermissionPolicy(options, SecurityPolicies.CanUpdateLot, SecurityPermissions.UpdateLot);
            AddPermissionPolicy(options, SecurityPolicies.CanDeleteLot, SecurityPermissions.DeleteLot);
            AddPermissionPolicy(options, SecurityPolicies.CanViewLots, SecurityPermissions.ViewLots);
            AddPermissionPolicy(options, SecurityPolicies.CanCreateAuction, SecurityPermissions.CreateAuction);
            options.AddPolicy(SecurityPolicies.CanWriteLotImage, policy =>
                policy.RequireAuthenticatedUser().RequireAssertion(context =>
                    AdminRoles.CanManageLots(context.User)));
        });

        return services;
    }


    private static void ConfigureJwtBearer(
        JwtBearerOptions options,
        IConfiguration configuration)
    {
        string signingKey = configuration["Authentication:SigningKey"]
            ?? throw new InvalidOperationException(
                "Missing configuration value 'Authentication:SigningKey'.");
        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException(
                "Configuration value 'Authentication:SigningKey' must contain at least 32 bytes.");
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration["Authentication:Issuer"],
            ValidateAudience = true,
            ValidAudience = configuration["Authentication:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
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
            policy.RequireAssertion(context => permission switch
            {
                SecurityPermissions.CreateAuction => AdminRoles.CanManageAuctions(context.User),
                SecurityPermissions.ViewLots => AdminRoles.CanReadLots(context.User),
                _ => AdminRoles.CanManageLots(context.User)
            });
        });
    }
}
