using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.EventHandling {
    public interface IIntegrationEvent {
        Guid EventId { get; }
        DateTime OccurredAt { get; }
    }
}
