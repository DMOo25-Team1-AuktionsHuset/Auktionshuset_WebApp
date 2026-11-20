using Auktionshuset.Api.Security;
using Auktionshuset.Application.Admin.Employees;
using Auktionshuset.Contracts.Dto.Admin.Employee;
using Microsoft.AspNetCore.Mvc;

namespace Auktionshuset.Api.Endpoints.Admin.Employee;

public static class EmployeeLoginEndpoints
{
    public static void MapEmployeeLogins(this RouteGroupBuilder group)
    {
        group.MapGet("/{employeeId:guid}/login", async (Guid employeeId, EmployeeLoginHandler handler, CancellationToken ct) =>
            ToResponse(await handler.GetAsync(employeeId, ct))).RequireAuthorization(SecurityPolicies.Admin);
        group.MapPut("/{employeeId:guid}/login", SaveAsync).RequireAuthorization(SecurityPolicies.Admin);
    }

    public static async Task<IResult> SaveAsync(Guid employeeId, SaveEmployeeLoginRequest request,
        [FromServices] EmployeeLoginHandler handler, CancellationToken ct) =>
        ToResponse(await handler.HandleAsync(new(employeeId, request.Email, request.Password, request.Role, request.IsActive), ct));

    private static IResult ToResponse(EmployeeLoginResult result) => result.Status switch
    {
        EmployeeLoginStatus.NotFound => Results.NotFound(),
        EmployeeLoginStatus.Invalid => Results.BadRequest(new { detail = result.Detail }),
        EmployeeLoginStatus.Conflict => Results.Conflict(new { detail = result.Detail }),
        _ => result.Login is null ? Results.Text("null", "application/json")
            : Results.Ok(new EmployeeLoginResponse(result.Login.Email, result.Login.Role, result.Login.IsActive))
    };
}
