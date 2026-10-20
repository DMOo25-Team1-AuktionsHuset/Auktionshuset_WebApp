using Microsoft.AspNetCore.Identity;

namespace Auktionshuset.Api.Security;

public sealed class ConfiguredAuthUserStore : IAuthUserStore
{
    private const string BootstrapAdminSection = "Authentication:BootstrapAdmin";
    private const string UsersSection = "Authentication:Users";

    private readonly IReadOnlyDictionary<string, AuthUser> users;

    public ConfiguredAuthUserStore(IConfiguration configuration, IPasswordHasher<AuthUser> passwordHasher)
    {
        var configuredUsers = new Dictionary<string, AuthUser>(StringComparer.OrdinalIgnoreCase);
        IConfigurationSection bootstrap = configuration.GetSection(BootstrapAdminSection);
        string? bootstrapEmail = bootstrap["Email"]?.Trim();
        string? bootstrapPassword = bootstrap["Password"];

        if (!string.IsNullOrWhiteSpace(bootstrapEmail) || !string.IsNullOrWhiteSpace(bootstrapPassword))
        {
            if (string.IsNullOrWhiteSpace(bootstrapEmail) || string.IsNullOrWhiteSpace(bootstrapPassword))
            {
                throw new InvalidOperationException(
                    $"Both '{BootstrapAdminSection}:Email' and '{BootstrapAdminSection}:Password' must be configured.");
            }

            AddUser(configuredUsers, passwordHasher, bootstrapEmail, bootstrapPassword, SecurityRoles.Admin,
                BootstrapAdminSection);
        }

        foreach (IConfigurationSection section in configuration.GetSection(UsersSection).GetChildren())
        {
            string? email = section["Email"]?.Trim();
            string? password = section["Password"];
            string? role = section["Role"]?.Trim();

            if (string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(password)
                || string.IsNullOrWhiteSpace(role))
            {
                throw new InvalidOperationException(
                    $"'{UsersSection}:{section.Key}' must configure Email, Password, and Role.");
            }

            if (!SecurityRolePermissions.IsKnownRole(role))
            {
                throw new InvalidOperationException(
                    $"'{UsersSection}:{section.Key}:Role' contains unknown role '{role}'.");
            }

            AddUser(configuredUsers, passwordHasher, email, password, role, $"{UsersSection}:{section.Key}");
        }

        users = configuredUsers;
    }

    public Task<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        users.TryGetValue(email.Trim(), out AuthUser? user);
        return Task.FromResult(user);
    }

    private static void AddUser(
        IDictionary<string, AuthUser> configuredUsers,
        IPasswordHasher<AuthUser> passwordHasher,
        string email,
        string password,
        string role,
        string configurationPath)
    {
        if (configuredUsers.ContainsKey(email))
        {
            throw new InvalidOperationException(
                $"Authentication configuration contains duplicate email '{email}' at '{configurationPath}'.");
        }

        var user = new AuthUser
        {
            UserId = Guid.NewGuid(),
            Email = email,
            PasswordHash = string.Empty,
            Roles = [role],
            Permissions = SecurityRolePermissions.GetPermissions(role)
        };

        user.PasswordHash = passwordHasher.HashPassword(user, password);
        configuredUsers.Add(user.Email, user);
    }
}
