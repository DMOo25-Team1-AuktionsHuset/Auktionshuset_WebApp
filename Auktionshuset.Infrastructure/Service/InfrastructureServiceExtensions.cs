using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace Auktionshuset.Infrastructure.Service
{
    public static class InfrastructureServiceExtensions
    {
        /// <summary>
        /// Registers the infrastructure services, including RabbitMQ messaging.
        /// </summary>
        /// <param name="services">The service collection to add the infrastructure services to.</param>
        /// <returns>The same service collection so that further calls can be chained.</returns>
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services)
        {
            // Add infrastructure services here
            services.AddRabbitMq();

            return services;
        }
    }
}
