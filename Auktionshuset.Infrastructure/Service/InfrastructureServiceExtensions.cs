using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace Auktionshuset.Infrastructure.Service
{
    public static class InfrastructureServiceExtensions
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services)
        {
            // Add infrastructure services here
            services.AddRabbitMq();

            return services;
        }
    }
}
