using System.ComponentModel.DataAnnotations;

namespace Auktionshuset.Contracts.Dto.Admin.Auction;

public sealed class CreateAuctionRequest : IValidatableObject
{
    [Required(ErrorMessage = "Startdato og starttidspunkt er påkrævet.")]
    public DateTime? StartsAt { get; init; }

    public Guid[] LotIds { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAt is { } startsAt && startsAt <= DateTime.Now)
        {
            yield return new ValidationResult(
                "Starttidspunktet skal ligge i fremtiden.",
                [nameof(StartsAt)]);
        }

        if (LotIds is not null && LotIds.Distinct().Count() != LotIds.Length)
        {
            yield return new ValidationResult(
                "Den samme lot kan ikke tilføjes mere end én gang.",
                [nameof(LotIds)]);
        }
    }
}
