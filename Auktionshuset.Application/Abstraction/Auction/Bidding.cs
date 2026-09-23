using Auktionshuset.Application.Auction;

namespace Auktionshuset.Application.Abstraction.Auction
{
    public class PlaceBidHandler(IPlaceBidStore store)
    {
        public Task<PlaceBidResult> HandleAsync(
            PlaceBidCommand command,
            CancellationToken cancellationToken) =>
            store.PlaceBidAsync(command, cancellationToken);
    }
}
