namespace Auktionshuset.Api.Security
{
    public sealed class AuthUser
    {
        public required Guid UserId { get; init; }
        public required string Email { get; init; }
        public required string PasswordHash { get; set; }
        public required IReadOnlyCollection<string> Roles { get; init; }
        public required IReadOnlyCollection<string> Permissions { get; init; } = new List<string>();
    }
}
