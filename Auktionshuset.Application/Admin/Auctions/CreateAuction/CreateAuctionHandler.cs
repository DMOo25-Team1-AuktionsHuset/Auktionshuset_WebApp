using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Domain.Entities;
using AuctionEntity = Auktionshuset.Domain.Entities.Auction;

namespace Auktionshuset.Application.Admin.Auctions.CreateAuction;

public sealed class CreateAuctionHandler(
    IAuctionRepository auctionRepository,
    ILotRepository lotRepository,
    IEmployeeRepository employeeRepository,
    IIntegrationEventPublisher eventPublisher)
{
    public async Task<CreateAuctionResult> HandleAsync(
        CreateAuctionCommand command,
        CancellationToken cancellationToken)
    {
        List<string> errors = new List<string>();

        var employee = command.EmployeeId is { } employeeId
            ? await employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            : null;

        AuctionValidation.CollectErrors(
            command.Name,
            command.StartsAt,
            command.EndsAt,
            command.EmployeeId,
            employee,
            requireFutureStart: true,
            errors);

        AuctionEntity auction = new AuctionEntity
        {
            AuctionId = Guid.NewGuid(),
            Name = command.Name.Trim(),
            StartsAt = command.StartsAt,
            EndsAt = command.EndsAt,
            EmployeeId = employee?.EmployeeId,
            Employee = employee,
            AuctionHouseId = command.AuctionHouseId,
            AuctionStatus = AuctionStatuses.Derive(command.StartsAt, command.EndsAt, DateTime.Now)
        };

        IReadOnlyCollection<AuctionLot> auctionLots = [];
        if (command.Lots.Count > 0)
        {
            var lots = await lotRepository.GetAllAsync(cancellationToken);
            auctionLots = AuctionValidation.BuildAuctionLots(
                command.Lots,
                lots.ToDictionary(lot => lot.LotId),
                auction,
                errors);
        }

        if (errors.Count > 0)
        {
            return CreateAuctionResult.Invalid(errors);
        }

        var itemCount = AuctionValidation.CountItems(auctionLots);

        await auctionRepository.AddAsync(auction, auctionLots, cancellationToken);

        AuctionCreatedIntegrationEvent integrationEvent = new AuctionCreatedIntegrationEvent(
            EventId: Guid.NewGuid(),
            AuctionId: auction.AuctionId,
            Name: auction.Name,
            Status: auction.AuctionStatus,
            StartsAt: auction.StartsAt,
            EndsAt: auction.EndsAt,
            LotCount: auctionLots.Count,
            ItemCount: itemCount,
            OccurredAt: DateTime.Now);

        await eventPublisher.PublishAsync(integrationEvent, cancellationToken);

        return CreateAuctionResult.Created(auction.AuctionId, auctionLots.Count, itemCount);
    }
}
