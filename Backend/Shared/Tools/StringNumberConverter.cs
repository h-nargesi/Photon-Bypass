using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotonBypass.Tools;

public class StringNumberConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type type_to_convert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String => long.Parse(reader.GetString() ?? string.Empty),
            _ => throw new JsonException("Invalid Number.")
        };
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}