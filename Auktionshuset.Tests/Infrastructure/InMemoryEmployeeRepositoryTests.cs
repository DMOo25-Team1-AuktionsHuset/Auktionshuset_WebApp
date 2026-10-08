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
    public async Task AddAsync_AddsEmployee()
    {
        var repository = new InMemoryEmployeeRepository();
        Employee employee = NewEmployee();

        await repository.AddAsync(employee, CancellationToken.None);

        Assert.Equal(employee.EmployeeId, (await repository.GetByIdAsync(employee.EmployeeId, CancellationToken.None))?.EmployeeId);
    }

    [Fact]
    public async Task AddAsync_WithDuplicateId_ThrowsInvalidOperationException()
    {
        var repository = new InMemoryEmployeeRepository();
        Employee existing = (await repository.GetAllAsync(CancellationToken.None))[0];

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.AddAsync(existing, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEmployee()
    {
        var repository = new InMemoryEmployeeRepository();
        Employee existing = (await repository.GetAllAsync(CancellationToken.None))[0];
        Employee updated = new()
        {
            EmployeeId = existing.EmployeeId,
            AuctionHouseId = existing.AuctionHouseId,
            FirstName = "Opdateret",
            LastName = existing.LastName,
            BirthDate = existing.BirthDate,
            Address = existing.Address
        };

        await repository.UpdateAsync(updated, CancellationToken.None);

        Assert.Equal("Opdateret", (await repository.GetByIdAsync(existing.EmployeeId, CancellationToken.None))?.FirstName);
    }

    [Fact]
    public async Task DeleteAsync_RemovesKnownEmployee()
    {
        var repository = new InMemoryEmployeeRepository();
        Employee employee = (await repository.GetAllAsync(CancellationToken.None))[0];

        Assert.True(await repository.DeleteAsync(employee.EmployeeId, CancellationToken.None));
        Assert.Null(await repository.GetByIdAsync(employee.EmployeeId, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseForUnknownId()
    {
        Assert.False(await new InMemoryEmployeeRepository().DeleteAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_WhenCancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new InMemoryEmployeeRepository().AddAsync(NewEmployee(), cancellation.Token));
    }

    [Fact]
    public async Task DeleteAsync_WhenCancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new InMemoryEmployeeRepository().DeleteAsync(Guid.NewGuid(), cancellation.Token));
    }

    private static Employee NewEmployee() => new()
    {
        EmployeeId = Guid.NewGuid(),
        AuctionHouseId = Guid.NewGuid(),
        FirstName = "Anna",
        LastName = "Test",
        BirthDate = new DateOnly(1990, 1, 1),
        Address = "Testvej 1"
    };

    [Fact]
    public async Task GetAllAsync_WhenCancelled_Throws()
    {
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new InMemoryEmployeeRepository().GetAllAsync(cancellation.Token));
    }
}
