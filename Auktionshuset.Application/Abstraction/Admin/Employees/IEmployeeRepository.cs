using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Abstraction.Admin.Employees
{
    public interface IEmployeeRepository
    {
        Task AddAsync(Employee employee, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(Guid employeeId, CancellationToken cancellationToken);

        Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken cancellationToken);
        Task<Employee?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken);
        Task UpdateAsync(Employee employee, CancellationToken cancellationToken);
    }
}
