using Auktionshuset.Api.Endpoints.Admin.Employee.CreateEmployee;
using Auktionshuset.Api.Endpoints.Admin.Employee.GetEmployees;
using Auktionshuset.Api.Endpoints.Admin.Employee.UpdateEmployee;
using Auktionshuset.Api.Endpoints.Admin.Employee.DeleteEmployee;

namespace Auktionshuset.Api.Endpoints.Admin.Employee {
    public static class EmployeeEndpoints {
        /// <summary>
        /// Maps every lot endpoint under the <c>/api/lots</c> route group.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder that the route group is added to.</param>
        /// <returns>The same endpoint route builder so that further routes can be mapped.</returns>
        public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder endpoints) {
            var group = endpoints
                .MapGroup("/api/employee")
                .WithTags("Employee");

            //group.MapCreateEmployee();
            //group.MapUpdateEmployee();
            //group.MapGetEmployee();
            group.MapDeleteEmployee();

            return endpoints;
        }
    }
}
