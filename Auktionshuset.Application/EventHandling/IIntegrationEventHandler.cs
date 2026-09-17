using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.EventHandling {
    public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent {
        /// <summary>
        /// Handles the specified integration event.
        /// </summary>
        /// <param name="message">The integration event to handle.</param>
        Task HandleAsync(TEvent message, CancellationToken cancellationToken);
    }
}
