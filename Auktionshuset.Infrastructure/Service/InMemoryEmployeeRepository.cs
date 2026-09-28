using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Domain.Entities;
using System.Collections.Concurrent;

namespace Auktionshuset.Infrastructure.Service
{
    /// <summary>
    /// An in-memory employee store, seeded with the auction house's employees so the auctionarius
    /// picker has something to offer. It is meant to be replaced by a persistent store later.
    /// </summary>
    public class InMemoryEmployeeRepository : IEmployeeRepository
    {
        private static readonly Guid AuctionHouseId = Guid.Parse("2f1b7c4e-8a3d-4c5f-9e10-6d4a8b2c1f30");

        private readonly ConcurrentDictionary<Guid, Employee> _employees = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryEmployeeRepository"/> with the
        /// seeded employees.
        /// </summary>
        public InMemoryEmployeeRepository()
        {
            Add(new Employee
            {
                EmployeeId = Guid.Parse("8c0f5b21-4d6e-4a90-b3c7-1e2f3a4b5c60"),
                AuctionHouseId = AuctionHouseId,
                FirstName = "Mette",
                LastName = "Jørgensen",
                BirthDate = new DateOnly(1979, 4, 12),
                Address = "Strandgade 14, 6100 Haderslev"
            });

            Add(new Employee
            {
                EmployeeId = Guid.Parse("3a7d9e04-5c81-4f2b-8d69-7b0c1a2e4f51"),
                AuctionHouseId = AuctionHouseId,
                FirstName = "Henrik",
                LastName = "Sørensen",
                BirthDate = new DateOnly(1985, 11, 3),
                Address = "Nørregade 27, 6100 Haderslev"
            });

            Add(new Employee
            {
                EmployeeId = Guid.Parse("c14b6f38-9e2a-4715-a8d0-5f3e2c7b9a02"),
                AuctionHouseId = AuctionHouseId,
                FirstName = "Louise",
                LastName = "Bertelsen",
                BirthDate = new DateOnly(1992, 7, 21),
                Address = "Skolegade 8, 6100 Haderslev"
            });
        }

        public Task AddAsync(Employee employee, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteAsync(Guid employeeId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets every employee ordered by name.
        /// </summary>
        /// <returns>A read-only snapshot of all employees.</returns>
        public Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<Employee> employees = _employees.Values
                .OrderBy(employee => employee.FirstName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(employee => employee.LastName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            return Task.FromResult(employees);
        }

        /// <summary>
        /// Gets the employee with the specified identifier.
        /// </summary>
        /// <returns>The matching employee, or <see langword="null"/> when no employee has that identifier.</returns>
        public Task<Employee?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _employees.TryGetValue(employeeId, out Employee? employee);

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
        private void Add(Employee employee) => _employees[employee.EmployeeId] = employee;
    }
}
