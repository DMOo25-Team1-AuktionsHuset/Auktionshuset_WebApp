using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Contracts.Dto.Auction {
    public sealed record PlaceBidRequest(Guid RequestId, decimal Amount);
}
