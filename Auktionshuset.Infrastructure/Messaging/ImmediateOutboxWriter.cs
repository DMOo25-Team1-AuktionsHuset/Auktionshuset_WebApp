using Auktionshuset.Application.Abstraction;
using Auktionshuset.Application.EventHandling;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Messaging
{
    public class ImmediateOutboxWriter(IIntegrationEventPublisher publisher) : IOutboxWriter
    {
        public Task AddAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        {
            return publisher.PublishAsync(
                integrationEvent,
                cancellationToken);
        }
    }
}
