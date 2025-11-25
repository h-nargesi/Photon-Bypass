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
        return new PersianCalendar().GetDayOfMonth(date).ToString("D2");
    }

    public static DateTime AddPersianMonth(this DateTime date, int month)
    {
        return new PersianCalendar().AddMonths(date, month);
    }

    public static int AddPersianMonthToDays(this DateTime date, int month)
    {
        return (int)(date.AddPersianMonth(month) - date).TotalDays;
    }
}
