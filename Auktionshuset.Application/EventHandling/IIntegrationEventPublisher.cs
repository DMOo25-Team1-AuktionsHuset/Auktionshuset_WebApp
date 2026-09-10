using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.EventHandling {
    public interface IIntegrationEventPublisher {
        Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken) where TEvent : IIntegrationEvent;
    }
}
