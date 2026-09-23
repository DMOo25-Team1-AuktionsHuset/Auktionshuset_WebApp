using Auktionshuset.Application.Auction;

namespace Auktionshuset.Application.Abstraction.Auction {
    public interface IPlaceBidStore {
        Task<PlaceBidResult> PlaceBidAsync(PlaceBidCommand command, CancellationToken cancellationToken);
    }
}
