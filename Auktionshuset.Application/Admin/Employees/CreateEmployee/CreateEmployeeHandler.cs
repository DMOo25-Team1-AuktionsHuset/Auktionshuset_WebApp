using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Application.Admin.Employees.CreateEmployee
{
    public class CreateEmployeeHandler(IEmployeeRepository employeeRepository, IOutboxWriter outboxWriter, IUnitOfWork unitOfWork)
    {
        public async Task<CreateEmployeeResult> HandleAsync(CreateEmployeeCommand command, CancellationToken cancellationToken)
        {
            var employee = new Domain.Entities.Employee
            {
                EmployeeId = Guid.NewGuid(),
                FirstName = command.FirstName,
                LastName = command.LastName,
                BirthDate = command.BirthDate,
                Address = command.Address,
                AuctionHouseId = command.AuctionHouseId
            };

            await employeeRepository.AddAsync(employee, cancellationToken);

            EmployeeCreatedIntegrationEvent integrationEvent = new EmployeeCreatedIntegrationEvent(
                EventId: Guid.NewGuid(),
                EmployeeId: employee.EmployeeId,
                AuctionHouseId: employee.AuctionHouseId,
                FirstName: employee.FirstName,
                LastName: employee.LastName,
                BirthDate: employee.BirthDate,
                Address: employee.Address,
                OccurredAt: DateTime.Now);

            await outboxWriter.AddAsync(integrationEvent, cancellationToken);

            await unitOfWork.CommitBatchAsync(cancellationToken);

            return new CreateEmployeeResult(employee.EmployeeId);
        }
    }
}
