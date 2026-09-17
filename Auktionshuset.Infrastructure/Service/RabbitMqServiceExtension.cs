using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.Messaging;
using Auktionshuset.Infrastructure.Messaging.Consumers.Lot;
using Microsoft.Extensions.Configuration;
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
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddSingleton<RabbitMqRoutingKeyResolver>();

            services.AddSingleton<IConnection>(_ =>
            {
                var hostName = GetRequiredConfigurationValue(
                    configuration,
                    "RabbitMQ:HostName");

                var userName = GetRequiredConfigurationValue(
                    configuration,
                    "RabbitMQ:UserName");

                var password = GetRequiredConfigurationValue(
                    configuration,
                    "RabbitMQ:Password");

                var port = GetRequiredRabbitMqPort(configuration);

                var factory = new ConnectionFactory
                {
                    HostName = hostName,
                    Port = port,
                    UserName = userName,
                    Password = password
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

        private static string GetRequiredConfigurationValue(
            IConfiguration configuration,
            string key)
        {
            var value = configuration[key];

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Missing required configuration value '{key}'.");
            }

            return value;
        }

        private static int GetRequiredRabbitMqPort(
            IConfiguration configuration)
        {
            var value = GetRequiredConfigurationValue(
                configuration,
                "RabbitMQ:Port");

            if (!int.TryParse(value, out var port) || port <= 0)
            {
                throw new InvalidOperationException(
                    "Configuration value 'RabbitMQ:Port' must be a positive integer.");
            }

            return port;
        }
    }
}
