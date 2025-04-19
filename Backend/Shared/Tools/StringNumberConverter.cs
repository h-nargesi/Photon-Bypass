using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotonBypass.Tools;

public class StringNumberConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt64();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return long.Parse(reader.GetString() ?? string.Empty);
        }

        throw new JsonException("Invalid Number.");
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}