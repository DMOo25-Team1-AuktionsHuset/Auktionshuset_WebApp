using Auktionshuset.Application.EventHandling;
using Auktionshuset.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Abstraction.Admin.Lots {
    public class CreateLotHandler(ILotRepository lotRepository, IIntegrationEventPublisher eventPublisher) {
        public async Task<CreateLotResult> HandleAsync(CreateLotCommand command, CancellationToken cancellationToken) {
            var lot = new Domain.Entities.Lot {
                LotId = Guid.NewGuid(),
                Name = command.Name,
                Category = command.Category,
                Quantity = command.Quantity,
                EstimatedValue = command.EstimatedValue,
                Description = command.Description,
                Tags = command.Tags.ToList(),
                AuctionHouseId = command.AuctionHouseId
            };

            await lotRepository.AddAsync(lot, cancellationToken);

            var integrationEvent = new CreateLotIntegrationEvent(
                EventId: Guid.NewGuid(),
                LotId: lot.LotId,
                AuctionHouseId: lot.AuctionHouseId,
                Name: lot.Name,
                Category: lot.Category,
                Quantity: lot.Quantity,
                EstimatedValue: lot.EstimatedValue,
                OccurredAt: DateTime.Now);

            await eventPublisher.PublishAsync(integrationEvent, cancellationToken);

            return new CreateLotResult(lot.LotId);
        }
    }
}
