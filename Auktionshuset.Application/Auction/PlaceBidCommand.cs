using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Auction {
    public sealed record PlaceBidCommand(
        Guid AuctionLotId,
        Guid CustomerId,
        Guid? DeviceSessionId,
        Guid RequestId,
        decimal Amount);
}
