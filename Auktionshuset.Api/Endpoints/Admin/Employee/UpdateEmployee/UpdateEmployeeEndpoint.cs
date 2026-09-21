using Auktionshuset.Application.Admin.Employees.UpdateEmployee;
using Auktionshuset.Contracts.Dto.Admin.Employee.UpdateEmployee;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Auktionshuset.Api.Endpoints.Admin.Employee.UpdateEmployee {
    public static class UpdateEmployeeEndpoint {
        public static RouteGroupBuilder MapUpdateEmployee(this RouteGroupBuilder group) {
            group.MapPut("/{employeeId:guid}", HandleAsync)
                .WithName("UpdateEmployee")
                .WithSummary("Updates an employee")
                .Produces<UpdateEmployeeResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound)
                .ProducesValidationProblem();

            return group;
        }

        public static async Task<Results<Ok<UpdateEmployeeResponse>, NotFound>> HandleAsync(
            [FromRoute] Guid employeeId,
            [FromBody] UpdateEmployeeRequest request,
            [FromServices] UpdateEmployeeHandler handler,
            CancellationToken cancellationToken) 
        {
            var command = new UpdateEmployeeCommand(
                EmployeeId: employeeId,
                FirstName: request.FirstName.Trim(),
                LastName: request.LastName.Trim(),
                BirthDate: request.BirthDate,
                Address: request.Address.Trim(),
                AuctionHouseId: request.AuctionHouseId);

            var result = await handler.HandleAsync(command, cancellationToken);

            return result == null ? TypedResults.NotFound() : TypedResults.Ok(new UpdateEmployeeResponse(result.EmployeeId));
        }
    }
}
