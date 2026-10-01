using Auktionshuset.Application.Admin.Employees;
using Auktionshuset.Contracts.Dto.Admin.Employee;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Auktionshuset.Api.Endpoints.Admin.Employee.GetEmployeeById;

public static class GetEmployeeByIdEndpoint
{
    public static RouteGroupBuilder MapGetEmployeeById(this RouteGroupBuilder group)
    {
        group.MapGet("/{employeeId:guid}", HandleAsync)
            .WithName("GetEmployeeById")
            .WithSummary("Gets an employee by identifier")
            .Produces<EmployeeResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    public static async Task<Results<Ok<EmployeeResponse>, NotFound>> HandleAsync(
        [FromRoute] Guid employeeId,
        [FromServices] GetEmployeeByIdHandler handler,
        CancellationToken cancellationToken)
    {
        Domain.Entities.Employee? employee = await handler.HandleAsync(employeeId, cancellationToken);
        if (employee is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new EmployeeResponse(
            employee.EmployeeId,
            employee.FirstName,
            employee.LastName,
            employee.BirthDate,
            employee.Address,
            employee.AuctionHouseId));
    }
}
