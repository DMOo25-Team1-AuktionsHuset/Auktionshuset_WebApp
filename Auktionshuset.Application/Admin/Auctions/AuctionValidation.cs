using Auktionshuset.Domain.Entities;
using AuctionEntity = Auktionshuset.Domain.Entities.Auction;

namespace Auktionshuset.Application.Admin.Auctions;

/// <summary>
/// Holds the business rules that are shared by the create and update auction handlers.
/// </summary>
internal static class AuctionValidation
{
    internal const int MaxNameLength = 120;
    internal const int MinNameLength = 2;

    /// <summary>
    /// Adds an error for every violation of the rules that apply to both creating and updating an
    /// auction.
    /// </summary>
    /// <param name="name">The auction name supplied by the caller.</param>
    /// <param name="startsAt">The supplied start time.</param>
    /// <param name="endsAt">The supplied end time.</param>
    /// <param name="employeeId">The optional auctionarius identifier supplied by the caller.</param>
    /// <param name="employee">The employee looked up for the supplied employee identifier.</param>
    /// <param name="requireFutureStart">
    /// When <see langword="true"/>, the start time has to lie in the future. Updating an auction that
    /// already started must not be blocked by this rule.
    /// </param>
    /// <param name="errors">The list that validation errors are added to.</param>
    internal static void CollectErrors(
        string? name,
        DateTime startsAt,
        DateTime? endsAt,
        Guid? employeeId,
        Employee? employee,
        DateTime nowUtc,
        bool requireFutureStart,
        List<string> errors)
    {
        string trimmedName = name?.Trim() ?? string.Empty;

        if (trimmedName.Length < MinNameLength || trimmedName.Length > MaxNameLength)
        {
            errors.Add($"Auktionsnavn skal være mellem {MinNameLength} og {MaxNameLength} tegn.");
        }

        bool startIsUtc = startsAt.Kind == DateTimeKind.Utc;
        bool endIsUtc = endsAt == null || endsAt.Value.Kind == DateTimeKind.Utc;

        if (!startIsUtc)
            errors.Add("Starttidspunktet skal være angivet som UTC.");

        if (!endIsUtc)
            errors.Add("Sluttidspunktet skal være angivet som UTC.");

        if (requireFutureStart && startIsUtc && startsAt <= nowUtc)
            errors.Add("Starttidspunktet skal ligge i fremtiden.");

        if (startIsUtc && endIsUtc && endsAt is DateTime end && end <= startsAt)
            errors.Add("Sluttidspunktet skal ligge efter starttidspunktet.");

        if (employeeId is not null && employee is null)
        {
            errors.Add("Den valgte auktionarius findes ikke.");
        }
    }

    /// <summary>
    /// Builds the auction/lot relationships for the supplied selections and adds an error for every
    /// selection that is unknown, duplicated or has an invalid quantity.
    /// </summary>
    /// <param name="selections">The lot selections supplied by the caller.</param>
    /// <param name="lotsById">Every stored lot, indexed by its identifier.</param>
    /// <param name="auction">The auction the relationships belong to.</param>
    /// <param name="errors">The list that validation errors are added to.</param>
    /// <returns>The relationships that could be built from the valid selections.</returns>
    internal static IReadOnlyCollection<AuctionLot> BuildAuctionLots(
        IReadOnlyCollection<AuctionLotSelection> selections,
        IReadOnlyDictionary<Guid, Lot> lotsById,
        AuctionEntity auction,
        List<string> errors,
        IReadOnlyDictionary<Guid, long>? reserved = null,
        IReadOnlyDictionary<Guid, decimal>? existingPrices = null)
    {
        if (selections.Select(selection => selection.LotId).Distinct().Count() != selections.Count)
        {
            errors.Add("Den samme genstand kan ikke tilføjes mere end én gang.");
        }

        List<AuctionLot> auctionLots = new List<AuctionLot>();

        foreach (AuctionLotSelection selection in selections)
        {
            if (!lotsById.TryGetValue(selection.LotId, out Lot? lot))
            {
                errors.Add("En eller flere af de valgte genstande findes ikke i lageret.");
                continue;
            }

            if (selection.Quantity < 1)
            {
                errors.Add($"Antallet for \"{lot.Name}\" skal være mindst 1.");
                continue;
            }

            decimal startingPrice = selection.StartingPrice ?? existingPrices?.GetValueOrDefault(selection.LotId) ?? 0m;
            if (startingPrice < 0 || startingPrice > 9999999999999999.99m
                || decimal.Round(startingPrice, 2) != startingPrice)
            {
                errors.Add($"Startprisen for {lot.Name} skal være mellem 0 og 9999999999999999,99 kr. med højst to decimaler.");
                continue;
            }

            long reservedQuantity = reserved?.GetValueOrDefault(lot.LotId) ?? 0;
            if (reservedQuantity == long.MaxValue)
            {
                errors.Add($"Lagerdata for {lot.Name} indeholder ugyldige reserverede antal. Afstem data før reservation.");
                continue;
            }
            long available = lot.Quantity - reservedQuantity;
            if (selection.Quantity > available)
            {
                errors.Add(
                    $"Der er kun {available} ledige stk. af \"{lot.Name}\" på lageret.");
                continue;
            }

            auctionLots.Add(new AuctionLot
            {
                AuctionLotId = Guid.NewGuid(),
                AuctionId = auction.AuctionId,
                Auction = auction,
                LotId = lot.LotId,
                Lot = lot,
                Quantity = selection.Quantity,
                StartingPrice = startingPrice,
                OpenForBids = false,
                CurrentHighestBid = null
            });
        }

        return auctionLots;
    }

    /// <summary>
    /// Counts the total number of units across the supplied relationships.
    /// </summary>
    internal static int CountItems(IReadOnlyCollection<AuctionLot> auctionLots) =>
        auctionLots.Sum(auctionLot => auctionLot.Quantity);
}
