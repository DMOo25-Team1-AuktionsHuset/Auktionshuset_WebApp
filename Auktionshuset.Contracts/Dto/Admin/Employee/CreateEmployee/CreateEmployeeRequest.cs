using System.ComponentModel.DataAnnotations;

namespace Auktionshuset.Contracts.Dto.Admin.Employee.CreateEmployee
{
    public sealed class CreateEmployeeRequest : IValidatableObject
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string FirstName { get; init; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string LastName { get; init; } = string.Empty;

        public DateOnly BirthDate { get; init; }

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Address { get; init; } = string.Empty;

        public Guid AuctionHouseId { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (AuctionHouseId == Guid.Empty)
            {
                yield return new ValidationResult("Auction house ID is required", [nameof(AuctionHouseId)]);
            }
        }
    }
}
