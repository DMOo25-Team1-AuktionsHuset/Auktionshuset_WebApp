using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Employees.DeleteEmployee;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Employee;
using Auktionshuset.Contracts.Dto.Admin.Employee.DeleteEmployee;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Employee
{
    public class DeleteEmployeeRealTimeHandler(
        IHubContext<EmployeeHub, IEmployeeClient> hubContext)
        : IIntegrationEventHandler<EmployeeDeletedIntegrationEvent>
    {
        public Task HandleAsync(EmployeeDeletedIntegrationEvent message, CancellationToken cancellationToken)
        {
            DeleteEmployeeNotification notification = new DeleteEmployeeNotification(
                EmployeeId: message.EmployeeId);
            return hubContext.Clients.All.EmployeeDeletedAsync(notification);
        }
    }
}
