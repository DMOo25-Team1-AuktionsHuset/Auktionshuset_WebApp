using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Auktionshuset.Models;

public sealed class CreateAuctionFormModel : IValidatableObject
{
    [Required(ErrorMessage = "Vælg en startdato.")]
    public DateTime? StartDate { get; set; }

    [Required(ErrorMessage = "Angiv et starttidspunkt.")]
    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Starttidspunktet skal angives som tt:mm.")]
    public string StartTime { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate is null || !TryGetStartTime(out var startTime))
        {
            yield break;
        }

        if (StartDate.Value.Date.Add(startTime.ToTimeSpan()) <= DateTime.Now)
        {
            yield return new ValidationResult(
                "Startdato og starttidspunkt skal ligge i fremtiden.",
                [nameof(StartDate)]);
        }
    }

    public DateTime GetStartsAt() =>
        StartDate!.Value.Date.Add(
            TimeOnly.ParseExact(StartTime, "HH:mm", CultureInfo.InvariantCulture).ToTimeSpan());

    private bool TryGetStartTime(out TimeOnly startTime) =>
        TimeOnly.TryParseExact(StartTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out startTime);
}
