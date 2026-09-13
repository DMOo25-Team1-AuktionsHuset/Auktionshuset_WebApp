using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Abstraction.Admin.Lots
{
    public sealed record LotCreatedIntegrationEvent(

            Guid EventId,
            Guid LotId,
            Guid AuctionHouseId,
            string Name,
            string Category,
            int Quantity,
            decimal EstimatedValue,
            DateTime OccurredAt) : IIntegrationEvent;
}

