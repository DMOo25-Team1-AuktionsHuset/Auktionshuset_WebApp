using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Employees.UpdateEmployee;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Employee;
using Auktionshuset.Contracts.Dto.Admin.Employee.UpdateEmployee;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Employee {
    public class UpdateEmployeeRealTimeHandler(IHubContext<EmployeeHub, IEmployeeClient> hubContext) : IIntegrationEventHandler<EmployeeUpdatedIntegrationEvent> {
        public Task HandleAsync(EmployeeUpdatedIntegrationEvent message, CancellationToken cancellationToken) {
            UpdateEmployeeNotification notification = new UpdateEmployeeNotification(
                EventId: message.EventId,
                EmployeeId: message.EmployeeId,
                AuctionHouseId: message.AuctionHouseId,
                FirstName: message.FirstName,
                LastName: message.LastName,
                BirthDate: message.BirthDate,
                Address: message.Address,
                OccurredAt: message.OccurredAt);

            return hubContext.Clients.All.EmployeeUpdatedAsync(notification);
        }
    }
}
