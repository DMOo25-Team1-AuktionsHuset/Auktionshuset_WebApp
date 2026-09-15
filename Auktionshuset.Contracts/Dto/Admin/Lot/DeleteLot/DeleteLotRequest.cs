using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Auktionshuset.Contracts.Dto.Admin.Lot.DeleteLot
{
    public sealed class DeleteLotRequest : IValidatableObject
    {
        [Required]
        public Guid LotId { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (LotId == Guid.Empty)
            {
                yield return new ValidationResult("Lot ID is required", [nameof(LotId)]);
            }
        }
    }
}
