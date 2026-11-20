using Microsoft.AspNetCore.Identity;

namespace Auktionshuset.Domain.Entities
{
    /// <summary>
    /// Identity account linked to a customer or employee, or a provisioned system account.
    /// </summary>
    public class LoginUser : IdentityUser
    {
        // Preserve the public Guid used by JWTs, refresh tokens and historical billing audits.
        public required Guid UserId { get; set; }
        public required string Role { get; set; }
        public required Guid? CustomerId { get; set; }
        public Guid? EmployeeId { get; set; }
        public Employee? Employee { get; set; }
        public bool IsActive { get; set; } = true;
        public int SessionVersion { get; set; }
        public bool IsSystemAccount { get; set; }

        public Customer? Customer { get; set; }
    }
}
