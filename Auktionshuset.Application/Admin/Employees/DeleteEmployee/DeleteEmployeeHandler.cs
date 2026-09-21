using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Application.Abstraction.Admin.Employees;

namespace Auktionshuset.Application.Admin.Employees.DeleteEmployee
{
    public sealed class DeleteEmployeeHandler(IEmployeeRepository employeeRepository)
    {
        /// <summary>
        /// Deletes the employee referenced by the specified command.
        /// </summary>
        /// <param name="command">The command identifying the employee to delete.</param>
        /// <returns><see langword="true"/> if the employee was found and deleted; otherwise, <see langword="false"/>.</returns>
        public Task<bool> HandleAsync(DeleteEmployeeCommand command, CancellationToken cancellationToken) =>
            employeeRepository.DeleteAsync(command.EmployeeId, cancellationToken);
    {
    }
}
