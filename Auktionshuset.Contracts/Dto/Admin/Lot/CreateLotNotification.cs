using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Contracts.Dto.Admin.Lot {
    public sealed record CreateLotNotification(
        Guid EventId,
        Guid LotId,
        Guid AuctionHouseId,
        string Name, 
        string Category, 
        int Quantity, 
        decimal EstimatedValue,
        DateTime OccurredAt);
}
