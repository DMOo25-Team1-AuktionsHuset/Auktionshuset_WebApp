using Auktionshuset.Application.Abstraction.Admin.Employees;

namespace Auktionshuset.Application.Admin.Employees;

public sealed class GetEmployeesHandler(IEmployeeRepository employeeRepository)
{
    /// <summary>
    /// Gets every employee ordered by name, so they can be offered as auctionarius.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A read-only list of every stored employee.</returns>
    public Task<IReadOnlyList<Domain.Entities.Employee>> HandleAsync(CancellationToken cancellationToken) =>
        employeeRepository.GetAllAsync(cancellationToken);
}
