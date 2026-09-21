using Xunit;
using Auktionshuset.Application.Admin.Auctions;

namespace Auktionshuset.Tests;

public class AuctionStatusesTests
{
    /// <summary>
    /// Verifies that an auction that has not started yet is upcoming.
    /// </summary>
    [Fact]
    public void Derive_WithFutureStart_ReturnsUpcoming()
    {
        var now = new DateTime(2026, 5, 1, 12, 0, 0);

        var status = AuctionStatuses.Derive(now.AddHours(1), now.AddHours(5), now);

        Assert.Equal(AuctionStatuses.Upcoming, status);
    }

    /// <summary>
    /// Verifies that an auction inside its time span is live.
    /// </summary>
    [Fact]
    public void Derive_BetweenStartAndEnd_ReturnsLive()
    {
        var now = new DateTime(2026, 5, 1, 12, 0, 0);

        var status = AuctionStatuses.Derive(now.AddHours(-1), now.AddHours(1), now);

        Assert.Equal(AuctionStatuses.Live, status);
    }

    /// <summary>
    /// Verifies that an auction past its end time is ended.
    /// </summary>
    [Fact]
    public void Derive_AfterEnd_ReturnsEnded()
    {
        var now = new DateTime(2026, 5, 1, 12, 0, 0);

        var status = AuctionStatuses.Derive(now.AddHours(-5), now.AddHours(-1), now);

        Assert.Equal(AuctionStatuses.Ended, status);
    }

    /// <summary>
    /// Verifies that the status values are the Danish labels the dashboard uses as filters.
    /// </summary>
    [Fact]
    public void All_ContainsEveryStatusLabel()
    {
        Assert.Equal(
            [AuctionStatuses.Upcoming, AuctionStatuses.Live, AuctionStatuses.Ended],
            AuctionStatuses.All);
    }
}
