using Auktionshuset.Api.Endpoints.Admin.GetEmployees;
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
        var result = await GetEmployeesEndpoint.HandleAsync(
            new GetEmployeesHandler(new InMemoryEmployeeRepository()),
            CancellationToken.None);

        var employees = Assert.IsType<Ok<IReadOnlyList<EmployeeListItemResponse>>>(result).Value!;

        Assert.NotEmpty(employees);
        Assert.All(employees, employee => Assert.NotEqual(Guid.Empty, employee.EmployeeId));
        Assert.All(employees, employee => Assert.False(string.IsNullOrWhiteSpace(employee.FullName)));
    }

    /// <summary>
    /// Verifies that every employee carries both an identifier and a full display name.
    /// </summary>
    [Fact]
    public async Task HandleAsync_BuildsFullNameFromFirstAndLastName()
    {
        var result = await GetEmployeesEndpoint.HandleAsync(
            new GetEmployeesHandler(new InMemoryEmployeeRepository()),
            CancellationToken.None);

        var employees = Assert.IsType<Ok<IReadOnlyList<EmployeeListItemResponse>>>(result).Value!;

        Assert.Contains(employees, employee => employee.FullName.Contains(' ', StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that the endpoint reports an empty list instead of failing when there are no employees.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithoutEmployees_ReturnsEmptyList()
    {
        var result = await GetEmployeesEndpoint.HandleAsync(
            new GetEmployeesHandler(new EmptyEmployeeRepository()),
            CancellationToken.None);

        Assert.Empty(Assert.IsType<Ok<IReadOnlyList<EmployeeListItemResponse>>>(result).Value!);
    }

    /// <summary>
    /// An employee store without any employees, used to check the empty state.
    /// </summary>
    private sealed class EmptyEmployeeRepository : IEmployeeRepository
    {
        public Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Employee>>([]);

        public Task<Employee?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken) =>
            Task.FromResult<Employee?>(null);
    }
}
