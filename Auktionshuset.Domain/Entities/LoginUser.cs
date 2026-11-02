namespace Auktionshuset.Domain.Entities
{
    /// <summary>
    /// A stored login belongs to exactly one customer or employee. Bootstrap users remain configured.
    /// </summary>
    public class LoginUser
    {
        public required Guid UserId { get; set; }
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }
        public required string Role { get; set; }
        public required Guid? CustomerId { get; set; }
        public Guid? EmployeeId { get; set; }
        public Employee? Employee { get; set; }
        public bool IsActive { get; set; } = true;
        public int SessionVersion { get; set; }

        public Customer? Customer { get; set; }
    }
}
