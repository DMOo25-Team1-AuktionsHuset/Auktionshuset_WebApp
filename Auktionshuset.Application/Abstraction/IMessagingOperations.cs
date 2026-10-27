using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Abstraction
{
    public interface IMessagingOperations
    {
        Task<MessagingStatus> ReadStatusAsync(CancellationToken cancellationToken);

        Task<IReadOnlyList<MessageInfo>> GetQuarantineAsync(
            string pipeline,
            int skip,
            int take,
            CancellationToken cancellationToken);

        Task<MessageDetails?> GetMessageAsync(
            string pipeline,
            Guid messageId,
            CancellationToken cancellationToken);

        Task<ReplayResult> ReplayAsync(
            string pipeline,
            Guid messageId,
            DateTime expectedQuarantinedAtTime,
            string actorId,
            string reason,
            CancellationToken cancellationToken);
    }

    public interface IMessagingDiagnostics
    {
        MessagingDiagnostics Read();
    }

    public sealed class QueueStatus
    {
        public string Pipeline { get; init; } = "";

        public long Pending { get; init; }
        public long Ready { get; init; }
        public long Leased { get; init; }
        public long Quarantined { get; init; }
        public long Processed { get; init; }

        public DateTime? OldestReadyAtTime { get; init; }
    }

    public sealed record MessagingStatus(
        DateTime SampledAtTime,
        IReadOnlyList<QueueStatus> Queues);

    public sealed record MessagingDiagnostics(
        string InstanceId,
        bool WorkersEnabled,
        DateTime StartedAtTime,
        MessagingStatus? Status,
        string? SampleError,
        DateTime? OutboxDbActivity,
        DateTime? InboxDbActivity,
        DateTime? MaintenanceDbActivity,
        bool ConsumerActive,
        DateTime? LastReceivedAtTime,
        DateTime? OutboxLastCompletedAtTime,
        DateTime? InboxLastCompletedAtTime);

    public sealed class MessageInfo
    {
        public string Pipeline { get; init; } = "";
        public Guid MessageId { get; init; }
        public Guid? EventId { get; init; }

        public string EventType { get; init; } = "";
        public string? ConsumerName { get; init; }
        public string? MessageKey { get; init; }

        public int Attempts { get; init; }
        public string? Error { get; init; }

        public DateTime NextAttemptAtTime { get; init; }
        public DateTime? QuarantinedAtTime { get; init; }
        public DateTime? ProcessedAtTime { get; init; }
        public DateTime? LeaseExpiresAtTime { get; init; }
    }

    public sealed record ReplayAuditInfo(
        Guid ReplayId,
        string ActorId,
        string Reason,
        DateTime RequestedAtTime,
        int PreviousAttempts,
        string? PreviousError);

    public sealed record MessageDetails(
        MessageInfo Message,
        IReadOnlyList<ReplayAuditInfo> Replays);

    public sealed record ReplayResult(int StatusCode, string Detail);
}
