using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PhotonBypass.Test.MockOutSources.Models;

public static partial class DateTimeConverter
{
    public static DateTime ConvertToDateTime(this string data)
    {
        DateTime result;
        if (data.StartsWith("now"))
        {
            result = DateTime.Now;
            data = data.Substring(3);
        }
        else if (data.StartsWith("today"))
        {
            result = DateTime.Now.Date;
            data = data.Substring(5);
        }
        else throw new JsonException("Invalid DateTime Value.");

        if (data.Length > 0)
        {
            int days;

            if (data.StartsWith('+'))
            {
                if (!int.TryParse(data.AsSpan(1), out days))
                {
                    throw new JsonException("Invalid DateTime Value.");
                }
            }
            else if (data.StartsWith('-'))
            {
                if (!int.TryParse(data.AsSpan(1), out days))
                {
                    throw new JsonException("Invalid DateTime Value.");
                }

                days = -days;
            }
            else throw new JsonException("Invalid DateTime Value.");

            result = result.AddDays(days);
        }

        return result;
    }

    public static string PrepareAllDateTimes(this string json)
    {
        return FindDateTime().Replace(json, (match) => JsonSerializer.Serialize(match.Groups[1].Value.ConvertToDateTime()));
    }

    [GeneratedRegex(@"""((today|now)(-|\+)\d+)""")]
    private static partial Regex FindDateTime();
}

class DateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var data = reader.GetString() ?? throw new JsonException("Invalid DateTime Value.");
            return data.ConvertToDateTime();
        }

        throw new JsonException("Invalid DateTime Format.");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

class DateTimeNullableJsonConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var data = reader.GetString();
            if (data == null) return null;
            else return data.ConvertToDateTime();
        }

        throw new JsonException("Invalid DateTime Format.");
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
