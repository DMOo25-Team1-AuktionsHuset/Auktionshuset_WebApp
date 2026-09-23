using System.ComponentModel.DataAnnotations;

namespace Auktionshuset.Contracts.Dto.Admin.Auction;

public sealed class CreateAuctionRequest : IValidatableObject
{
    [Required(ErrorMessage = "Auktionsnavn er påkrævet.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Auktionsnavn skal være mellem 2 og 120 tegn.")]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = "Startdato og starttidspunkt er påkrævet.")]
    public DateTime? StartsAt { get; init; }

    [Required(ErrorMessage = "Slutdato og sluttidspunkt er påkrævet.")]
    public DateTime? EndsAt { get; init; }

    /// <summary>
    /// Gets the required identifier of the auctionarius assigned to the auction.
    /// </summary>
    [Required(ErrorMessage = "Vælg en auktionarius.")]
    public Guid? EmployeeId { get; init; }

    public Guid? AuctionHouseId { get; init; }

    public AuctionLotRequest[] Lots { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAt is { } startsAt && startsAt <= DateTime.Now)
        {
            yield return new ValidationResult(
                "Starttidspunktet skal ligge i fremtiden.",
                [nameof(StartsAt)]);
        }

        if (StartsAt is { } start && EndsAt is { } end && end <= start)
        {
            yield return new ValidationResult(
                "Sluttidspunktet skal ligge efter starttidspunktet.",
                [nameof(EndsAt)]);
        }

        if (Lots is not null && Lots.Select(lot => lot.LotId).Distinct().Count() != Lots.Length)
        {
            yield return new ValidationResult(
                "Den samme genstand kan ikke tilføjes mere end én gang.",
                [nameof(Lots)]);
        }

        if (Lots is not null && Lots.Any(lot => lot.Quantity < 1))
        {
            yield return new ValidationResult(
                "Antallet for en valgt genstand skal være mindst 1.",
                [nameof(Lots)]);
        }
    }
}
