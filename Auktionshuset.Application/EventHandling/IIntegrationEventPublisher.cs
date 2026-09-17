using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.EventHandling {
    public interface IIntegrationEventPublisher {
        /// <summary>
        /// Publishes the specified integration event to every handler that is registered for its type.
        /// </summary>
        /// <typeparam name="TEvent">The type of integration event to publish.</typeparam>
        /// <param name="message">The integration event to publish.</param>
        /// <seealso cref="IIntegrationEventHandler{TEvent}"/>
        Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken) where TEvent : IIntegrationEvent;
    }
}
