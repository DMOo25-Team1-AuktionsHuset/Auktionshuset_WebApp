using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.Messaging;
using Auktionshuset.Infrastructure.Messaging.Consumers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;


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
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddSingleton<RabbitMqRoutingKeyResolver>();

            services.AddSingleton<IConnection>(_ =>
            {
                string hostName = GetRequiredConfigurationValue(
                    configuration,
                    "RabbitMQ:HostName");

                string userName = GetRequiredConfigurationValue(
                    configuration,
                    "RabbitMQ:UserName");

                string password = GetRequiredConfigurationValue(
                    configuration,
                    "RabbitMQ:Password");

                int port = GetRequiredRabbitMqPort(configuration);

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

            services.AddHostedService<AdminEventsConsumer>();

            return services;
        }

        private static string GetRequiredConfigurationValue(
            IConfiguration configuration,
            string key)
        {
            string? value = configuration[key];

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
            string value = GetRequiredConfigurationValue(
                configuration,
                "RabbitMQ:Port");

            if (!int.TryParse(value, out int port) || port <= 0)
            {
                throw new InvalidOperationException(
                    "Configuration value 'RabbitMQ:Port' must be a positive integer.");
            }

            return port;
        }
    }
}
