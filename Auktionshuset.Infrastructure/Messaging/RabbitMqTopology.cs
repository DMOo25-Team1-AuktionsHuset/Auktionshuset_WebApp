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

    }
}
