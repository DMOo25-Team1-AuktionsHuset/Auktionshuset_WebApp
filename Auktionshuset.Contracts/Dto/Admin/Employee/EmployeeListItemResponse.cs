using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Contracts.Dto.Admin.Employee
{
    public sealed record EmployeeListItemResponse(
        Guid EmployeeId, 
        string FirstName, 
        string LastName,
        DateOnly BirthDate,
        string Address);
}
