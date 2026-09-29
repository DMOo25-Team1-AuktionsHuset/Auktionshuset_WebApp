using Auktionshuset.Application.Abstraction.Auction;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Admin.Auctions.CloseAuction
{
    public class CloseAuctionLotHandler(ICloseAuctionLotStore store)
    {
        public Task<Guid?> HandleAsync(Guid auctionLotId, CancellationToken cancellationToken) =>
            store.CloseAuctionLotAsync(auctionLotId, cancellationToken);
    }
}
