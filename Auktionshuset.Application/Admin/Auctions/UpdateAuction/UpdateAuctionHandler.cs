using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Domain.Entities;
using AuctionEntity = Auktionshuset.Domain.Entities.Auction;

namespace Auktionshuset.Application.Admin.Auctions.UpdateAuction;

public sealed class UpdateAuctionHandler(
    IAuctionRepository auctionRepository,
    ILotRepository lotRepository,
    IEmployeeRepository employeeRepository,
    IOutboxWriter outboxWriter,
    IUnitOfWork unitOfWork)
{
    public async Task<UpdateAuctionResult> HandleAsync(
        UpdateAuctionCommand command,
        CancellationToken cancellationToken)
    {
        AuctionEntity? auction = await auctionRepository.GetByIdAsync(command.AuctionId, cancellationToken);

        if (auction is null)
        {
            return UpdateAuctionResult.Missing();
        }

        List<string> errors = new List<string>();

        Employee? employee = command.EmployeeId is { } employeeId
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

        if (command.AuctionHouseId is { } auctionHouseId
            && employee is not null
            && employee.AuctionHouseId != auctionHouseId)
        {
            errors.Add("Auktionshuset skal svare til medarbejderens auktionshus.");
        }

        if (errors.Count > 0)
        {
            return UpdateAuctionResult.Invalid(errors);
        }

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

        var assignedEmployee = employee!;
        auction.Name = command.Name.Trim();
        auction.StartsAt = command.StartsAt;
        auction.EndedAt = command.EndsAt;
        auction.EmployeeId = assignedEmployee.EmployeeId;
        auction.Employee = assignedEmployee;
        auction.AuctionHouseId = assignedEmployee.AuctionHouseId;
        auction.AuctionStatus = AuctionStatuses.Derive(command.StartsAt, command.EndsAt, DateTime.Now);

        int itemCount = AuctionValidation.CountItems(auctionLots);

        bool updated = await auctionRepository.UpdateAsync(auction, auctionLots, cancellationToken);

        if (!updated)
        {
            return UpdateAuctionResult.Missing();
        }

        await outboxWriter.AddAsync(
            new AuctionUpdatedIntegrationEvent(
                EventId: Guid.NewGuid(),
                AuctionId: auction.AuctionId,
                Name: auction.Name,
                Status: auction.AuctionStatus,
                StartsAt: auction.StartsAt,
                EndsAt: command.EndsAt,
                LotCount: auctionLots.Count,
                ItemCount: itemCount,
                OccurredAt: DateTime.Now),
            cancellationToken);

        await unitOfWork.CommitBatchAsync(cancellationToken);

        return UpdateAuctionResult.Updated(auction.AuctionId, auctionLots.Count, itemCount);
    }
}
