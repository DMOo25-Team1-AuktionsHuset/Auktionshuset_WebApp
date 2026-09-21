using Auktionshuset.Application.EventHandling;
using Microsoft.AspNetCore.SignalR;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Employee.DeleteEmployee;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Employee;
using Auktionshuset.Contracts.Dto.Admin.Employee.DeleteEmployee;

namespace Auktionshuset.Api.Events.Admin.Employee

{
    public class DeleteEmployeeRealTimeHandler(
        IHubContext<EmployeeHub, IEmployeeClient> hubContext)
        : IIntegrationEventHandler<EmployeeDeletedIntegrationEvent>
    {
        public Task HandleAsync(EmployeeDeletedIntegrationEvent message, CancellationToken cancellationToken)
        {
            var notification = new DeleteEmployeeNotification(
                EmployeeId: message.EmployeeId);
            return hubContext.Clients.All.EmployeeDeletedAsync(notification);
        }
    }
}
