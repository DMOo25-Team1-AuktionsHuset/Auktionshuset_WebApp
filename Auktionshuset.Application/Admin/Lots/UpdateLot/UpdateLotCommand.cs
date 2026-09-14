using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Admin.Lots.UpdateLot {
    public sealed record UpdateLotCommand(
        Guid LotId,
        Guid AuctionHouseId,
        string Name,
        string Category,
        int Quantity,
        decimal EstimatedValue,
        string Description,
        IReadOnlyCollection<string> Tags);
}
