using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Domain.Entities;
using System.Collections.Concurrent;

namespace Auktionshuset.Infrastructure.Service
{
    public class InMemoryEmployeeRepository : IEmployeeRepository
    {
        private readonly ConcurrentDictionary<Guid, Employee> _employees = [];

        public Task AddAsync(Employee employee, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_employees.TryAdd(employee.EmployeeId, employee))
            {
                throw new InvalidOperationException(
                    $"An employee with ID {employee.EmployeeId} already exists");
            }

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(
            Guid employeeId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_employees.TryRemove(employeeId, out _));
        }

        public Task<IReadOnlyList<Employee>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<Employee> employees = _employees.Values
                .OrderBy(employee => employee.LastName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(employee => employee.FirstName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            return Task.FromResult(employees);
        }

        public Task<Employee?> GetByIdAsync(
            Guid employeeId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _employees.TryGetValue(employeeId, out var employee);
            return Task.FromResult(employee);
        }

        public Task UpdateAsync(
            Employee employee,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_employees.ContainsKey(employee.EmployeeId))
            {
                throw new KeyNotFoundException(
                    $"Employee {employee.EmployeeId} was not found");
            }

            _employees[employee.EmployeeId] = employee;
            return Task.CompletedTask;
        }
    }
}
