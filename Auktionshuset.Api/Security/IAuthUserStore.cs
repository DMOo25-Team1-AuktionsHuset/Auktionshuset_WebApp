namespace Auktionshuset.Api.Security
{
    public interface IAuthUserStore
    {
        public Task<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    }
}
