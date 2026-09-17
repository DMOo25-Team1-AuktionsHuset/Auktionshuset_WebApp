using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Domain.Entities;
using AuctionEntity = Auktionshuset.Domain.Entities.Auction;

namespace Auktionshuset.Application.Admin.Auctions.CreateAuction;

public sealed class CreateAuctionHandler(
    IAuctionRepository auctionRepository,
    ILotRepository lotRepository,
    IIntegrationEventPublisher eventPublisher)
{
    private const string PlannedStatus = "Planlagt";

    public async Task<CreateAuctionResult> HandleAsync(
        CreateAuctionCommand command,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (command.StartsAt <= DateTime.Now)
        {
            errors.Add("Starttidspunktet skal ligge i fremtiden.");
        }

        var lotIds = command.LotIds.ToArray();
        if (lotIds.Distinct().Count() != lotIds.Length)
        {
            errors.Add("Den samme lot kan ikke tilføjes mere end én gang.");
        }

        if (errors.Count > 0)
        {
            return CreateAuctionResult.Invalid(errors);
        }

        var auction = new AuctionEntity
        {
            AuctionId = Guid.NewGuid(),
            StartsAt = command.StartsAt,
            AuctionStatus = PlannedStatus
        };

        IReadOnlyCollection<AuctionLot> auctionLots = [];
        if (lotIds.Length > 0)
        {
            var lots = await lotRepository.GetAllAsync(cancellationToken);
            var lotsById = lots.ToDictionary(lot => lot.LotId);

            if (lotIds.Any(lotId => !lotsById.ContainsKey(lotId)))
            {
                return CreateAuctionResult.Invalid(["En eller flere af de valgte lots findes ikke i lageret."]);
            }

            auctionLots = lotIds
                .Select(lotId => new AuctionLot
                {
                    AuctionLotId = Guid.NewGuid(),
                    AuctionId = auction,
                    LotId = lotsById[lotId]
                })
                .ToArray();
        }

        await auctionRepository.AddAsync(auction, auctionLots, cancellationToken);

        var integrationEvent = new AuctionCreatedIntegrationEvent(
            EventId: Guid.NewGuid(),
            AuctionId: auction.AuctionId,
            StartsAt: auction.StartsAt,
            LotCount: auctionLots.Count,
            OccurredAt: DateTime.Now);

        await eventPublisher.PublishAsync(integrationEvent, cancellationToken);

        return CreateAuctionResult.Created(auction.AuctionId, auctionLots.Count);
    }
}
