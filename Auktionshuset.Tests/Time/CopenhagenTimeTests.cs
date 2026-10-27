using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Auktionshuset.Contracts.Time;
using Auktionshuset.Models;
using Xunit;

namespace Auktionshuset.Tests.Time;

public sealed class CopenhagenTimeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(1, 11)]
    [InlineData(7, 10)]
    public void CopenhagenNoon_RoundTripsThroughUtc(int month, int utcHour)
    {
        var local = new DateTime(2026, month, 15, 12, 0, 0, DateTimeKind.Unspecified);

        Assert.True(CopenhagenTime.TryToUtc(local, out DateTime utc, out _));
        Assert.Equal(new DateTime(2026, month, 15, utcHour, 0, 0, DateTimeKind.Utc), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(local, CopenhagenTime.FromUtc(utc));
        Assert.Equal(DateTimeKind.Unspecified, CopenhagenTime.FromUtc(utc).Kind);
    }

    [Theory]
    [InlineData(3, 29)]
    [InlineData(10, 25)]
    public void DaylightSavingGapAndOverlap_AreRejected(int month, int day)
    {
        var local = new DateTime(2026, month, day, 2, 30, 0, DateTimeKind.Unspecified);

        Assert.False(CopenhagenTime.TryToUtc(local, out _, out string? error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void ConversionAcrossMidnight_PreservesTheInstant()
    {
        var local = new DateTime(2026, 7, 1, 0, 30, 0, DateTimeKind.Unspecified);

        Assert.True(CopenhagenTime.TryToUtc(local, out DateTime utc, out _));
        Assert.Equal(new DateTime(2026, 6, 30, 22, 30, 0, DateTimeKind.Utc), utc);
        Assert.Equal(local, CopenhagenTime.FromUtc(utc));
    }

    [Fact]
    public void Today_UsesCopenhagenInsteadOfTheServersTimeZone()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 1, 1, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 1, 2), CopenhagenTime.Today(clock));
    }

    [Theory]
    [InlineData("2027-01-15T12:00:00+01:00")]
    [InlineData("2027-01-15T11:00:00Z")]
    public void CreateAndUpdateRequests_NormalizeExplicitOffsets(string timestamp)
    {
        string json = $$"""{"name":"Auktion","startsAt":"{{timestamp}}","endsAt":null}""";

        var create = JsonSerializer.Deserialize<CreateAuctionRequest>(json, JsonOptions)!;
        var update = JsonSerializer.Deserialize<UpdateAuctionRequest>(json, JsonOptions)!;

        var expected = new DateTimeOffset(2027, 1, 15, 11, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, create.StartsAt);
        Assert.Equal(TimeSpan.Zero, create.StartsAt!.Value.Offset);
        Assert.Equal(expected, update.StartsAt);
        Assert.Null(create.EndsAt);
        Assert.Null(update.EndsAt);
    }

    [Theory]
    [InlineData("2027-01-15T12:00:00")]
    [InlineData("")]
    [InlineData("not-a-time")]
    public void CreateAndUpdateRequests_RejectMissingOrInvalidOffsets(string timestamp)
    {
        string json = $$"""{"name":"Auktion","startsAt":"{{timestamp}}"}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateAuctionRequest>(json, JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateAuctionRequest>(json, JsonOptions));
    }

    [Fact]
    public void FormValidation_UsesTheInjectedClockAndCopenhagenTime()
    {
        var model = new CreateAuctionFormModel
        {
            Name = "Auktion",
            StartDate = new DateTime(2027, 1, 15),
            StartTime = "12:00",
            EndTime = "13:00"
        };
        var clock = new FixedClock(new DateTimeOffset(2027, 1, 15, 11, 1, 0, TimeSpan.Zero));
        using var services = new ClockServices(clock);
        var errors = model.Validate(new ValidationContext(model, services, null)).ToArray();

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(model.StartTime)));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class ClockServices(TimeProvider clock) : IServiceProvider, IDisposable
    {
        public object? GetService(Type serviceType) => serviceType == typeof(TimeProvider) ? clock : null;
        public void Dispose() { }
    }
}
