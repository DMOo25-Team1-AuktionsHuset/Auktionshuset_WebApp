using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Abstraction.Auth;

public enum RefreshRotationResult { Rotated, Invalid, Replay }

public interface IRefreshTokenStore
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);
    Task<RefreshToken?> FindAsync(string tokenHash, CancellationToken cancellationToken);
    // Must atomically check, revoke, and replace, serialized against family revocation.
    Task<RefreshRotationResult> RotateAsync(string tokenHash, RefreshToken replacement,
        DateTimeOffset now, CancellationToken cancellationToken);
    Task RevokeFamilyAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken);
}
