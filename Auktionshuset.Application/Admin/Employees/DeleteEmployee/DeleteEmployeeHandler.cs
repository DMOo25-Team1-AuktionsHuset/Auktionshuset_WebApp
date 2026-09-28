using Auktionshuset.Application.EventHandling;
using Auktionshuset.Application.Abstraction.Admin.Employees;

namespace Auktionshuset.Application.Admin.Employees.DeleteEmployee
{
    public sealed class DeleteEmployeeHandler(
        IEmployeeRepository employeeRepository,
        IIntegrationEventPublisher eventPublisher)
    {
        /// <summary>
        /// Deletes the employee referenced by the specified command.
        /// </summary>
        /// <param name="command">The command identifying the employee to delete.</param>
        /// <returns><see langword="true"/> if the employee was found and deleted; otherwise, <see langword="false"/>.</returns>
        public async Task<bool> HandleAsync(
            DeleteEmployeeCommand command,
            CancellationToken cancellationToken)
        {
            bool deleted = await employeeRepository.DeleteAsync(
                command.EmployeeId,
                cancellationToken);

            if (!deleted)
            {
                return false;
            }

            var integrationEvent = new EmployeeDeletedIntegrationEvent(
                EventId: Guid.NewGuid(),
                EmployeeId: command.EmployeeId,
                OccurredAt: DateTime.UtcNow);

            await eventPublisher.PublishAsync(
                integrationEvent,
                cancellationToken);

            return true;
        }

    }
}
