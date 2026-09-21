using Auktionshuset.Application.Admin.Employee.DeleteEmployee;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Auktionshuset.Api.Endpoints.Admin.Employee.DeleteEmployee {
    public class DeleteEmployeeEndpoint 
    {
        public static RouteGroupBuilder MapDeleteEmployee(this RouteGroupBuilder group)
        {
            group.MapDelete("/{employeeId:guid}", HandleAsync)
                .WithName("DeleteEmployee")
                .WithSummary("Deletes an employee")
                .Produces(StatusCodes.Status204NoContent)
                .Produces(StatusCodes.Status404NotFound)
                .RequireAuthorization("CanDeleteEmployee");
            return group;
        }
        public static async Task<Results<NoContent, NotFound>> HandleAsync(
            [FromRoute] Guid employeeId,
            [FromServices] DeleteEmployeeHandler handler,
            CancellationToken cancellationToken)
        {
                var deleted = await handler.HandleAsync(new DeleteEmployeeCommand(employeeId), cancellationToken);
                return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
        }
    }
}
