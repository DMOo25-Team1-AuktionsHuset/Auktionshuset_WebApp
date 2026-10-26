using Auktionshuset.Application.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal class MessagingStatusSampler(
        IServiceScopeFactory scopeFactory,
        MessagingRuntime runtime,
        IOptions<MessagingOperationsOptions> options,
        ILogger<MessagingStatusSampler> logger)
        : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();

                    var operations = scope.ServiceProvider
                        .GetRequiredService<IMessagingOperations>();

                    MessagingStatus status =
                        await operations.ReadStatusAsync(stoppingToken);

                    runtime.Sampled(status);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    runtime.SampleFailed(exception);

                    logger.LogWarning(
                        exception,
                        "Messaging-status kunne ikke opdateres.");
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(
                        options.Value.StatusSampleIntervalSeconds),
                    stoppingToken);
            }
        }
    }
}
