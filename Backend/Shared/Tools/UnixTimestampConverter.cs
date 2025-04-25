using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotonBypass.Tools;

public class UnixTimestampConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var timestamp = reader.GetInt64();
            return UnixTimeStampToDateTime(timestamp);
        }

        throw new JsonException("Invalid Unix timestamp.");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var unixTimestamp = DateTimeToUnixTimeStamp(value);
        writer.WriteNumberValue(unixTimestamp);
    }

    public static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
    {
        var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddMilliseconds(unixTimeStamp).ToLocalTime();
        return dateTime;
    }

    public static long DateTimeToUnixTimeStamp(DateTime input) => new DateTimeOffset(input).ToUnixTimeMilliseconds();
}