using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Contracts.Dto.Admin.Lot {
    public sealed record CreateLotRequest(
        string Name, 
        string Category, 
        int Quantity, 
        decimal EstimatedValue, 
        string Description, 
        IReadOnlyCollection<string> Tags, 
        Guid AuctionHouseId);
}
