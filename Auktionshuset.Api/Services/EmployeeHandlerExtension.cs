using Auktionshuset.Api.Events.Admin.Employee;
using Auktionshuset.Application.Admin.Employees;
using Auktionshuset.Application.Admin.Employees.CreateEmployee;
using Auktionshuset.Application.Admin.Employees.DeleteEmployee;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Api.Services
{
    internal static class EmployeeHandlerExtension
    {
        internal static IServiceCollection AddEmployeeHandler(
            this IServiceCollection services)
        {
            services.AddScoped<CreateEmployeeHandler>();
            services.AddScoped<DeleteEmployeeHandler>();

            services.AddScoped<
                IIntegrationEventHandler<EmployeeCreatedIntegrationEvent>,
                CreateEmployeeRealTimeHandler>();

            services.AddScoped<
                IIntegrationEventHandler<EmployeeDeletedIntegrationEvent>,
                DeleteEmployeeRealTimeHandler>();

            return services;
        }
    }
}
