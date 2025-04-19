using Photon.Persian;

namespace PhotonBypass.Tools;

public static class PersianHandler
{
    public static string ToPersianString(this DateTime date)
    {
        return new Jalali(date).GetDate().ToNumberic();
    }

    public static string ToPersianString(this DateTime date, string format)
    {
        return new Jalali(date).GetDate().ToString(format);
    }
}
