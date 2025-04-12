using Photon.Persian;

namespace PhotonBypass.Infra;

public static class PersianHandler
{
    public static string ToPersianString(this DateTime date)
    {
        return new Jalali(date).GetDate().ToNumberic();
    }
}
