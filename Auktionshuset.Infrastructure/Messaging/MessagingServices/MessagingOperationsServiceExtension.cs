using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Application.Abstraction;
using Auktionshuset.Infrastructure.Messaging.Consumers;
using Auktionshuset.Infrastructure.Messaging.InboxServices;
using Auktionshuset.Infrastructure.Messaging.OutboxServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Auktionshuset.Infrastructure.Messaging.MessagingServices
{
    internal static class MessagingOperationsServiceExtension
    {
        internal static IServiceCollection AddMessagingOperations(
            this IServiceCollection services, IConfiguration configuration)
        {
            IConfigurationSection section = configuration.GetSection("MessagingOperations");

            services.AddOptions<MessagingOperationsOptions>()
                .Bind(section)
                .Validate(x =>
                        x.OutboxRetentionDays is >= 1 and <= 3650
                        && x.CleanupBatchSize is >= 1 and <= 1000
                        && x.MaintenanceIntervalSeconds is >= 10 and <= 3600
                        && x.StatusSampleIntervalSeconds is >= 5 and >= 300
                        && x.WorkerStaleAfterSeconds is >= 30 and <= 7200
                        && x.WorkerStaleAfterSeconds >= x.StatusSampleIntervalSeconds * 3
                        && (!x.RunMaintenance || x.WorkerStaleAfterSeconds >= x.MaintenanceIntervalSeconds * 2)
                        && x.BacklogWarningAfterSeconds is >= 0 and <= 600,
                    "MessagingOperations indeholder ugyldige værdier")
                .ValidateOnStart();

            services.TryAddSingleton<TimeProvider>(TimeProvider.System);

            services.AddSingleton<MessagingRuntime>();

            services.AddSingleton<IMessagingDiagnostics>(provider => provider.GetRequiredService<MessagingRuntime>());

            services.AddScoped<IMessagingOperations, MessagingOperations>();

            services.AddHostedService<MessagingStatusSampler>();

            MessagingOperationsOptions settings = section.Get<MessagingOperationsOptions>() ?? new();

            if (settings.RunMaintenance) services.AddHostedService<MessagingMaintenanceWorker>();

            if (settings.RunWorkers)
            {
                services.AddHostedService<OutboxProcessor>();
                services.AddHostedService<InboxProcessor>();
                services.AddHostedService<AdminEventsConsumer>();
            }

            services.AddHealthChecks()
                    .AddCheck<DatabaseHealthChecks>("database", tags: ["ready"])
                    .AddCheck<MessagingHealthCheck>("messaging", tags: ["messaging"]);

                return services;
        }
    }
}
