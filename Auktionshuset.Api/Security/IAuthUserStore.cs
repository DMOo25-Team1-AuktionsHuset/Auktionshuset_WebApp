namespace Auktionshuset.Api.Security
{
    public interface IAuthUserStore
    {
        public Task<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);
        Task<AuthUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);
        Task<bool> CheckPasswordAsync(AuthUser user, string password, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<AuthUser>();
            return Task.FromResult(hasher.VerifyHashedPassword(user, user.PasswordHash, password)
                != Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed);
        }
    }
}
