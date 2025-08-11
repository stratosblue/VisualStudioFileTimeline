using System.Globalization;
using Microsoft.VisualStudio.PlatformUI;

namespace VisualStudioFileTimeline.Converters;

public class TimeDisplayConverter : ValueConverter<DateTime, string>
{
    #region Public 字段

    public const int DaySeconds = 24 * HourSeconds;

    public const int HourSeconds = 60 * MinuteSeconds;

    public const int MinuteSeconds = 60;

    public const int MonthSeconds = 30 * DaySeconds;

    public const int WeekSeconds = 7 * DaySeconds;

    public const int YearSeconds = 365 * MonthSeconds;

    #endregion Public 字段

    #region Protected 方法

    protected override string Convert(DateTime value, object parameter, CultureInfo culture)
    {
        var now = DateTime.Now;

        var timeSpan = now - value;

        var seconds = timeSpan.TotalSeconds;

        if (seconds < MinuteSeconds)
        {
            return Resources.TimeDisplayFormat_Now;
        }
        else if (seconds < HourSeconds)
        {
            return string.Format(Resources.TimeDisplayFormat_Minute, (int)seconds / MinuteSeconds);
        }
        else if (seconds < DaySeconds)
        {
            return string.Format(Resources.TimeDisplayFormat_Hour, (int)seconds / HourSeconds);
        }
        else if (seconds < WeekSeconds)
        {
            return string.Format(Resources.TimeDisplayFormat_Day, (int)seconds / DaySeconds);
        }
        else if (seconds < MonthSeconds)
        {
            return string.Format(Resources.TimeDisplayFormat_Week, (int)seconds / WeekSeconds);
        }
        else if (seconds < YearSeconds)
        {
            return string.Format(Resources.TimeDisplayFormat_Month, (int)seconds / MonthSeconds);
        }
        return string.Format(Resources.TimeDisplayFormat_Year, (int)seconds / YearSeconds);
    }

    #endregion Protected 方法
}
