using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Abstraction.Admin.Lots {
    public sealed record CreateLotCommand(
        string Name, 
        string Category, 
        int Quantity, 
        decimal EstimatedValue, 
        string Description, 
        IReadOnlyCollection<string> Tags, 
        Guid AuctionHouseId);
}
