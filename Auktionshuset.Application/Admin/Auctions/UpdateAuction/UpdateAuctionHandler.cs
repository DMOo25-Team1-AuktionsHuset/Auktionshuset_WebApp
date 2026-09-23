using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Admin.Auctions.UpdateAuction;

public sealed class UpdateAuctionHandler(
    IAuctionRepository auctionRepository,
    ILotRepository lotRepository,
    IEmployeeRepository employeeRepository,
    IIntegrationEventPublisher eventPublisher)
{
    public async Task<UpdateAuctionResult> HandleAsync(
        UpdateAuctionCommand command,
        CancellationToken cancellationToken)
    {
        var auction = await auctionRepository.GetByIdAsync(command.AuctionId, cancellationToken);

        if (auction is null)
        {
            return UpdateAuctionResult.Missing();
        }

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
            requireFutureStart: false,
            errors);

        var lots = await lotRepository.GetAllAsync(cancellationToken);
        var auctionLots = AuctionValidation.BuildAuctionLots(
            command.Lots,
            lots.ToDictionary(lot => lot.LotId),
            auction,
            errors);

        if (errors.Count > 0)
        {
            return UpdateAuctionResult.Invalid(errors);
        }

        auction.Name = command.Name.Trim();
        auction.StartsAt = command.StartsAt;
        auction.EndsAt = command.EndsAt;
        auction.EmployeeId = employee?.EmployeeId;
        auction.Employee = employee;
        auction.AuctionHouseId = command.AuctionHouseId ?? auction.AuctionHouseId;
        auction.AuctionStatus = AuctionStatuses.Derive(command.StartsAt, command.EndsAt, DateTime.Now);

        var itemCount = AuctionValidation.CountItems(auctionLots);

        var updated = await auctionRepository.UpdateAsync(auction, auctionLots, cancellationToken);

        if (!updated)
        {
            return UpdateAuctionResult.Missing();
        }

        await eventPublisher.PublishAsync(
            new AuctionUpdatedIntegrationEvent(
                EventId: Guid.NewGuid(),
                AuctionId: auction.AuctionId,
                Name: auction.Name,
                Status: auction.AuctionStatus,
                StartsAt: auction.StartsAt,
                EndsAt: auction.EndsAt,
                LotCount: auctionLots.Count,
                ItemCount: itemCount,
                OccurredAt: DateTime.Now),
            cancellationToken);

        return UpdateAuctionResult.Updated(auction.AuctionId, auctionLots.Count, itemCount);
    }
}
