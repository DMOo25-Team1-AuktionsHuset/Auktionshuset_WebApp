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
        public DateTime RecievedAtTime { get; init; }
        public DateTime? ProcessedAtTime { get; set; }
        public string? Error { get; set; }
        public DateTime? InboxQuarantinedAtTime { get; set; }

    }
}
