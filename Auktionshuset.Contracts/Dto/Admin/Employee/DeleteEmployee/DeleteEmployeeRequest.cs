using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Auktionshuset.Contracts.Dto.Admin.Employee.DeleteEmployee
{
    public sealed class DeleteEmployeeRequest : IValidatableObject
    {
        [Required]
        public Guid EmployeeId { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EmployeeId == Guid.Empty)
            {
                yield return new ValidationResult("EmployeeId is required.", [nameof(EmployeeId)]);
            }
        }
    }
}
