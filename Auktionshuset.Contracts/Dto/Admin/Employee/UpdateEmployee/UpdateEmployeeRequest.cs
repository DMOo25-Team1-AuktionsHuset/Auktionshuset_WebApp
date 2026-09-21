using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Auktionshuset.Contracts.Dto.Admin.Employee.UpdateEmployee {
    public class UpdateEmployeeRequest : IValidatableObject {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string FirstName { get; init; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string LastName { get; init; }

        public DateOnly BirthDate { get; init; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Address { get; init; }

        public Guid AuctionHouseId { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) {
            if(BirthDate == default) {
                yield return new ValidationResult("Birth date is required.", [nameof(BirthDate)]);
            }

            if(BirthDate > DateOnly.FromDateTime(DateTime.Today)) {
                yield return new ValidationResult("Birth date cannot be in the future.", [nameof(BirthDate)]);
            }

            if(AuctionHouseId == Guid.Empty) {
                yield return new ValidationResult("Auction house ID is required.", [nameof(AuctionHouseId)]);
            }
        }
    }
}
