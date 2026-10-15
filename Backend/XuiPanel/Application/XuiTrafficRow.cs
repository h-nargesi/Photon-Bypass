namespace PhotonBypass.Xui.Application;

public static class XuiTrafficRow
{
    public const string SessionIdPrefix = "xui:";

    public static string SessionId(int account_id, int renewal_id, DateTime day)
    {
        return $"{SessionIdPrefix}{account_id}:{renewal_id}:{day:yyyyMMdd}";
    }
}
