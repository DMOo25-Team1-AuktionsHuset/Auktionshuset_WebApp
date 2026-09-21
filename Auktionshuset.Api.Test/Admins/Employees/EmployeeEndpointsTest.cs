using Auktionshuset.Api.Endpoints.Admin.Employee.CreateEmployee;
using Auktionshuset.Api.Endpoints.Admin.Employee.DeleteEmployee;
using Auktionshuset.Api.Endpoints.Admin.Employee.UpdateEmployee;
using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.Admin.Employees.CreateEmployee;
using Auktionshuset.Application.Admin.Employees.DeleteEmployee;
using Auktionshuset.Application.Admin.Employees.UpdateEmployee;
using Auktionshuset.Contracts.Dto.Admin.Employee.CreateEmployee;
using Auktionshuset.Contracts.Dto.Admin.Employee.UpdateEmployee;
using Auktionshuset.Domain.Entities;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Auktionshuset.Api.Test.Admins.Employees;

public class EmployeeEndpointsTest
{
    [Fact]
    public async Task Create_WithValidRequest_TrimsValuesAndReturnsCreatedLocation()
    {
        var repository = new TestEmployeeRepository();
        var publisher = new RecordingEventPublisher();
        var handler = new CreateEmployeeHandler(repository, publisher);
        var request = CreateRequest(
            firstName: "  Anna  ",
            lastName: "  Jensen ",
            address: "  Main Street 1  ");

        var result = await CreateEmployeeEndpoint.HandleAsync(request, handler, CancellationToken.None);

        var response = Assert.IsType<CreateEmployeeResponse>(result.Value);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal($"/api/employees/{response.EmployeeId}", result.Location);

        var saved = await repository.GetByIdAsync(response.EmployeeId, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal("Anna", saved.FirstName);
        Assert.Equal("Jensen", saved.LastName);
        Assert.Equal("Main Street 1", saved.Address);

        var published = Assert.Single(publisher.OfType<EmployeeCreatedIntegrationEvent>());
        Assert.Equal(response.EmployeeId, published.EmployeeId);
    }

    [Fact]
    public async Task Update_WithExistingEmployee_TrimsValuesAndReturnsOk()
    {
        var employee = CreateEmployee();
        var repository = new TestEmployeeRepository(employee);
        var publisher = new RecordingEventPublisher();
        var handler = new UpdateEmployeeHandler(repository, publisher);
        var request = new UpdateEmployeeRequest
        {
            FirstName = "  Updated  ",
            LastName = "  Employee ",
            BirthDate = new DateOnly(1988, 6, 15),
            Address = "  New Address  ",
            AuctionHouseId = employee.AuctionHouseId
        };

        var result = await UpdateEmployeeEndpoint.HandleAsync(
            employee.EmployeeId,
            request,
            handler,
            CancellationToken.None);

        var response = Assert.IsType<Ok<UpdateEmployeeResponse>>(result.Result).Value!;
        Assert.Equal(employee.EmployeeId, response.EmployeeId);

        var updated = await repository.GetByIdAsync(employee.EmployeeId, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.FirstName);
        Assert.Equal("Employee", updated.LastName);
        Assert.Equal(request.BirthDate, updated.BirthDate);
        Assert.Equal("New Address", updated.Address);
        Assert.Single(publisher.OfType<EmployeeUpdatedIntegrationEvent>());
    }

    [Fact]
    public async Task Update_WithUnknownEmployee_ReturnsNotFound()
    {
        var repository = new TestEmployeeRepository();
        var handler = new UpdateEmployeeHandler(repository, new RecordingEventPublisher());

        var result = await UpdateEmployeeEndpoint.HandleAsync(
            Guid.NewGuid(),
            CreateUpdateRequest(),
            handler,
            CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    [Fact]
    public async Task Delete_WithExistingEmployee_ReturnsNoContentAndPublishesEvent()
    {
        var employee = CreateEmployee();
        var repository = new TestEmployeeRepository(employee);
        var publisher = new RecordingEventPublisher();
        var handler = new DeleteEmployeeHandler(repository, publisher);

        var result = await DeleteEmployeeEndpoint.HandleAsync(
            employee.EmployeeId,
            handler,
            CancellationToken.None);

        Assert.IsType<NoContent>(result.Result);
        Assert.Null(await repository.GetByIdAsync(employee.EmployeeId, CancellationToken.None));
        Assert.Equal(employee.EmployeeId, Assert.Single(publisher.OfType<EmployeeDeletedIntegrationEvent>()).EmployeeId);
    }

    [Fact]
    public async Task Delete_WithUnknownEmployee_ReturnsNotFound()
    {
        var result = await DeleteEmployeeEndpoint.HandleAsync(
            Guid.NewGuid(),
            new DeleteEmployeeHandler(new TestEmployeeRepository(), new RecordingEventPublisher()),
            CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    private static CreateEmployeeRequest CreateRequest(
        string firstName = "Anna",
        string lastName = "Jensen",
        string address = "Main Street 1") => new()
    {
        FirstName = firstName,
        LastName = lastName,
        BirthDate = new DateOnly(1990, 5, 20),
        Address = address,
        AuctionHouseId = Guid.Parse("2f1b7c4e-8a3d-4c5f-9e10-6d4a8b2c1f30")
    };

    private static UpdateEmployeeRequest CreateUpdateRequest() => new()
    {
        FirstName = "Updated",
        LastName = "Employee",
        BirthDate = new DateOnly(1988, 6, 15),
        Address = "New Address",
        AuctionHouseId = Guid.NewGuid()
    };

    private static Employee CreateEmployee() => new()
    {
        EmployeeId = Guid.NewGuid(),
        AuctionHouseId = Guid.NewGuid(),
        FirstName = "Original",
        LastName = "Employee",
        BirthDate = new DateOnly(1980, 1, 1),
        Address = "Original Address"
    };

    private sealed class TestEmployeeRepository(params Employee[] initialEmployees) : IEmployeeRepository
    {
        private readonly Dictionary<Guid, Employee> employees =
            initialEmployees.ToDictionary(employee => employee.EmployeeId);

        public Task AddAsync(Employee employee, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            employees.Add(employee.EmployeeId, employee);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Guid employeeId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(employees.Remove(employeeId));
        }

        public Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<Employee>>(employees.Values.ToArray());
        }

        public Task<Employee?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            employees.TryGetValue(employeeId, out var employee);
            return Task.FromResult(employee);
        }

        public Task UpdateAsync(Employee employee, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            employees[employee.EmployeeId] = employee;
            return Task.CompletedTask;
        }
    }
}
