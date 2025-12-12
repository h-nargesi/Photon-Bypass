using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PhotonBypass.Test.MockOptions;

public static partial class DateTimeConverter
{
    public static DateTime ConvertToDateTimeStrict(this string data)
    {
        return data.ConvertToDateTime() ??
               throw new JsonException("Invalid DateTime Value.");
    }

    public static DateTime? ConvertToDateTime(this string? data)
    {
        if (string.IsNullOrEmpty(data)) return null;
        
        DateTime result;
        if (data.StartsWith("now"))
        {
            result = DateTime.Now;
            data = data[3..];
        }
        else if (data.StartsWith("today"))
        {
            result = DateTime.Now.Date;
            data = data[5..];
        }
        else return null;

        if (data.Length <= 0) return result;
        
        int days;

        if (data.StartsWith('+'))
        {
            if (!int.TryParse(data.AsSpan(1), out days))
            {
                return null;
            }
        }
        else if (data.StartsWith('-'))
        {
            if (!int.TryParse(data.AsSpan(1), out days))
            {
                return null;
            }

            days = -days;
        }
        else return null;

        result = result.AddDays(days);

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
    public override DateTime Read(ref Utf8JsonReader reader, Type type_to_convert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("Invalid DateTime Format.");
        
        var data = reader.GetString() ?? throw new JsonException("Invalid DateTime Value.");

        return data.ConvertToDateTimeStrict();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime());
    }
}

class DateTimeNullableJsonConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type type_to_convert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) 
            throw new JsonException("Invalid DateTime Format.");
        
        var data = reader.GetString();
        
        return data?.ConvertToDateTimeStrict();
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
