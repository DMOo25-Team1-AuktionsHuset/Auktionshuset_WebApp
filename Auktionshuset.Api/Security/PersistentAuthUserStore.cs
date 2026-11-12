using System.Globalization;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Api.Security;

/// <summary>
/// Reads Identity accounts. Configured accounts are provisioned once into AspNetUsers;
/// configuration-only hosts are supported for isolated security tests.
/// </summary>
public sealed class PersistentAuthUserStore(
    ConfiguredAuthUserStore configuredUsers,
    IServiceProvider services) : IAuthUserStore
{
    public async Task<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AuthUser? configured = await configuredUsers.FindByEmailAsync(email, cancellationToken);
        AHDBContext? db = services.GetService<AHDBContext>();
        if (db is null) return configured;
        if (configured is not null) await ProvisionAsync(db, configured, cancellationToken);

        string normalized = NormalizeEmail(email);
        LoginUser? loginUser = await db.LoginUsers.AsNoTracking()
            .SingleOrDefaultAsync(user => user.Email == normalized, cancellationToken);
        return loginUser is null || !loginUser.IsActive ? null : ToAuthUser(loginUser);
    }

    public async Task<AuthUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AuthUser? configured = await configuredUsers.FindByIdAsync(userId, cancellationToken);
        AHDBContext? db = services.GetService<AHDBContext>();
        if (db is null) return configured;
        if (configured is not null)
        {
            var provisioned = await ProvisionAsync(db, configured, cancellationToken);
            return !provisioned.IsActive ? null : ToAuthUser(provisioned);
        }

        LoginUser? loginUser = await db.LoginUsers.AsNoTracking()
            .SingleOrDefaultAsync(user => user.UserId == userId, cancellationToken);
        return loginUser is null || !loginUser.IsActive ? null : ToAuthUser(loginUser);
    }

    public async Task<bool> CheckPasswordAsync(AuthUser user, string password, CancellationToken cancellationToken)
    {
        var db = services.GetService<AHDBContext>();
        if (db is null)
            return services.GetRequiredService<IPasswordHasher<AuthUser>>()
                .VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
        var account = await db.LoginUsers.SingleOrDefaultAsync(u => u.UserId == user.UserId, cancellationToken);
        return account is { IsActive: true } && account.SessionVersion == user.SessionVersion
            && await services.GetRequiredService<UserManager<LoginUser>>().CheckPasswordAsync(account, password);
    }

    private async Task<LoginUser> ProvisionAsync(AHDBContext db, AuthUser configured, CancellationToken ct)
    {
        var email = NormalizeEmail(configured.Email);
        var existing = await db.LoginUsers.SingleOrDefaultAsync(u => u.Email == email, ct);
        if (existing is not null) return existing;
        var account = new LoginUser
        {
            UserId = configured.UserId, Email = email, UserName = email,
            PasswordHash = configured.PasswordHash, Role = configured.Roles.Single(),
            CustomerId = null, IsSystemAccount = true
        };
        try
        {
            var result = await services.GetRequiredService<UserManager<LoginUser>>().CreateAsync(account);
            if (result.Succeeded) return account;
            if (!result.Errors.All(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
                throw new InvalidOperationException("Could not provision the configured Identity account.");
        }
        catch (DbUpdateException)
        {
            // A concurrent request may have provisioned the same account first.
            db.Entry(account).State = EntityState.Detached;
            var winner = await db.LoginUsers.SingleOrDefaultAsync(u => u.Email == email, ct);
            if (winner is not null) return winner;
            throw;
        }
        return await db.LoginUsers.SingleAsync(u => u.Email == email, ct);
    }

    public static string NormalizeEmail(string email) =>
        email.Trim().ToLower(CultureInfo.InvariantCulture);

    public static AuthUser ToAuthUser(LoginUser loginUser)
    {
        var user = new AuthUser
        {
            UserId = loginUser.UserId,
            SessionVersion = loginUser.SessionVersion,
            Email = loginUser.Email!,
            PasswordHash = loginUser.PasswordHash!,
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
