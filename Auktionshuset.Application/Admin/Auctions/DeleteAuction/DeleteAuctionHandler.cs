using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Admin.Auctions;

namespace Auktionshuset.Application.Admin.Auctions.DeleteAuction;

public sealed class DeleteAuctionHandler(
    IAuctionRepository auctionRepository,
    IOutboxWriter outboxWriter,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Deletes the auction referenced by the command and tells connected clients about it.
    /// </summary>
    /// <param name="command">The command identifying the auction to delete.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the auction was found and deleted; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> HandleAsync(DeleteAuctionCommand command, CancellationToken cancellationToken)
    {
        bool deleted = await auctionRepository.DeleteAsync(command.AuctionId, cancellationToken);

        if (!deleted)
        {
            return false;
        }

        await outboxWriter.AddAsync(
            new AuctionDeletedIntegrationEvent(
                EventId: Guid.NewGuid(),
                AuctionId: command.AuctionId,
                OccurredAt: DateTime.Now),
            cancellationToken);

        await unitOfWork.CommitBatchAsync(cancellationToken);

        return true;
    }
}
