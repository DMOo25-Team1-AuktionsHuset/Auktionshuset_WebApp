namespace Auktionshuset.Domain.Entities
{
    public class Outbox
    {
        public required Guid OutboxId { get; set; }
        public required string EventType { get; set; }
        public required string Content { get; set; }
        public required DateTime OccurredAtTime { get; set; }
        public required DateTime? ProcessedAtTime { get; set; }
    }
}
