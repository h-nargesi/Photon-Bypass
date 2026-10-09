using System.Text.RegularExpressions;

namespace PhotonBypass.ServerBridge;

public static partial class InjectionRegex
{
    [GeneratedRegex(@"\A[0-9a-fA-F]+\z")]
    public static partial Regex SessionId();

    [GeneratedRegex(@"\A[0-9A-Za-z_.\-]+\z")]
    public static partial Regex Username();
}
