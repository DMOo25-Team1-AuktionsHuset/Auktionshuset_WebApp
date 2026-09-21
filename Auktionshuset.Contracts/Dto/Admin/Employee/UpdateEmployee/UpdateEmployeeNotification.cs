using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Contracts.Dto.Admin.Employee.UpdateEmployee {
    public sealed record UpdateEmployeeNotification(
        Guid EventId,
        Guid EmployeeId,
        Guid AuctionHouseId,
        string FirstName,
        string LastName,
        DateOnly BirthDate,
        string Address,
        DateTime OccurredAt);
}
