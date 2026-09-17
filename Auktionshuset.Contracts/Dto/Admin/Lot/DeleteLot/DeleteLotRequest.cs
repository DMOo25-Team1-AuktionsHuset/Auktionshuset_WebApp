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

        /// <summary>
        /// Validates that a lot identifier was supplied.
        /// </summary>
        /// <param name="validationContext">The context supplied by the validation framework.</param>
        /// <returns>A sequence of <see cref="ValidationResult"/> instances describing every failure found; the sequence is empty when the request is valid.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (LotId == Guid.Empty)
            {
                yield return new ValidationResult("Lot ID is required", [nameof(LotId)]);
            }
        }
    }
}
