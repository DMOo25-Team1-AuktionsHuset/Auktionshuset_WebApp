using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Auktionshuset.Infrastructure.Messaging;

namespace Auktionshuset.Infrastructure
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
