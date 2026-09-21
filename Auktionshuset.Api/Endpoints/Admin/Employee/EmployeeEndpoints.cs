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
        public static IEndpointRouteBuilder MapLotEndpoints(this IEndpointRouteBuilder endpoints) {
            var group = endpoints
                .MapGroup("/api/employees")
                .WithTags("Employee");

            //group.MapCreateEmploye();
            //group.MapUpdateEmployes();
            //group.MapGetEmploye();
            //group.MapDeleteEmploye();

            return endpoints;
        }
    }
}
