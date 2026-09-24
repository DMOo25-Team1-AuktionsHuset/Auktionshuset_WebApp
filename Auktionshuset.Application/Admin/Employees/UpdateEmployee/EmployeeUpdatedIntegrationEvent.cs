using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Employees.UpdateEmployee
{
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
