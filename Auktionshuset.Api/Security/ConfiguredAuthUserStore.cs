using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;
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
                UserId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("bootstrap:" + email.ToUpperInvariant())).AsSpan(0, 16)),
                Email = email,
                PasswordHash = string.Empty,
                Roles = [SecurityRoles.SuperAdmin],
                Permissions = SecurityPermissions.All,
                // Stable across restarts, invalidated by a password or signing-key change.
                // Keyed hashing avoids making the token DB an offline password verifier.
                CredentialVersion = Convert.ToHexString(HMACSHA256.HashData(
                    Encoding.UTF8.GetBytes(configuration["Authentication:SigningKey"]
                        ?? throw new InvalidOperationException("Authentication:SigningKey must be configured.")),
                    Encoding.UTF8.GetBytes(password)))
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

        public Task<AuthUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(users.Values.SingleOrDefault(user => user.UserId == userId));
        }
    }
}
