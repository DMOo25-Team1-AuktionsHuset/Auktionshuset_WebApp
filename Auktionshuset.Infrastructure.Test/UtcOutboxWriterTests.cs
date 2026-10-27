using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Infrastructure.Data;
using Auktionshuset.Infrastructure.Database;
using Auktionshuset.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Test;

public sealed class UtcOutboxWriterTests
{
    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public async Task NonUtcEvent_IsRejectedBeforeStaging(DateTimeKind kind)
    {
        using var context = CreateContext();
        var writer = new EfOutboxWriter(context, new EventContractRegistry());
        var message = CreateEvent(new DateTime(2030, 1, 15, 12, 0, 0, kind));

        await Assert.ThrowsAsync<ArgumentException>(() => writer.AddAsync(message));

        Assert.Empty(context.ChangeTracker.Entries<OutboxMessage>());
    }

    [Fact]
    public async Task UtcEvent_IsStagedWithoutChangingItsTimestamp()
    {
        using var context = CreateContext();
        var writer = new EfOutboxWriter(context, new EventContractRegistry());
        var occurredAt = new DateTime(2030, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var message = CreateEvent(occurredAt);

        await writer.AddAsync(message);

        var entry = Assert.Single(context.ChangeTracker.Entries<OutboxMessage>());
        Assert.Equal(EntityState.Added, entry.State);
        Assert.Equal(message.EventId, entry.Entity.OutboxId);
        Assert.Equal(occurredAt, entry.Entity.OccurredAtTime);
        Assert.Equal(DateTimeKind.Utc, entry.Entity.OccurredAtTime.Kind);
        Assert.Contains("2030-01-15T12:00:00Z", entry.Entity.Payload);
    }

    private static AHDBContext CreateContext() => new(
        new DbContextOptionsBuilder<AHDBContext>()
            .UseNpgsql("Host=localhost;Database=outbox-model-test;Username=postgres;Password=postgres")
            .Options);

    private static LotCreatedIntegrationEvent CreateEvent(DateTime occurredAt) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Vase", "Keramik", 1, 500m, occurredAt);
}
