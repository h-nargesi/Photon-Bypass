using System.Globalization;

namespace PhotonBypass.Tools;

public static class PersianHandler
{
    public static string ToPersianString(this DateTime date)
    {
        var cl = new PersianCalendar();
        return $"{cl.GetYear(date):D4}/{cl.GetMonth(date):D2}/{cl.GetDayOfMonth(date):D2}";
    }

    public static string ToPersianDayOfMonth(this DateTime date)
    {
        var cl = new PersianCalendar();
        return cl.GetDayOfMonth(date).ToString("D2");
    }

    public static int AddMonthToDays(this DateTime date, int month)
    {
        var cl = new PersianCalendar();
        var target = cl.AddMonths(date, month);
        return (int)(target - date).TotalDays;
    }
}
