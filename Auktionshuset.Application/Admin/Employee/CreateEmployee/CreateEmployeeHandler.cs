using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace Auktionshuset.Application.Admin.Employee.CreateEmployee {
    public class CreateEmployeeHandler(IEmployeeRepository employeeRepository, IIntegrationEventPublisher eventPublisher) {
        public async Task<CreateEmployeeResult> HandleAsync(CreateEmployeeCommand command, CancellationToken cancellationToken) {
            var employee = new Domain.Entities.Employee {
                EmployeeId = Guid.NewGuid(),
                FirstName = command.FirstName,
                LastName = command.LastName,
                BirthDate = command.BirthDate,
                Address = command.Address,
                AuctionHouseId = command.AuctionHouseId
            };

            await employeeRepository.AddAsync(employee, cancellationToken);

            var integrationEvent = new EmployeeCreatedIntegrationEvent(
                EventId: Guid.NewGuid(),
                EmployeeId: employee.EmployeeId,
                AuctionHouseId: employee.AuctionHouseId,
                FirstName: employee.FirstName,
                LastName: employee.LastName,
                BirthDate: employee.BirthDate,
                Address: employee.Address,
                OccurredAt: DateTime.Now);

            await eventPublisher.PublishAsync(integrationEvent, cancellationToken);

            return new CreateEmployeeResult(employee.EmployeeId);
        }
    }
}
