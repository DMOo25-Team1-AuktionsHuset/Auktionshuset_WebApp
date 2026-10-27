using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Domain.Entities;
using AuctionEntity = Auktionshuset.Domain.Entities.Auction;

namespace Auktionshuset.Application.Admin.Auctions.UpdateAuction;

public sealed class UpdateAuctionHandler(
    IAuctionRepository auctionRepository,
    ILotRepository lotRepository,
    IEmployeeRepository employeeRepository,
    IOutboxWriter outboxWriter,
    IAuctionWriteSessionFactory sessions,
    TimeProvider clock)
{
    public async Task<UpdateAuctionResult> HandleAsync(
        UpdateAuctionCommand command,
        CancellationToken cancellationToken)
    {
        await using IAuctionWriteSession session = await sessions.BeginAsync(command.AuctionId, cancellationToken);

        AuctionEntity? auction = await auctionRepository.GetByIdAsync(command.AuctionId, cancellationToken);

        if (auction is null)
        {
            return UpdateAuctionResult.Missing();
        }

        if (auction.StartedAtTime is not null
            || auction.ClosedAtTime is not null
            || auction.AuctionStatus != AuctionStatuses.Upcoming)
            return UpdateAuctionResult.Invalid(
            [
                "En startet eller afsluttet auktion kan ikke redigeres."
            ]);

        var errors = new List<string>();

        Employee? employee = command.EmployeeId is { } employeeId
            ? await employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            : null;

        AuctionValidation.CollectErrors(
            command.Name,
            command.StartsAt,
            command.EndsAt,
            command.EmployeeId,
            employee,
            nowUtc: clock.GetUtcNow().UtcDateTime,
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

        string newName = command.Name.Trim();

        Guid? newEmployeeId = employee?.EmployeeId;

        Guid newAuctionHouseId =
            command.AuctionHouseId
            ?? employee?.AuctionHouseId
            ?? auction.AuctionHouseId;

        IReadOnlyList<AuctionLot> existingLots =
            await auctionRepository.GetAuctionLotsAsync(
                auction.AuctionId,
                cancellationToken);

        var selectedQuantities = auctionLots.ToDictionary(
            item => item.LotId,
            item => item.Quantity);

        bool sameLots =
            existingLots.Count == auctionLots.Count
            && existingLots.All(item =>
                selectedQuantities.TryGetValue(
                    item.LotId, 
                    out int quantity)
                && item.Quantity == quantity);

        bool unchanged =
            auction.Name == newName
            && auction.StartsAt == command.StartsAt
            && auction.EndedAt == command.EndsAt
            && auction.EmployeeId == newEmployeeId
            && auction.AuctionHouseId == newAuctionHouseId
            && sameLots;

        if (unchanged) return UpdateAuctionResult.Updated(
            auction.AuctionId,
            existingLots.Count,
            existingLots.Sum(item => item.Quantity));

        auction.Name = newName;
        auction.StartsAt = command.StartsAt;
        auction.EndedAt = command.EndsAt;
        auction.EmployeeId = newEmployeeId;
        auction.Employee = employee;
        auction.AuctionHouseId = newAuctionHouseId;

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
                OccurredAt: clock.GetUtcNow().UtcDateTime),
            cancellationToken);

        await session.CommitAsync(cancellationToken);

        return UpdateAuctionResult.Updated(auction.AuctionId, auctionLots.Count, itemCount);
    }
}
