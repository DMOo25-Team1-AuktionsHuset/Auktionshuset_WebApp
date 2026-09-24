using Auktionshuset.Api.Endpoints.Admin.Employee.GetEmployees;
using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.Admin.Employees;
using Auktionshuset.Contracts.Dto.Admin.Employee;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Auktionshuset.Api.Test.Admins.Employees;

public class GetEmployeesTest
{
    /// <summary>
    /// Verifies that the endpoint offers the stored employees as auctionarius options.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ReturnsSeededEmployees()
    {
        Ok<IReadOnlyList<EmployeeListItemResponse>> result = await GetEmployeesEndpoint.HandleAsync(
            new GetEmployeesHandler(new InMemoryEmployeeRepository()),
            CancellationToken.None);

        IReadOnlyList<EmployeeListItemResponse> employees = Assert.IsType<Ok<IReadOnlyList<EmployeeListItemResponse>>>(result).Value!;

        Assert.NotEmpty(employees);
        Assert.All(employees, employee => Assert.NotEqual(Guid.Empty, employee.EmployeeId));
        Assert.All(employees, employee => Assert.False(string.IsNullOrWhiteSpace(employee.FirstName)));
    }

    /// <summary>
    /// Verifies that the endpoint preserves the employee's separate name fields.
    /// </summary>
    [Fact]
    public async Task HandleAsync_PreservesFirstAndLastName()
    {
        Ok<IReadOnlyList<EmployeeListItemResponse>> result = await GetEmployeesEndpoint.HandleAsync(
            new GetEmployeesHandler(new InMemoryEmployeeRepository()),
            CancellationToken.None);

        IReadOnlyList<EmployeeListItemResponse> employees = Assert.IsType<Ok<IReadOnlyList<EmployeeListItemResponse>>>(result).Value!;

        Assert.Contains(
            employees,
            employee => employee.FirstName == "Mette" && employee.LastName == "Jørgensen");
    }

    /// <summary>
    /// Verifies that the endpoint reports an empty list instead of failing when there are no employees.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithoutEmployees_ReturnsEmptyList()
    {
        Ok<IReadOnlyList<EmployeeListItemResponse>> result = await GetEmployeesEndpoint.HandleAsync(
            new GetEmployeesHandler(new EmptyEmployeeRepository()),
            CancellationToken.None);

        Assert.Empty(Assert.IsType<Ok<IReadOnlyList<EmployeeListItemResponse>>>(result).Value!);
    }

    /// <summary>
    /// An employee store without any employees, used to check the empty state.
    /// </summary>
    private sealed class EmptyEmployeeRepository : IEmployeeRepository
    {
        public Task AddAsync(Employee employee, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteAsync(Guid employeeId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Employee>>([]);

        public Task<Employee?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken) =>
            Task.FromResult<Employee?>(null);

        public Task UpdateAsync(Employee employee, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
