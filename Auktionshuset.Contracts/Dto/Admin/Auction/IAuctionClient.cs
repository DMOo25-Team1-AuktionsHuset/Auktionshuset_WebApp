using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Contracts.Dto.Admin.Auction {
    public interface IAuctionClient {
        Task AuctionCreatedAsync(CreateAuctionNotification notification);
    }
}
