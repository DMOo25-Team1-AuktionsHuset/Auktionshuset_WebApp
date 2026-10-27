namespace Auktionshuset.Application.Abstraction.Auction
{
    public interface IAuctionLifeCycleStore
    {
        Task<bool> StartAsync(Guid auctionId, CancellationToken cancellationToken);
        Task<bool> CloseAsync(Guid auctionId, CancellationToken cancellationToken);
    }
}
