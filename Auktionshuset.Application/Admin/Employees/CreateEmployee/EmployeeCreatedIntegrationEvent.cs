using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Employees.CreateEmployee
{
    public sealed record EmployeeCreatedIntegrationEvent(
        Guid EventId,
        Guid EmployeeId,
        Guid AuctionHouseId,
        string FirstName,
        string LastName,
        DateOnly BirthDate,
        string Address,
        DateTime OccurredAt) : IIntegrationEvent;
}
