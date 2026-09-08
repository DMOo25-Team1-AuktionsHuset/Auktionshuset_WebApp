using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Application.EventHandling;
using RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal static class RabbitMqServiceExtension
    {
        internal static IServiceCollection AddRabbitMq(
            this IServiceCollection services)
        {
            services.AddSingleton<RabbitMqRoutingKeyResolver>();

            services.AddSingleton<IConnection>(_ =>
            {
            // OBS: Kun til lokal test
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest"

            };

            return factory
                .CreateConnectionAsync()
                .GetAwaiter()
                .GetResult();
        });

            services.AddSingleton<
                IIntegrationEventPublisher,
                RabbitMqIntegrationEventPublisher>();

            return services;
        }
    }
}
