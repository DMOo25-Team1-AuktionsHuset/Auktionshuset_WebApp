using System.Globalization;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Api.Security;

/// <summary>
/// Serves configured bootstrap/administrative users first and permanently stored
/// customer and employee login users second. Test hosts without a database
/// keep working with configuration-only users.
/// </summary>
public sealed class PersistentAuthUserStore(
    ConfiguredAuthUserStore configuredUsers,
    IServiceProvider services) : IAuthUserStore
{
    public async Task<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AuthUser? configured = await configuredUsers.FindByEmailAsync(email, cancellationToken);
        if (configured is not null) return configured;

        AHDBContext? db = services.GetService<AHDBContext>();
        if (db is null) return null;

        string normalized = NormalizeEmail(email);
        LoginUser? loginUser = await db.LoginUsers.AsNoTracking()
            .SingleOrDefaultAsync(user => user.Email == normalized, cancellationToken);
        return loginUser is null || !loginUser.IsActive ? null : ToAuthUser(loginUser);
    }

    public async Task<AuthUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AuthUser? configured = await configuredUsers.FindByIdAsync(userId, cancellationToken);
        if (configured is not null) return configured;

        AHDBContext? db = services.GetService<AHDBContext>();
        if (db is null) return null;

        LoginUser? loginUser = await db.LoginUsers.AsNoTracking()
            .SingleOrDefaultAsync(user => user.UserId == userId, cancellationToken);
        return loginUser is null || !loginUser.IsActive ? null : ToAuthUser(loginUser);
    }

    public static string NormalizeEmail(string email) =>
        email.Trim().ToLower(CultureInfo.InvariantCulture);

    public static AuthUser ToAuthUser(LoginUser loginUser)
    {
        var user = new AuthUser
        {
            UserId = loginUser.UserId,
            SessionVersion = loginUser.SessionVersion,
            Email = loginUser.Email,
            PasswordHash = loginUser.PasswordHash,
            Roles = [loginUser.Role],
            Permissions = SecurityRolePermissions.GetPermissions(loginUser.Role)
        };
        return user;
    }

    /// <summary>
    /// Hashes a password with the same hasher used for configured users, keyed on a
    /// placeholder user so only the hash format matters.
    /// </summary>
    public static string HashPassword(IPasswordHasher<AuthUser> passwordHasher, string password)
    {
        var user = new AuthUser
        {
            UserId = Guid.NewGuid(),
            Email = string.Empty,
            PasswordHash = string.Empty,
            Roles = [],
            Permissions = []
        };
        return passwordHasher.HashPassword(user, password);
    }
}
