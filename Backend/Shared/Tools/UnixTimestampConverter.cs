using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotonBypass.Tools;

public class UnixTimestampConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type type_to_convert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number)
        {
            throw new JsonException("Invalid Unix timestamp.");
        }
        
        var timestamp = reader.GetInt64();
        return UnixTimeStampToDateTime(timestamp);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var unix_timestamp = DateTimeToUnixTimeStamp(value);
        writer.WriteNumberValue(unix_timestamp);
    }

    private static DateTime UnixTimeStampToDateTime(long unix_time_stamp)
    {
        var date_time = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        date_time = date_time.AddMilliseconds(unix_time_stamp).ToLocalTime();
        return date_time;
    }

    public static long DateTimeToUnixTimeStamp(DateTime input) => new DateTimeOffset(input).ToUnixTimeMilliseconds();
}