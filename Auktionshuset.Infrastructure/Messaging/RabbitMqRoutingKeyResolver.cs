using Auktionshuset.Application.Abstraction.Admin.Lots;
using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal sealed class RabbitMqRoutingKeyResolver
    {
        public string Resolve<TEvent>()
            where TEvent : IIntegrationEvent
        {
            return typeof(TEvent) switch
            {
                var type when type == typeof(LotCreatedIntegrationEvent)
                    => RabbitMqTopology.RoutingKeys.LotCreated,

                _ => throw new InvalidOperationException(
                    $"No routing key defined for event type {typeof(TEvent).Name}.")
            };
        }
    }
}
