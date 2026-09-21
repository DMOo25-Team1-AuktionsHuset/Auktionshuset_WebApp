using Auktionshuset.Api.Events.Admin.Employee;
using Auktionshuset.Application.Admin.Employee.CreateEmployee;
using Auktionshuset.Application.Admin.Employee.DeleteEmployee;
using Auktionshuset.Application.Admin.Employee.UpdateEmployee;
using Auktionshuset.Application.Admin.Employees.DeleteEmployees;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Api.Services
{
    internal static class EmployeeHandlerExtension
    {
        internal static IServiceCollection AddEmployeeHandler(
            this IServiceCollection services)
        {
            services.AddScoped<DeleteEmployeeHandler>();

            services.AddScoped<
                IIntegrationEventHandler<EmployeeDeletedIntegrationEvent>,
                DeleteEmployeeRealTimeHandler>();

            return services;
        }
    }
}
