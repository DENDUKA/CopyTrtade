namespace CopyTrading.Extensions;

public static class DateTimeExtension
{
    public static string ToStringMs(this DateTime dateTime)
    {
        return dateTime.ToString("dd.MM.yyyy HH:mm:ss.fff");
    }

    public static DateTime ToDateTime(this string dateTimeString)
    {
        return DateTime.ParseExact(
            dateTimeString,
            "dd.MM.yyyy HH:mm:ss.fff",
            System.Globalization.CultureInfo.InvariantCulture
        );
    }
}