using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.EventHandling {
    public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent {
        Task HandleAsync(TEvent message, CancellationToken cancellationToken);
    }
}
