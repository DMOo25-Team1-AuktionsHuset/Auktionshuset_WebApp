using Auktionshuset.Application.EventHandling;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Abstraction.Admin.Lots {
    public sealed record CreateLotIntegrationEvent(
        Guid EventId,
        Guid LotId,
        Guid AuctionHouseId,
        string Name,
        string Category,
        int Quantity,
        decimal EstimatedValue,
        DateTime OccurredAt) : IIntegrationEvent;
}
