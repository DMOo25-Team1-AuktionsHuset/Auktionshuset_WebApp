using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Data
{
    public sealed class OutboxMessage
    {
        public Guid OutboxId { get; init; }
        public required string EventType { get; init; }
        public required string Payload { get; init; }
        public DateTime OccuredAtTime { get; init; }

        public DateTime? ProcessedAtTime { get; set; }
        public int Attempts { get; set; }
        public string? Error { get; set; }
    }
}
