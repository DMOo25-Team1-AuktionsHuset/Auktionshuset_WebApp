using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.Admin.Lots.Images;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Admin.Lots.UpdateLot
{
    public class UpdateLotHandler(ILotRepository lotRepository, IOutboxWriter outboxWriter, IUnitOfWork unitOfWork)
    {
        /// <summary>
        /// Applies the command's values to an already stored lot and publishes an integration event
        /// describing the result.
        /// </summary>
        /// <returns>The identifier of the updated lot, or <see langword="null"/> when no lot matches the command.</returns>
        public async Task<UpdateLotResult?> HandleAsync(UpdateLotCommand command, CancellationToken cancellationToken)
        {
            Lot? lot = await lotRepository.GetByIdAsync(command.LotId, cancellationToken);

            if (lot == null)
            {
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

            await LotNotificationPublisher.AddUpdatedAsync(lot, outboxWriter, cancellationToken);

            await unitOfWork.CommitBatchAsync(cancellationToken);

            return new UpdateLotResult(lot.LotId);
        }
    }
}
