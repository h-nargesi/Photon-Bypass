using System.Text.Json;
using System.Text.RegularExpressions;

namespace PhotonBypass.Test.MockOptions;

public static partial class DateTimeConverter
{
    public static string PrepareAllDateTimes(this string json)
    {
        return FindDateTime().Replace(json, match => JsonSerializer.Serialize(match.ConvertToDateTime()));
    }

    private static DateTime ConvertToDateTime(this Match match)
    {
        if (match == null || match.Groups.Count < 3)
        {
            throw new JsonException("Invalid DateTime Value.");
        }

        var result = match.Groups[2].Value switch
        {
            "now" => DateTime.Now,
            "today" => DateTime.Now.Date,
            _ => throw new JsonException("Invalid DateTime Value.")
        };

        if (match.Groups.Count < 6 || string.IsNullOrEmpty(match.Groups[3].Value))
        {
            return result;
        }

        if (!int.TryParse(match.Groups[5].Value, out var days))
        {
            throw new JsonException("Invalid DateTime Value.");
        }

        result = match.Groups[4].Value switch
        {
            "+" => result.AddDays(days),
            "-" => result.AddDays(-days),
            _ => throw new JsonException("Invalid DateTime Value.")
        };

        if (match.Groups.Count < 9 || string.IsNullOrEmpty(match.Groups[6].Value))
        {
            return result;
        }

        if (!int.TryParse(match.Groups[8].Value, out var hours))
        {
            throw new JsonException("Invalid DateTime Value.");
        }

        result = match.Groups[7].Value switch
        {
            "+" => result.AddHours(hours),
            "-" => result.AddHours(-hours),
            _ => throw new JsonException("Invalid DateTime Value.")
        };

        return result;
    }

    [GeneratedRegex(@"""((today|now)((-|\+)(\d+))?((-|\+)(\d+))?)""")]
    private static partial Regex FindDateTime();
}