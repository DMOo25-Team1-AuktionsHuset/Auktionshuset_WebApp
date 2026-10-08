using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Repositories
{
    public sealed class EFEmployeeRepository(AHDBContext context)
        :IEmployeeRepository
    {
        public Task AddAsync(Employee employee, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Employee.Add(employee);
            return Task.CompletedTask;
        }

        public async Task<bool> DeleteAsync(Guid employeeId, CancellationToken cancellationToken)
        {
            Employee? employee = await context.Employee.FindAsync(
                [employeeId], cancellationToken);
            if (employee == null)
            {
                return false;
            }

            context.Employee.Remove(employee);
            return true;
        }

        public async Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken cancellationToken)
        {
            return await context.Employee
                .AsNoTracking()
                .OrderBy(employee => employee.LastName)
                .ThenBy(employee => employee.FirstName)
                .ToListAsync(cancellationToken);
        }

        public async Task<Employee?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken)
        {
            return await context.Employee
                .AsNoTracking()
                .FirstOrDefaultAsync(employee => employee.EmployeeId == employeeId, cancellationToken);
        }

        public Task UpdateAsync(Employee employee, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Employee.Update(employee);
            return Task.CompletedTask;
        }
    }
}
