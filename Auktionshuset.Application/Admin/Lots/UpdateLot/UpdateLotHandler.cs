using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.EventHandling;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Admin.Lots.UpdateLot {
    public class UpdateLotHandler(ILotRepository lotRepository, IIntegrationEventPublisher eventPublisher) {
        public async Task<UpdateLotResult?> HandleAsync(UpdateLotCommand command, CancellationToken cancellationToken) {
            var lot = await lotRepository.GetByIdAsync(command.LotId, cancellationToken);

            if(lot == null) {
                return null;
            }

            lot.Name = command.Name;
            lot.Category = command.Category;
            lot.Quantity = command.Quantity;
            lot.EstimatedValue = command.EstimatedValue;
            lot.Description = command.Description;
            lot.Tags = command.Tags.ToList();
            lot.AuctionHouseId = command.AuctionHouseId;

            await lotRepository.UpdateAsync(lot, cancellationToken);

            await eventPublisher.PublishAsync(new LotCreatedIntegrationEvent(
                EventId: Guid.NewGuid(),
                LotId: lot.LotId,
                AuctionHouseId: lot.AuctionHouseId,
                Name: lot.Name,
                Category: lot.Category,
                Quantity: lot.Quantity,
                EstimatedValue: lot.EstimatedValue,
                OccurredAt: DateTime.Now),
                cancellationToken);

            return new UpdateLotResult(lot.LotId);
        }
    }
}
