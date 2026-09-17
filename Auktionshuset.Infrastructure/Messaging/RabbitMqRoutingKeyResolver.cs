using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Application.Admin.Lots;
using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Application.Admin.Lots.DeleteLot;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal sealed class RabbitMqRoutingKeyResolver
    {
        /// <summary>
        /// Returns the routing key configured for the given integration event type.
        /// </summary>
        /// <typeparam name="TEvent">The integration event type to resolve a routing key for.</typeparam>
        /// <returns>
        /// The routing key declared in <see cref="RabbitMqTopology.RoutingKeys"/> for
        /// <typeparamref name="TEvent"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no routing key is mapped to <typeparamref name="TEvent"/>.
        /// </exception>
        public string Resolve<TEvent>()
            where TEvent : IIntegrationEvent
        {
            return typeof(TEvent) switch
            {
                var type when type == typeof(LotCreatedIntegrationEvent)
                    => RabbitMqTopology.RoutingKeys.LotCreated,

                var type when type == typeof(AuctionCreatedIntegrationEvent)
                    => RabbitMqTopology.RoutingKeys.AuctionCreated,

                var type when type == typeof(LotUpdatedIntegrationEvent)
                    => RabbitMqTopology.RoutingKeys.LotUpdated,

                var type when type == typeof(LotDeletedIntegrationEvent)
                    => RabbitMqTopology.RoutingKeys.LotDeleted,

                _ => throw new InvalidOperationException(
                    $"No routing key defined for event type {typeof(TEvent).Name}.")
            };
        }
    }
}
