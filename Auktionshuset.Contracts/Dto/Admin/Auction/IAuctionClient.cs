namespace Auktionshuset.Contracts.Dto.Admin.Auction
{
    public interface IAuctionClient
    {
        Task AuctionCreatedAsync(CreateAuctionNotification notification);

        Task AuctionUpdatedAsync(UpdateAuctionNotification notification);

        Task AuctionDeletedAsync(DeleteAuctionNotification notification);
    }
}
