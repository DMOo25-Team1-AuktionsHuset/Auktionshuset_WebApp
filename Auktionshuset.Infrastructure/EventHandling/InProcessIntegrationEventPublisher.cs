using Auktionshuset.Application.EventHandling;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.EventHandling {
    public class InProcessIntegrationEventPublisher(IServiceProvider serviceProvider) : IIntegrationEventPublisher {
        /// <summary>
        /// Resolves every registered <see cref="IIntegrationEventHandler{TEvent}"/> for the event
        /// and invokes them in process, without involving a message broker.
        /// </summary>
        /// <typeparam name="TEvent">The type of integration event being published.</typeparam>
        /// <param name="message">The integration event to dispatch to each resolved handler.</param>
        /// <returns>A task that completes once every handler has finished processing the event.</returns>
        /// <seealso cref="IIntegrationEventPublisher"/>
        public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken) where TEvent : IIntegrationEvent {
            var serviceType = typeof(IEnumerable<IIntegrationEventHandler<TEvent>>);

            var handlers = serviceProvider.GetService(serviceType) as IEnumerable<IIntegrationEventHandler<TEvent>> ?? [];

            return Task.WhenAll(handlers.Select(handler => handler.HandleAsync(message, cancellationToken)));
        }
    }
}
