namespace Auktionshuset.Infrastructure.Data
{
    public sealed class InboxMessage
    {
        public Guid InboxId { get; init; }
        public Guid? EventId { get; set; }
        public string? TransportMessageId { get; init; }
        public required string ConsumerName { get; init; }
        public required string MessageKey { get; init; }
        public required string EventType { get; init; }
        public required byte[] Payload { get; init; }
        public DateTime ReceivedAtTime { get; init; }
        public DateTime? ProcessedAtTime { get; set; }
        public string? Error { get; set; }
        public DateTime? InboxQuarantinedAtTime { get; set; }
        public int Attempts { get; set; }
        public DateTime NextAttemptAtTime { get; set; }
        public Guid? LeaseToken { get; set; }
        public DateTime? LeaseExpiresAtTime { get; set; }

    }
}
