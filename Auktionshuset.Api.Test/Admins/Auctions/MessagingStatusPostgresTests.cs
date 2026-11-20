using System.Data;
using System.Data.Common;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Messaging;
using Auktionshuset.Infrastructure.Messaging.InboxServices;
using Auktionshuset.Infrastructure.Messaging.OutboxServices;
using Auktionshuset.Infrastructure.Messaging.MessagingServices;
using Auktionshuset.Infrastructure.Messaging.RabbitMqServices;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Auktionshuset.Api.Test.Admins.Auctions;

public sealed partial class AuctionStockPostgresTests
{
    [PostgresStockFact]
    public async Task MessagingStatusReadsBothPipelinesAndFiltersAdminConsumerAsText()
    {
        var parameters = new StatusParameterObserver();
        await using var db = new AHDBContext(new DbContextOptionsBuilder<AHDBContext>()
            .UseNpgsql(connectionString).AddInterceptors(parameters).Options);
        using var runtime = new MessagingRuntime(Clock, Options.Create(new MessagingOperationsOptions()));
        var operations = new MessagingOperations(db, new EventContractRegistry(), runtime, Clock);
        var empty = await operations.ReadStatusAsync(default);
        Assert.Equal(2, empty.Queues.Count);
        Assert.All(empty.Queues, q => Assert.Equal(0, q.Pending + q.Processed + q.Quarantined));

        // Use database-relative times: readiness is classified by CURRENT_TIMESTAMP.
        var past = DateTime.UtcNow.AddDays(-1);
        var future = DateTime.UtcNow.AddDays(1);
        for (int state = 0; state < 6; state++)
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                OutboxId = Guid.NewGuid(), EventType = "test", Payload = "{}", OccurredAtTime = past,
                NextAttemptAtTime = state == 1 ? future : past,
                LeaseExpiresAtTime = state == 2 ? future : null,
                QuarantinedAtTime = state == 3 ? past : null,
                ProcessedAtTime = state == 4 ? past : null,
                Attempts = state == 5 ? OutboxPolicy.MaxAttempts : 0
            });
            db.InboxMessages.Add(new InboxMessage
            {
                InboxId = Guid.NewGuid(), ConsumerName = RabbitMqTopology.Queues.Admin,
                MessageKey = Guid.NewGuid().ToString(), EventType = "test", Payload = [], ReceivedAtTime = past,
                NextAttemptAtTime = state == 1 ? future : past,
                LeaseExpiresAtTime = state == 2 ? future : null,
                InboxQuarantinedAtTime = state == 3 ? past : null,
                ProcessedAtTime = state == 4 ? past : null,
                Attempts = state == 5 ? InboxPolicy.MaxAttempts : 0
            });
        }
        db.InboxMessages.Add(new InboxMessage
        {
            InboxId = Guid.NewGuid(), ConsumerName = "another.consumer", MessageKey = "excluded",
            EventType = "test", Payload = [], ReceivedAtTime = past, NextAttemptAtTime = past
        });
        await db.SaveChangesAsync();

        var status = await operations.ReadStatusAsync(default);
        Assert.Equal(2, parameters.StatusReads);
        Assert.Equal(Now, status.SampledAtTime);
        Assert.Equal(ExpectedPipelines, status.Queues.Select(q => q.Pipeline));
        Assert.All(status.Queues, q =>
        {
            Assert.Equal(4, q.Pending);
            Assert.Equal(1, q.Ready);
            Assert.Equal(1, q.Leased);
            Assert.Equal(1, q.Quarantined);
            Assert.Equal(1, q.Processed);
            Assert.Equal(past.Ticks / 10, q.OldestReadyAtTime!.Value.Ticks / 10);
        });
    }

    private static readonly string[] ExpectedPipelines = new[] { "outbox", "inbox" };

    private sealed class StatusParameterObserver : DbCommandInterceptor
    {
        public int StatusReads { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("WITH messages AS", StringComparison.Ordinal))
            {
                var queue = Assert.Single(command.Parameters.Cast<DbParameter>(),
                    p => Equals(p.Value, RabbitMqTopology.Queues.Admin));
                Assert.Equal(DbType.String, queue.DbType);
                Assert.DoesNotContain(RabbitMqTopology.Queues.Admin, command.CommandText);
                Assert.Equal(3, command.Parameters.Count); // Both attempt limits and the consumer name.
                StatusReads++;
            }
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
