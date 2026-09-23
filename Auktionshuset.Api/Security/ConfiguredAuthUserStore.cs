using Microsoft.AspNetCore.Identity;
using Auktionshuset.Api.Security;

namespace Auktionshuset.Api.Security
{
    public sealed class ConfiguredAuthUserStore : IAuthUserStore
    {
        private const string BootstrapAdminSection = "Authentication:BootstrapAdmin";

        private readonly IReadOnlyDictionary<string, AuthUser> users;

        public ConfiguredAuthUserStore(IConfiguration configuration, IPasswordHasher<AuthUser> passwordHasher)
        {
            var email = configuration[$"{BootstrapAdminSection}:Email"]?.Trim();
            var password = configuration[$"{BootstrapAdminSection}:Password"];

            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
            {
                users = new Dictionary<string, AuthUser>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    $"Both '{BootstrapAdminSection}:Email' and '{BootstrapAdminSection}:Password' must be configured. ");
            }

            var user = new AuthUser
            {
                UserId = Guid.NewGuid(),
                Email = email,
                PasswordHash = password,
                Roles = [SecurityRoles.Admin],
                Permissions = SecurityPermissions.All
            };

            user.PasswordHash = passwordHasher.HashPassword(user, password);
            users = new Dictionary<string, AuthUser>(StringComparer.OrdinalIgnoreCase)
            {
                [user.Email] = user
            };
        }

        public Task <AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            users.TryGetValue(email.Trim(), out var user);
            return Task.FromResult(user);
        }
    }
}
