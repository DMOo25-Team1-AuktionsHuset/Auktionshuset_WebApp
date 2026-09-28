using Xunit;
using Auktionshuset.Infrastructure.Service;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Tests;

public class InMemoryEmployeeRepositoryTests
{
    /// <summary>
    /// Verifies that the store is seeded, so the auctionarius picker is usable out of the box.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_IsSeededWithEmployees()
    {
        IReadOnlyList<Employee> employees = await new InMemoryEmployeeRepository().GetAllAsync(CancellationToken.None);

        Assert.NotEmpty(employees);
        Assert.All(employees, employee => Assert.False(string.IsNullOrWhiteSpace(employee.FirstName)));
        Assert.All(employees, employee => Assert.False(string.IsNullOrWhiteSpace(employee.LastName)));
    }

    /// <summary>
    /// Verifies that employees are returned ordered by name.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_OrdersByFirstName()
    {
        IReadOnlyList<Employee> employees = await new InMemoryEmployeeRepository().GetAllAsync(CancellationToken.None);

        string[] expected = employees
            .Select(employee => employee.FirstName)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        Assert.Equal(expected, employees.Select(employee => employee.FirstName));
    }

    /// <summary>
    /// Verifies that an employee can be looked up by identifier.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_WithSeededEmployee_ReturnsIt()
    {
        var repository = new InMemoryEmployeeRepository();
        Employee seeded = (await repository.GetAllAsync(CancellationToken.None))[0];

        Employee? employee = await repository.GetByIdAsync(seeded.EmployeeId, CancellationToken.None);

        Assert.NotNull(employee);
        Assert.Equal(seeded.EmployeeId, employee.EmployeeId);
    }

    /// <summary>
    /// Verifies that an unknown identifier returns nothing.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        Employee? employee = await new InMemoryEmployeeRepository().GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(employee);
    }

    /// <summary>
    /// Verifies that a cancelled request is honoured.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_WhenCancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new InMemoryEmployeeRepository().GetAllAsync(cancellation.Token));
    }
}
