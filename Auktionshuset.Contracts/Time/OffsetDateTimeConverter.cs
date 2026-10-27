using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Auktionshuset.Contracts.Time
{
    public class OffsetDateTimeConverter : JsonConverter<DateTimeOffset?>
    {
        public override DateTimeOffset? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("Time should be an ISO 8601 string");

            string? text = reader.GetString();

            bool hasExplicitOffset = !string.IsNullOrEmpty(text)
                && (text.EndsWith('Z')
                    || (text.Length >= 6
                        && (text[^6] == '+' || text[^6] == '-')
                        && text[^3] == ':'));

            if (!hasExplicitOffset || !reader.TryGetDateTimeOffset(out DateTimeOffset value))
                throw new JsonException("Timeslot should contain Z or explicit offset, eg. +01:00");

            return value.ToUniversalTime();
        }

        public override void Write(
            Utf8JsonWriter writer,
            DateTimeOffset? value,
            JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value.Value.ToUniversalTime());
        }
    }
}
