using Auktionshuset.Application.Admin.Employees;
using Auktionshuset.Contracts.Dto.Admin.Employee;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Auktionshuset.Api.Endpoints.Admin.GetEmployees;

public static class GetEmployeesEndpoint
{
    /// <summary>
    /// Maps every employee endpoint under the <c>/api/employees</c> route group.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder that the route group is added to.</param>
    /// <returns>The same endpoint route builder so that further routes can be mapped.</returns>
    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/employees")
            .WithTags("Employees");

        group.MapGet("/", HandleAsync)
            .WithName("GetEmployees")
            .WithSummary("Gets every employee that can act as auctionarius")
            .Produces<IReadOnlyList<EmployeeListItemResponse>>();

        return endpoints;
    }

    /// <summary>
    /// Returns every employee as a selectable option with an identifier and a display name.
    /// </summary>
    /// <param name="handler">The handler that supplies every employee.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A 200 response containing every employee.</returns>
    public static async Task<Ok<IReadOnlyList<EmployeeListItemResponse>>> HandleAsync(
        GetEmployeesHandler handler,
        CancellationToken cancellationToken)
    {
        var employees = await handler.HandleAsync(cancellationToken);

        var response = employees
            .Select(employee => new EmployeeListItemResponse(
                EmployeeId: employee.EmployeeId,
                FullName: $"{employee.FirstName} {employee.LastName}".Trim()))
            .ToArray();

        return TypedResults.Ok<IReadOnlyList<EmployeeListItemResponse>>(response);
    }
}
