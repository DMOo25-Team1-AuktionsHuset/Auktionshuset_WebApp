using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.Messaging;
using Auktionshuset.Infrastructure.Messaging.Consumers.Lot;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Service
{
    internal static class RabbitMqServiceExtension
    {
        /// <summary>
        /// Registers the RabbitMQ connection, the event publisher and the lot consumers.
        /// </summary>
        /// <param name="services">The service collection to add the messaging services to.</param>
        /// <returns>The same service collection so that further calls can be chained.</returns>
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

            services.AddHostedService<LotCreatedConsumer>();
            services.AddHostedService<LotUpdatedConsumer>();
            services.AddHostedService<LotDeletedConsumer>();

            return services;
        }
    }
}
