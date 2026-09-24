using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Employees.DeleteEmployee
{
    public sealed record EmployeeDeletedIntegrationEvent(
        Guid EventId,
        Guid EmployeeId,
        DateTime OccurredAt) : IIntegrationEvent;
}
