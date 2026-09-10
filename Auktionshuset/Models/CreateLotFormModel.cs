using System.ComponentModel.DataAnnotations;

namespace Auktionshuset.Models;

public sealed class CreateLotFormModel : IValidatableObject
{
    [Required(ErrorMessage = "Navn er påkrævet.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Navn skal være mellem 2 og 100 tegn.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kategori er påkrævet.")]
    [StringLength(50, ErrorMessage = "Kategori må højst være 50 tegn.")]
    public string Category { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Antal skal være mindst 1.")]
    public int Quantity { get; set; } = 1;

    [Range(typeof(decimal), "0.01", "50000000", ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Estimeret værdi skal være mellem 0,01 og 50.000.000.")]
    public decimal EstimatedValue { get; set; }

    [Required(ErrorMessage = "Beskrivelse er påkrævet.")]
    [StringLength(2_000, ErrorMessage = "Beskrivelse må højst være 2.000 tegn.")]
    public string Description { get; set; } = string.Empty;

    public string Tags { get; set; } = string.Empty;

    [Required(ErrorMessage = "Auktionshus-id er påkrævet.")]
    public string AuctionHouseId { get; set; } = string.Empty;

    public string[] GetTags() => Tags
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(AuctionHouseId)
            && (!Guid.TryParse(AuctionHouseId, out var auctionHouseId) || auctionHouseId == Guid.Empty))
        {
            yield return new ValidationResult(
                "Auktionshus-id skal være et gyldigt id og må ikke være tomt.",
                [nameof(AuctionHouseId)]);
        }

        if (GetTags().Length > 20)
        {
            yield return new ValidationResult(
                "Du kan højst angive 20 tags.",
                [nameof(Tags)]);
        }
    }
}
