using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Contracts.Time
{
    public static class CopenhagenTime
    {
        private static readonly TimeZoneInfo zone =
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

        public static bool TryToUtc(DateTime localTime, out DateTime utc, out string? error)
        {
            utc = default;
            error = null;

            if (localTime.Kind != DateTimeKind.Unspecified)
            {
                error = "Formularens tidspunkt skal være et lokalt klokkeslæt.";
                return false;
            }

            if (zone.IsInvalidTime(localTime))
            {
                error = "Klokkeslættet findes ikke på denne dato på grund af overgangen til sommertid.";
                return false;
            }

            if (zone.IsAmbiguousTime(localTime))
            {
                error = "Klokkeslættet forekommer to gange på denne dato. Vælg et entydigt klokkeslæt.";
                return false;
            }

            utc = TimeZoneInfo.ConvertTimeToUtc(localTime, zone);
            return true;

        }

        public static DateTime FromUtc(DateTime utc)
        {
            if (utc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Time should have DateTimeKind.Utc", nameof(utc));

            return TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        }

        public static DateOnly Today(TimeProvider clock)
        {
            return DateOnly.FromDateTime(FromUtc(clock.GetUtcNow().UtcDateTime));
        }
    }
}
