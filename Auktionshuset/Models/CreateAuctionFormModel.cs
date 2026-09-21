using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Auktionshuset.Models;

/// <summary>
/// The values captured by the auction form. Dates and times are kept apart because the browser
/// inputs for them are separate fields.
/// </summary>
public sealed class CreateAuctionFormModel : IValidatableObject
{
    // Browsers may report a time input as "HH:mm:ss", so both formats are accepted.
    private static readonly string[] TimeFormats = ["HH:mm", "HH:mm:ss"];

    private const string TimePattern = @"^([01]\d|2[0-3]):[0-5]\d(:[0-5]\d)?$";

    [Required(ErrorMessage = "Auktionsnavn er påkrævet.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Auktionsnavn skal være mellem 2 og 120 tegn.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vælg en startdato.")]
    public DateTime? StartDate { get; set; }

    [Required(ErrorMessage = "Angiv et starttidspunkt.")]
    [RegularExpression(TimePattern, ErrorMessage = "Starttidspunktet skal angives som tt:mm.")]
    public string StartTime { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vælg en slutdato.")]
    public DateTime? EndDate { get; set; }

    [Required(ErrorMessage = "Angiv et sluttidspunkt.")]
    [RegularExpression(TimePattern, ErrorMessage = "Sluttidspunktet skal angives som tt:mm.")]
    public string EndTime { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vælg en auktionarius.")]
    public Guid? EmployeeId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the start time has to lie in the future. It is turned
    /// off while editing an auction that has already started.
    /// </summary>
    public bool RequireFutureStart { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RequireFutureStart
            && TryCombine(StartDate, StartTime, out var upcomingStart)
            && upcomingStart <= DateTime.Now)
        {
            yield return new ValidationResult(
                "Starttidspunktet skal ligge i fremtiden.",
                [nameof(StartDate)]);
        }

        if (TryCombine(StartDate, StartTime, out var start)
            && TryCombine(EndDate, EndTime, out var end)
            && end <= start)
        {
            yield return new ValidationResult(
                "Sluttidspunktet skal ligge efter starttidspunktet.",
                [nameof(EndDate)]);
        }
    }

    /// <summary>
    /// Combines the start date and start time into a single moment.
    /// </summary>
    /// <returns>The start moment, or <see langword="null"/> when the fields are not filled in yet.</returns>
    public DateTime? GetStartsAt() => TryCombine(StartDate, StartTime, out var value) ? value : null;

    /// <summary>
    /// Combines the end date and end time into a single moment.
    /// </summary>
    /// <returns>The end moment, or <see langword="null"/> when the fields are not filled in yet.</returns>
    public DateTime? GetEndsAt() => TryCombine(EndDate, EndTime, out var value) ? value : null;

    private static bool TryCombine(DateTime? date, string time, out DateTime value)
    {
        value = default;

        if (date is null
            || !TimeOnly.TryParseExact(time, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        value = date.Value.Date.Add(parsed.ToTimeSpan());
        return true;
    }
}
