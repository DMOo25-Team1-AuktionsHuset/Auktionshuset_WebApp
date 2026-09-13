using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal static class RabbitMqTopology
    {
        public const string EventExchange = "auktionshuset.events";

        internal static class Queues
        {
            public const string LotCreated =
                "auktionshuset.lot-created";
        }

        public static class RoutingKeys
        {
            public const string LotCreated = "lot.created.v1";
        }
    }
}
