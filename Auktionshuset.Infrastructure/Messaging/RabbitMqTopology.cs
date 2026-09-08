using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal static class RabbitMqTopology
    {
        public const string EventExchange = "auktionshuset.events";

        public static class RoutingKeys
        {
            public const string LotCreated = "lot.created.v1";
        }
    }
}
