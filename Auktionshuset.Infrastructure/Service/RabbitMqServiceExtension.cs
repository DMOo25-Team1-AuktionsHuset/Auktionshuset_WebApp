using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Messaging;
using Auktionshuset.Infrastructure.Messaging.Consumers;
using Auktionshuset.Infrastructure.Service.Vault;
using Auktionshuset.Infrastructure.Messaging.InboxServices;
using Auktionshuset.Infrastructure.Messaging.OutboxServices;
using Auktionshuset.Infrastructure.Messaging.RabbitMqServices;
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
            IConfiguration configuration,
            RabbitMqCredentials credentials)
        {
            services.AddSingleton<EventContractRegistry>();
            services.AddSingleton<RabbitMqTopologyInitializer>();

            services.AddSingleton(_ => new ConnectionFactory
            {
                HostName = GetRequiredConfigurationValue(
                    configuration, "RabbitMQ:HostName"),
                UserName = GetRequiredConfigurationValue(
                    configuration, "RabbitMQ:UserName"),
                Password = GetRequiredConfigurationValue(
                    configuration, "RabbitMQ:Password"),
                Port = GetRequiredRabbitMqPort(configuration),
                VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/",
                ClientProvidedName = "auktionshuset-api",

                // Provider and worker loop owns reconnect
                AutomaticRecoveryEnabled = false,
                TopologyRecoveryEnabled = false,

                RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
                HandshakeContinuationTimeout = TimeSpan.FromSeconds(5),
                ContinuationTimeout = TimeSpan.FromSeconds(5),
                int port = GetRequiredRabbitMqPort(configuration);

                var factory = new ConnectionFactory
                {
                    HostName = hostName,
                    Port = port,
                    UserName = credentials.Username,
                    Password = credentials.Password,
                    VirtualHost = "/"
                };
                RequestedHeartbeat = TimeSpan.FromSeconds(15),

                ConsumerDispatchConcurrency = 1
            });

            services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
            services.AddSingleton<IIntegrationEventPublisher, RabbitMqIntegrationEventPublisher>();
            services.AddScoped<IOutboxWriter, EfOutboxWriter>();
            services.AddScoped<DbOutboxStore>();
            services.AddScoped<DbInboxStore>();
            services.AddScoped<IInboxStore>(services => services.GetRequiredService<DbInboxStore>());

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

            if (!int.TryParse(value, out int port) || port is < 1 or > 65535)
            {
                throw new InvalidOperationException(
                    "Configuration value 'RabbitMQ:Port' must be between 1 and 65535.");
            }

            return port;
        }
    }
}
