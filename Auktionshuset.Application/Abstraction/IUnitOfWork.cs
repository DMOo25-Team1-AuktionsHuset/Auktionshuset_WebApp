namespace Auktionshuset.Application.Abstraction
{
    public interface IUnitOfWork
    {
        Task CommitBatchAsync(CancellationToken cancellationToken = default);
    }
}
