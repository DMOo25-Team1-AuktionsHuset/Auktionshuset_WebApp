using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Repositories;

public sealed class PostgresRefreshTokenStore(AHDBContext db) : IRefreshTokenStore
{
    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<RefreshToken?> FindAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<RefreshRotationResult> RotateAsync(string tokenHash, RefreshToken replacement,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var token = await FindAsync(tokenHash, cancellationToken);
        if (token is null) return RefreshRotationResult.Invalid;

        // Lock the root, not just the presented token: refresh and logout must share one lock.
        var root = await LockFamilyAsync(token.FamilyId, cancellationToken);
        token = await FindAsync(tokenHash, cancellationToken);
        if (root is null || token is null || root.FamilyRevokedAt is not null || token.ExpiresAt <= now)
            return RefreshRotationResult.Invalid;

        if (token.RevokedAt is not null)
        {
            await RevokeLockedFamilyAsync(token.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RefreshRotationResult.Replay;
        }

        if (replacement.UserId != token.UserId || replacement.FamilyId != token.FamilyId
            || replacement.ExpiresAt != token.ExpiresAt)
            throw new InvalidOperationException("Refresh replacement must retain its session identity and absolute expiry.");

        await db.RefreshTokens.Where(t => t.Id == token.Id).ExecuteUpdateAsync(update => update
            .SetProperty(t => t.RevokedAt, now)
            .SetProperty(t => t.ReplacedByTokenId, replacement.Id), cancellationToken);
        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RefreshRotationResult.Rotated;
    }

    public async Task RevokeFamilyAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var token = await FindAsync(tokenHash, cancellationToken);
        if (token is null) return;
        if (await LockFamilyAsync(token.FamilyId, cancellationToken) is null) return;
        await RevokeLockedFamilyAsync(token.FamilyId, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<RefreshToken?> LockFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var rows = await db.RefreshTokens.FromSqlInterpolated(
            $"SELECT * FROM \"RefreshTokens\" WHERE \"Id\" = {familyId} FOR UPDATE")
            .AsNoTracking().ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    private async Task RevokeLockedFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await db.RefreshTokens.Where(t => t.Id == familyId).ExecuteUpdateAsync(update =>
            update.SetProperty(t => t.FamilyRevokedAt, now), cancellationToken);
        await db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }
}
