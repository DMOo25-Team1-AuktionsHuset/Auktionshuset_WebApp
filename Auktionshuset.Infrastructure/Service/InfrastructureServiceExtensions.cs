using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Repositories;
using Auktionshuset.Infrastructure.Service.Lots;
using Auktionshuset.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Auktionshuset.Infrastructure.Service
{
    public static class InfrastructureServiceExtensions
    {
        /// <summary>
        /// Registers the infrastructure services, including messaging, repositories, and the
        /// PostgreSQL lot image store.
        /// </summary>
        /// <param name="services">The service collection to add the infrastructure services to.</param>
        /// <returns>The same service collection so that further calls can be chained.</returns>
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {

            string connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' blev ikke fundet.");

            //DBContext
            services.AddDbContext<AHDBContext>(options =>
                options.UseNpgsql(connectionString));

            // Add infrastructure services here
            services.AddRabbitMq(configuration);

            // Add repositories here
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();

            services.AddScoped<ILotRepository, EFLotRepo>();
            services.AddScoped<ILotImageStore, PostgresLotImageStore>();
            services.AddScoped<IEmployeeRepository, EFEmployeeRepository>();
            services.AddScoped<IAuctionRepository, EFAuctionRepository>();

            return services;
        }
    }
}
