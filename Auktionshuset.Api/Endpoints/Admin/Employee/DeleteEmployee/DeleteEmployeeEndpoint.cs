using Auktionshuset.Application.Admin.Employees.DeleteEmployee;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Auktionshuset.Api.Security;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Api.Endpoints.Admin.Employee.DeleteEmployee
{
    public static class DeleteEmployeeEndpoint
    {
        public static RouteGroupBuilder MapDeleteEmployee(this RouteGroupBuilder group)
        {
            group.MapDelete("/{employeeId:guid}", HandleAsync)
                .WithName("DeleteEmployee")
                .WithSummary("Deletes an employee")
                .Produces(StatusCodes.Status204NoContent)
                .Produces(StatusCodes.Status404NotFound)
                .RequireAuthorization(SecurityPolicies.CanDeleteEmployee);
            return group;
        }
        public static async Task<Results<NoContent, NotFound, Conflict<ProblemDetails>>> HandleAsync(
            [FromRoute] Guid employeeId,
            [FromServices] DeleteEmployeeHandler handler,
            CancellationToken cancellationToken,
            [FromServices] AHDBContext? db = null)
        {
            if (db is not null && await db.LoginUsers.AnyAsync(u => u.EmployeeId == employeeId, cancellationToken))
                return TypedResults.Conflict(new ProblemDetails { Detail = "Medarbejderen har et login og kan ikke slettes. Deaktivér login i stedet." });
            bool deleted;
            try { deleted = await handler.HandleAsync(new DeleteEmployeeCommand(employeeId), cancellationToken); }
            catch (DbUpdateException)
            { return TypedResults.Conflict(new ProblemDetails { Detail = "Medarbejderen er i brug og kunne ikke slettes. Hent oversigten igen." }); }
            return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
        }
    }
}
