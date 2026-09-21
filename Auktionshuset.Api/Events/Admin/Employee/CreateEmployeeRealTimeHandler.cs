using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Admin.Employee.CreateEmployee;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Contracts.Dto.Admin.Employee;
using Auktionshuset.Contracts.Dto.Admin.Employee.CreateEmployee;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Events.Admin.Employee {
    public class CreateEmployeeRealTimeHandler(IHubContext<EmployeeHub, IEmployeeClient> hubContext) : IIntegrationEventHandler<EmployeeCreatedIntegrationEvent> {
        public Task HandleAsync(EmployeeCreatedIntegrationEvent message, CancellationToken cancellationToken) {
            var notication = new CreateEmployeeNotification(
                EventId: message.EventId,
                EmployeeId: message.EmployeeId,
                AuctionHouseId: message.AuctionHouseId,
                FirstName: message.FirstName,
                LastName: message.LastName,
                BirthDate: message.BirthDate,
                Address: message.Address,
                OccurredAt: message.OccurredAt);

            return hubContext.Clients.All.EmployeeCreatedAsync(notication);
        }
    }
}
