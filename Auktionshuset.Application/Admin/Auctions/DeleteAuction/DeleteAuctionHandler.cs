using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction.Auction;

namespace Auktionshuset.Application.Admin.Auctions.DeleteAuction;

public sealed class DeleteAuctionHandler(
    IAuctionRepository auctionRepository,
    IOutboxWriter outboxWriter,
    IAuctionWriteSessionFactory sessions, TimeProvider clock)
{
    /// <summary>
    /// Deletes the auction referenced by the command and tells connected clients about it.
    /// </summary>
    /// <param name="command">The command identifying the auction to delete.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the auction was found and deleted; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> HandleAsync(DeleteAuctionCommand command, CancellationToken cancellationToken)
    {
        await using IAuctionWriteSession session = 
            await sessions.BeginAsync(
                command.AuctionId,
                cancellationToken);

        var auction = await auctionRepository.GetByIdAsync(command.AuctionId, cancellationToken);

        if (auction is null) return false;

        if (auction.StartedAtTime is not null
            || auction.ClosedAtTime is not null
            || auction.AuctionStatus != AuctionStatuses.Upcoming)
            throw new AuctionStateConflictException("En startet eller afsluttet auktion kan ikke slettes.");

        bool deleted = await auctionRepository.DeleteAsync(command.AuctionId, cancellationToken);

        if (!deleted)
        {
            return false;
        }

        await outboxWriter.AddAsync(
            new AuctionDeletedIntegrationEvent(
                EventId: Guid.NewGuid(),
                AuctionId: command.AuctionId,
                OccurredAt: clock.GetUtcNow().UtcDateTime),
            cancellationToken);

        await session.CommitAsync(cancellationToken);

        return true;
    }
}
