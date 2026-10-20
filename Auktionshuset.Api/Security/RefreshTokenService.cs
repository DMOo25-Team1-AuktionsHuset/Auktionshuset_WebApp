using System.Security.Cryptography;
using System.Text;
using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Contracts.Dto.Auth;
using Auktionshuset.Domain.Entities;
using Microsoft.AspNetCore.WebUtilities;

namespace Auktionshuset.Api.Security;

public sealed class RefreshTokenService(IRefreshTokenStore store, IAuthUserStore users,
    IAccessTokenService accessTokens, TimeProvider clock, IConfiguration configuration)
{
    public async Task<LoginResponse> CreateSessionAsync(AuthUser user, CancellationToken cancellationToken)
    {
        int days = configuration.GetValue("Authentication:RefreshTokenLifetimeDays", 7);
        if (days is < 1 or > 30)
            throw new InvalidOperationException("Authentication:RefreshTokenLifetimeDays must be between 1 and 30.");
        var now = clock.GetUtcNow();
        Guid id = Guid.NewGuid();
        var (plaintext, token) = CreateToken(user, id, id, now, now.AddDays(days));
        var response = CreateResponse(user, plaintext, token.ExpiresAt);
        await store.AddAsync(token, cancellationToken);
        return response;
    }

    public async Task<LoginResponse?> RotateAsync(string plaintext, CancellationToken cancellationToken)
    {
        if (!IsValidFormat(plaintext)) return null;
        string hash = Hash(plaintext);
        var token = await store.FindAsync(hash, cancellationToken);
        if (token is null || token.ExpiresAt <= clock.GetUtcNow()) return null;
        var user = await users.FindByIdAsync(token.UserId, cancellationToken);
        if (user is null)
        {
            await store.RevokeFamilyAsync(hash, clock.GetUtcNow(), cancellationToken);
            return null;
        }
        var (nextPlaintext, replacement) = CreateToken(user, Guid.NewGuid(), token.FamilyId,
            clock.GetUtcNow(), token.ExpiresAt);
        var response = CreateResponse(user, nextPlaintext, token.ExpiresAt);
        var result = await store.RotateAsync(hash, replacement, clock.GetUtcNow(), cancellationToken);
        return result == RefreshRotationResult.Rotated ? response : null;
    }

    public Task RevokeAsync(string plaintext, CancellationToken cancellationToken) =>
        IsValidFormat(plaintext)
            ? store.RevokeFamilyAsync(Hash(plaintext), clock.GetUtcNow(), cancellationToken)
            : Task.CompletedTask;

    private LoginResponse CreateResponse(AuthUser user, string refreshToken, DateTimeOffset refreshExpiresAt)
    {
        var accessToken = accessTokens.Issue(user);
        return new LoginResponse(accessToken.Value, "Bearer", accessToken.ExpiresAtUtc,
            new AuthenticatedUserResponse(user.UserId, user.Email, user.Roles, user.Permissions),
            refreshToken, refreshExpiresAt);
    }

    private static (string Plaintext, RefreshToken Token) CreateToken(AuthUser user, Guid id, Guid familyId,
        DateTimeOffset now, DateTimeOffset expiresAt)
    {
        string plaintext = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        return (plaintext, new RefreshToken
        {
            Id = id, UserId = user.UserId, FamilyId = familyId, TokenHash = Hash(plaintext),
            CreatedAt = now, ExpiresAt = expiresAt
        });
    }

    private static bool IsValidFormat(string? token) => token is { Length: 86 }
        && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
