using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Admin.Employees;

public sealed class GetEmployeeByIdHandler(IEmployeeRepository employeeRepository)
{
    public Task<Employee?> HandleAsync(Guid employeeId, CancellationToken cancellationToken) =>
        employeeRepository.GetByIdAsync(employeeId, cancellationToken);
}
