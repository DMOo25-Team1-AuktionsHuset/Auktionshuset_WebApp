using Microsoft.AspNetCore.Http.HttpResults;
using Auktionshuset.Contracts.Dto.Admin.Employee.CreateEmployee;
using Auktionshuset.Application.Admin.Employees.CreateEmployee;
using Microsoft.AspNetCore.Mvc;

namespace Auktionshuset.Api.Endpoints.Admin.Employee.CreateEmployee {
    public static class CreateEmployeeEndpoint {
        public static RouteGroupBuilder MapCreateEmployee(this RouteGroupBuilder group) {
            group.MapPost("/", HandleAsync)
                .WithName("CreateEmployee")
                .WithSummary("Creates a new employee")
                .Produces<CreateEmployeeResponse>(StatusCodes.Status201Created)
                .ProducesValidationProblem()
                .AllowAnonymous();
            return group;
        }

        public static async Task<Created<CreateEmployeeResponse>> HandleAsync(
            [FromBody]CreateEmployeeRequest request, 
            [FromServices]CreateEmployeeHandler handler, 
            CancellationToken cancellationToken) 
        {
            var command = new CreateEmployeeCommand(
                FirstName: request.FirstName.Trim(),
                LastName: request.LastName.Trim(),
                BirthDate: request.BirthDate,
                Address: request.Address.Trim(),
                AuctionHouseId: request.AuctionHouseId);

            var result = await handler.HandleAsync(command, cancellationToken);

            return TypedResults.Created($"/api/employee/{result.EmployeeId}", new CreateEmployeeResponse(result.EmployeeId));
        }
    }
}
