using Auktionshuset.Application.EventHandling;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.EventHandling {
    public class InProcessIntegrationEventPublisher(IServiceProvider serviceProvider) : IIntegrationEventPublisher {
        public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken) where TEvent : IIntegrationEvent {
            var serviceType = typeof(IEnumerable<IIntegrationEventHandler<TEvent>>);

            var handlers = serviceProvider.GetService(serviceType) as IEnumerable<IIntegrationEventHandler<TEvent>> ?? [];

            return Task.WhenAll(handlers.Select(handler => handler.HandleAsync(message, cancellationToken)));
        }
    }
}
