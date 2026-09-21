using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Admin.Employee.CreateEmployee {
    public sealed record CreateEmployeeCommand(
        string FirstName,
        string LastName,
        DateOnly BirthDate,
        string Address,
        Guid AuctionHouseId);
}