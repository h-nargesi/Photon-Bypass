using tik4net;

namespace PhotonBypass.ServerBridge.Tik4net;

public class TikFilterParam(string name, string value) : ITikCommandParameter
{
    public string Name { get; set; } = name;
    public string Value { get; set; } = value;
    public TikCommandParameterFormat ParameterFormat { get; set; } = TikCommandParameterFormat.Filter;
}
