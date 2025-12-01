using System.Reflection;
using tik4net;
using tik4net.Objects;

namespace PhotonBypass.ServerBridge.Tik4net;

public class TikParam : ITikCommandParameter
{
    public TikParam(string name)
    {
        Name = name;
        Value = string.Empty;
    }

    public TikParam(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; set; }
    public string Value { get; set; }
    public TikCommandParameterFormat ParameterFormat { get; set; } = TikCommandParameterFormat.Filter;

    public static TikParam[] BuildFilter<T>(Func<T> expresion) where T : class, new()
    {
        var model = expresion?.Invoke();

        return typeof(T).GetProperties()
            .Select(p => new
            {
                Name = p.GetCustomAttributes<TikPropertyAttribute>().FirstOrDefault()?.FieldName,
                Value = p.GetValue(model)?.ToString(),
            })
            .Where(p => !string.IsNullOrEmpty(p.Name))
            .Select(p => new TikParam(p.Name ?? string.Empty, p.Value ?? string.Empty)
            {
                ParameterFormat = TikCommandParameterFormat.NameValue,
            })
            .ToArray();
    }

    public static TikParam True<T>(string name)
    {
        return new TikParam(GetFielName<T>(name) ?? string.Empty);
    }

    public static TikParam Not<T>(string name)
    {
        return new TikParam($"!{GetFielName<T>(name)}");
    }

    public static TikParam Equal<T>(string name, string value)
    {
        return new TikParam(GetFielName<T>(name) ?? string.Empty, value);
    }

    public static TikParam Less<T>(string name, string value)
    {
        return new TikParam($"<{GetFielName<T>(name)}", value);
    }

    public static TikParam Greater<T>(string name, string value)
    {
        return new TikParam($">{GetFielName<T>(name)}", value);
    }

    public static string? GetFielName<T>(string name)
    {
        return typeof(T).GetProperty(name)?
            .GetCustomAttributes<TikPropertyAttribute>()
            .FirstOrDefault()?
            .FieldName;
    }
}
