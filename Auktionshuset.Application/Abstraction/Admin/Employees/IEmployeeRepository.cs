using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Abstraction.Admin.Employees;

public interface IEmployeeRepository
{
    /// <summary>
    /// Gets every employee, ordered by name.
    /// </summary>
    Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the employee with the specified identifier.
    /// </summary>
    /// <returns>The matching employee, or <see langword="null"/> when no employee has that identifier.</returns>
    Task<Employee?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken);
}
