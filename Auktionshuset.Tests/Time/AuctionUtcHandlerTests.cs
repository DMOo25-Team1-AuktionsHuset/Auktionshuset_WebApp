using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Infrastructure.Service;
using Xunit;

namespace Auktionshuset.Tests.Time;

public sealed class AuctionUtcHandlerTests
{
    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public async Task NonUtcCommands_AreRejectedWithoutSaving(DateTimeKind kind)
    {
        var now = new DateTime(2030, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var repository = new InMemoryAuctionRepository();
        var outbox = new RecordingEventPublisher();
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new CreateAuctionHandler(repository, new InMemoryLotRepository(),
            new TestEmployeeRepository(), outbox, unitOfWork, new FixedClock(now));
        var start = DateTime.SpecifyKind(now.AddHours(1), kind);

        var result = await handler.HandleAsync(TestData.CreateCommand(null, start, start.AddHours(1)), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(await repository.GetAllAsync(CancellationToken.None));
        Assert.Empty(outbox.Published);
        Assert.Equal(0, unitOfWork.CommitCount);
    }

    [Fact]
    public async Task FutureUtcCommand_PreservesUtcAndUsesTheInjectedClockForItsEvent()
    {
        var now = new DateTime(2030, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var repository = new InMemoryAuctionRepository();
        var outbox = new RecordingEventPublisher();
        var handler = new CreateAuctionHandler(repository, new InMemoryLotRepository(),
            new TestEmployeeRepository(), outbox, new RecordingUnitOfWork(), new FixedClock(now));

        var result = await handler.HandleAsync(TestData.CreateCommand(null, now.AddHours(1), now.AddHours(2)), CancellationToken.None);

        Assert.True(result.Succeeded);
        var stored = (await repository.GetByIdAsync(result.AuctionId, CancellationToken.None))!;
        var message = Assert.IsType<AuctionCreatedIntegrationEvent>(Assert.Single(outbox.Published));
        Assert.Equal(DateTimeKind.Utc, stored.StartsAt.Kind);
        Assert.Equal(now, message.OccurredAt);
        Assert.Equal(DateTimeKind.Utc, message.OccurredAt.Kind);
    }

    [Fact]
    public async Task PastUtcStart_IsRejectedUsingTheInjectedClock()
    {
        var now = new DateTime(2030, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var repository = new InMemoryAuctionRepository();
        var outbox = new RecordingEventPublisher();
        var handler = new CreateAuctionHandler(repository, new InMemoryLotRepository(),
            new TestEmployeeRepository(), outbox, new RecordingUnitOfWork(), new FixedClock(now));

        var result = await handler.HandleAsync(TestData.CreateCommand(null, now.AddMinutes(-1), now.AddHours(1)), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("fremtiden"));
        Assert.Empty(outbox.Published);
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
