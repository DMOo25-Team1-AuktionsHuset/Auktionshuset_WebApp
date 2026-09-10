using System.ComponentModel.DataAnnotations;

namespace Auktionshuset.Contracts.Dto.Admin.Lot {
    public sealed class CreateLotRequest : IValidatableObject {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; init; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Category { get; init; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; init; }

        [Range(typeof(decimal), "0.01", "50000000", ParseLimitsInInvariantCulture = true)]
        public decimal EstimatedValue { get; init; }

        [Required]
        [StringLength(2_000)]
        public string Description { get; init; } = string.Empty;

        [MaxLength(20)]
        public string[] Tags { get; init; } = [];

        public Guid AuctionHouseId { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) {
            if(AuctionHouseId == Guid.Empty) {
                yield return new ValidationResult("Auction house ID is required", [nameof(AuctionHouseId)]);
            }

            if(Tags.Any(string.IsNullOrWhiteSpace)) {
                yield return new ValidationResult("Tags cannot be empty", [nameof(Tags)]);
            }
        }
    }
}
