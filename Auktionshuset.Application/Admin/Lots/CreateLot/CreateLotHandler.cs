using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Application.Abstraction;

namespace Auktionshuset.Application.Admin.Lots.CreateLot {
    public class CreateLotHandler(ILotRepository lotRepository, IOutboxWriter outboxWriter) 
    {
        /// <summary>
        /// Creates a lot from the specified command, adds it to the repository, and publishes a
        /// <see cref="LotCreatedIntegrationEvent"/> for it.
        /// </summary>
        /// <param name="command">The values used to create the new lot.</param>
        /// <returns>A result containing the identifier of the newly created lot.</returns>
        public async Task<CreateLotResult> HandleAsync(CreateLotCommand command, CancellationToken cancellationToken) {
            Lot lot = new Domain.Entities.Lot {
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

            LotCreatedIntegrationEvent integrationEvent = new LotCreatedIntegrationEvent(
                EventId: Guid.NewGuid(),
                LotId: lot.LotId,
                AuctionHouseId: lot.AuctionHouseId,
                Name: lot.Name,
                Category: lot.Category,
                Quantity: lot.Quantity,
                EstimatedValue: lot.EstimatedValue,
                OccurredAt: DateTime.Now);

            await outboxWriter.AddAsync(integrationEvent, cancellationToken);

            return new CreateLotResult(lot.LotId);
        }
    }
}
