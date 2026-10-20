using Auktionshuset.Api.Security;
using Auktionshuset.Application.Admin.Employees;
using Auktionshuset.Contracts.Dto.Admin.Employee;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Auktionshuset.Api.Endpoints.Admin.Employee.GetEmployees
{
    public static class GetEmployeesEndpoint
    {
        /// <summary>
        /// Maps every employee endpoint under the <c>/api/employees</c> route group.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder that the route group is added to.</param>
        /// <returns>The same endpoint route builder so that further routes can be mapped.</returns>
        public static RouteGroupBuilder MapGetEmployees(this RouteGroupBuilder group)
        {
            group.MapGet("/", HandleAsync)
                .WithName("GetEmployees")
                .WithSummary("Gets every employee")
                .Produces<IReadOnlyList<EmployeeListItemResponse>>()
                .RequireAuthorization(SecurityPolicies.CanViewEmployees);

            return group;
        }

        /// <summary>
        /// Returns every employee as a selectable option with an identifier and a display name.
        /// </summary>
        /// <param name="handler">The handler that supplies every employee.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>A 200 response containing every employee.</returns>
        public static RouteGroupBuilder MapEmployeeOptions(this RouteGroupBuilder group)
        {
            group.MapGet("/options", HandleOptionsAsync)
                .WithName("GetEmployeeOptions")
                .WithSummary("Gets the limited employee options used to assign an auctioneer")
                .Produces<IReadOnlyList<EmployeeOptionResponse>>()
                .RequireAuthorization(SecurityPolicies.CanViewEmployeeOptions);

            return group;
        }

        public static async Task<Ok<IReadOnlyList<EmployeeOptionResponse>>> HandleOptionsAsync(
            GetEmployeesHandler handler,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Domain.Entities.Employee> employees = await handler.HandleAsync(cancellationToken);

            EmployeeOptionResponse[] response = employees
                .Select(employee => new EmployeeOptionResponse(
                    employee.EmployeeId,
                    $"{employee.FirstName} {employee.LastName}".Trim()))
                .ToArray();

            return TypedResults.Ok<IReadOnlyList<EmployeeOptionResponse>>(response);
        }

        public static async Task<Ok<IReadOnlyList<EmployeeListItemResponse>>> HandleAsync(
            GetEmployeesHandler handler,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Domain.Entities.Employee> employees = await handler.HandleAsync(cancellationToken);

            EmployeeListItemResponse[] response = employees
                .Select(employee => new EmployeeListItemResponse(
                    EmployeeId: employee.EmployeeId,
                    FirstName: employee.FirstName,
                    LastName: employee.LastName,
                    BirthDate: employee.BirthDate,
                    Address: employee.Address,
                    AuctionHouseId: employee.AuctionHouseId))
                .ToArray();

            return TypedResults.Ok<IReadOnlyList<EmployeeListItemResponse>>(response);
        }
    }

}
