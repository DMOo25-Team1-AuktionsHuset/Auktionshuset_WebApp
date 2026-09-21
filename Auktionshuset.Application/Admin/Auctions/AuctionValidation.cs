using Auktionshuset.Domain.Entities;

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
    /// <param name="employeeId">The auctionarius identifier supplied by the caller, if any.</param>
    /// <param name="employee">The employee looked up for the supplied employee identifier, if any.</param>
    /// <param name="requireFutureStart">
    /// When <see langword="true"/>, the start time has to lie in the future. Updating an auction that
    /// already started must not be blocked by this rule.
    /// </param>
    /// <param name="errors">The list that validation errors are added to.</param>
    internal static void CollectErrors(
        string? name,
        DateTime startsAt,
        DateTime endsAt,
        Guid? employeeId,
        Employee? employee,
        bool requireFutureStart,
        List<string> errors)
    {
        var trimmedName = name?.Trim() ?? string.Empty;

        if (trimmedName.Length < MinNameLength || trimmedName.Length > MaxNameLength)
        {
            errors.Add($"Auktionsnavn skal være mellem {MinNameLength} og {MaxNameLength} tegn.");
        }

        if (requireFutureStart && startsAt <= DateTime.Now)
        {
            errors.Add("Starttidspunktet skal ligge i fremtiden.");
        }

        if (endsAt <= startsAt)
        {
            errors.Add("Sluttidspunktet skal ligge efter starttidspunktet.");
        }

        // An auctionarius is optional, but a supplied one has to exist.
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
        Auction auction,
        List<string> errors)
    {
        if (selections.Select(selection => selection.LotId).Distinct().Count() != selections.Count)
        {
            errors.Add("Den samme genstand kan ikke tilføjes mere end én gang.");
        }

        var auctionLots = new List<AuctionLot>();

        foreach (var selection in selections)
        {
            if (!lotsById.TryGetValue(selection.LotId, out var lot))
            {
                errors.Add("En eller flere af de valgte genstande findes ikke i lageret.");
                continue;
            }

            if (selection.Quantity < 1)
            {
                errors.Add($"Antallet for \"{lot.Name}\" skal være mindst 1.");
                continue;
            }

            if (selection.Quantity > lot.Quantity)
            {
                errors.Add(
                    $"Der er kun {lot.Quantity} stk. af \"{lot.Name}\" på lageret.");
                continue;
            }

            auctionLots.Add(new AuctionLot
            {
                AuctionLotId = Guid.NewGuid(),
                AuctionId = auction.AuctionId,
                Auction = auction,
                LotId = lot.LotId,
                Lot = lot,
                Quantity = selection.Quantity
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
