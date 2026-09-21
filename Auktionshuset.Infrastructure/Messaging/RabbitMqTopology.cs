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
            public const string Admin =
                "auktionshuset.admin";
        }

        public static class RoutingKeys
        {
            public const string LotCreated = 
                "lot.created.v1";

            public const string LotUpdated =
                "lot.updated.v1";

            public const string LotDeleted =
                "lot.deleted.v1";

            public const string AuctionCreated = 
                "auction.created.v1";

            public const string EmployeeDeleted =
                "employee.deleted.v1";

            public const string EmployeeCreated =
                "employee.created.v1";
        }
    }
}
