namespace Auktionshuset.Domain.Entities;

// No plaintext token or password is persisted. FamilyId is the first token's Id.
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    // Stored on the root row; protects against refresh/logout races across API instances.
    public DateTimeOffset? FamilyRevokedAt { get; set; }
}
