using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Admin.Employees.UpdateEmployee
{
    public class UpdateEmployeeHandler(IEmployeeRepository employeeRepository, IOutboxWriter outboxWriter, IUnitOfWork unitOfWork)
    {
        public async Task<UpdateEmployeeResult?> HandleAsync(UpdateEmployeeCommand command, CancellationToken cancellationToken)
        {
            Employee? employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);

            if (employee == null)
            {
                return null;
            }

            employee.FirstName = command.FirstName;
            employee.LastName = command.LastName;
            employee.BirthDate = command.BirthDate;
            employee.Address = command.Address;
            employee.AuctionHouseId = command.AuctionHouseId;

            await employeeRepository.UpdateAsync(employee, cancellationToken);

            EmployeeUpdatedIntegrationEvent integrationEvent = new EmployeeUpdatedIntegrationEvent(
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

            return new UpdateEmployeeResult(employee.EmployeeId);
        }
    }
}
