using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Auktionshuset.Infrastructure.Data
{
    public class AuctionDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        private static readonly TimeZoneInfo _copenhagenTimeZoneInfo =
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

        public AuctionDateTimeConverter() : base(
            value => ToUtc(value),
            value => FromUtc(value))
        {

        }

        private static DateTime ToUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc) return value;

            if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();

            if (_copenhagenTimeZoneInfo.IsInvalidTime(value))
            {
                throw new ArgumentException("Time is invalid in the Copenhagen time zone.");
            }

            if (_copenhagenTimeZoneInfo.IsAmbiguousTime(value))
            {
                throw new ArgumentException("Time is invalid in the Copenhagen time zone.");
            }

            return TimeZoneInfo.ConvertTimeToUtc(value, _copenhagenTimeZoneInfo);
        }

        public static DateTime FromUtc(DateTime value)
        {
            DateTime utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, _copenhagenTimeZoneInfo);
        }
    }
}
