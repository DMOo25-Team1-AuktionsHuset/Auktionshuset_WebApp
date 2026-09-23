using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Repositories;
using Auktionshuset.Infrastructure.Service.Lots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Auktionshuset.Infrastructure.Service
{
    public static class InfrastructureServiceExtensions
    {
        /// <summary>
        /// Registers the infrastructure services, including messaging, the in-memory stores and the
        /// lot image store.
        /// </summary>
        /// <param name="services">The service collection to add the infrastructure services to.</param>
        /// <returns>The same service collection so that further calls can be chained.</returns>
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {

            var connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' blev ikke fundet.");

            services.AddDbContext<AHDBContext>(options =>
                options.UseNpgsql(connectionString));

            // Add infrastructure services here
            services.AddRabbitMq(configuration);
            services.AddScoped<ILotRepository, EFLotRepo>();

            services.AddSingleton<IEmployeeRepository, InMemoryEmployeeRepository>();

            services.AddSingleton<LotImageStoreOptions>();
            services.AddSingleton<ILotImageStore, FileSystemLotImageStore>();

            return services;
        }
    }
}
