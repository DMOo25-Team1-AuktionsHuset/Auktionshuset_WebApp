using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.Messaging;
using Auktionshuset.Infrastructure.Messaging.Consumers.Lot;
using Auktionshuset.Infrastructure.Messaging.Consumers.Auction;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;


namespace Auktionshuset.Infrastructure.Service
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

            services.AddHostedService<LotCreatedConsumer>();
            services.AddHostedService<AuctionCreatedConsumer>();
            services.AddHostedService<LotUpdatedConsumer>();
            services.AddHostedService<LotDeletedConsumer>();

            return services;
        }
    }
}
