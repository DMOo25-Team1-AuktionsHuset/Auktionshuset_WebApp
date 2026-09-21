using Auktionshuset.Application.EventHandling;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Admin.Employees.UpdateEmployee {
    public sealed record EmployeeUpdatedIntegrationEvent(
    Guid EventId,
    Guid EmployeeId,
    Guid AuctionHouseId,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    string Address,
    DateTime OccurredAt) : IIntegrationEvent;
}
