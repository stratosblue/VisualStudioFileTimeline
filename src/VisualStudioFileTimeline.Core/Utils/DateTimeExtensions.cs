namespace VisualStudioFileTimeline.Utils;

public static class DateTimeExtensions
{
    #region Public 方法

    public static DateTime FromUnixTimeMilliseconds(long timestamp)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).ToLocalTime().DateTime;
    }

    public static long ToUnixTimeMilliseconds(this DateTime dateTime)
    {
        return new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
    }

    #endregion Public 方法
}
