using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Auktionshuset.Domain.Entities.Admin
{
    public class Employee
    {
        public required Guid EmployeeId { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required DateOnly BirthDate { get; set; }
        public required string Address { get; set; }
    }
}
