using Serilog.Core;
using Serilog.Events;

namespace VisualStudioFileTimeline.Internal.Serilog;

internal sealed class ThreadInfoEnricher : ILogEventEnricher
{
    #region Public 字段

    public const string ProcessIdPropertyName = "ProcessId";

    public const string ThreadInfoPropertyName = "ThreadInfo";

    #endregion Public 字段

    #region Private 字段

    private static readonly ThreadLocal<LogEventProperty> s_threadInfoCache = new();

    private readonly LogEventProperty _processIdProperty;

    #endregion Private 字段

    #region Public 构造函数

    public ThreadInfoEnricher()
    {
        _processIdProperty = new LogEventProperty(ProcessIdPropertyName, new ScalarValue(GetProcessId()));
    }

    #endregion Public 构造函数

    #region Public 方法

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(_processIdProperty);

        if (s_threadInfoCache.Value is not { } threadInfo)
        {
            var thread = Thread.CurrentThread;
            threadInfo = new(ThreadInfoPropertyName, new ScalarValue(new ThreadInfo(thread.ManagedThreadId, thread.Name)));
            s_threadInfoCache.Value = threadInfo;
        }

        logEvent.AddPropertyIfAbsent(threadInfo);
    }

    #endregion Public 方法

    #region Private 方法

    private static int GetProcessId()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return process.Id;
    }

    #endregion Private 方法
}

public record class ThreadInfo(int ThreadId, string? ThreadName)
{
    private readonly string _string = string.IsNullOrWhiteSpace(ThreadName) ? $"<{ThreadId}>" : $"<{ThreadId}({ThreadName})>";

    public override string ToString() => _string;
}
