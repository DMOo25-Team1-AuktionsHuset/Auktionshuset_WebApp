using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Data
{
    public class MessageReplayAudit
    {
        public Guid ReplayId { get; init; }

        public required string Pipeline { get; init; }
        public Guid MessageId { get; init; }
        public Guid? EventId { get; init; }

        public required string ActorId { get; init; }
        public required string Reason { get; init; }

        public DateTime RequestedAtTime { get; init; }

        public int PreviousAttempts { get; init; }
        public string? PreviousError { get; init; }
    }
}
