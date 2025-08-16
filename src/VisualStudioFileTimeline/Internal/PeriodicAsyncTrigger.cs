namespace VisualStudioFileTimeline.Internal;

/// <summary>
/// 周期异步触发器
/// </summary>
internal sealed class PeriodicAsyncTrigger : IDisposable
{
    #region Private 字段

    private readonly Func<Task> _callback;

    private readonly CancellationToken _cancellationToken;

    private readonly CancellationTokenSource _tokenSource;

    private readonly TimeSpan _triggerInterval;

    private volatile bool _isDisposed;

    private Task? _lastTask;

    private DateTime _lastTriggerTime = DateTime.MinValue;

    #endregion Private 字段

    #region Public 构造函数

    public PeriodicAsyncTrigger(Func<Task> callback, TimeSpan triggerInterval)
    {
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        _triggerInterval = triggerInterval;

        _tokenSource = new();
        _cancellationToken = _tokenSource.Token;
    }

    #endregion Public 构造函数

    #region Public 方法

    public bool TryToTrigger()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(PeriodicAsyncTrigger));
        }

        var now = DateTime.UtcNow;
        if (_lastTriggerTime.Add(_triggerInterval) > now)
        {
            return false;
        }

        lock (_tokenSource)
        {
            if (_lastTriggerTime.Add(_triggerInterval) > now)
            {
                return false;
            }

            if (_lastTask is { } lastTask
                && !lastTask.IsCompleted)
            {
                return false;
            }

            _lastTriggerTime = now;

            _lastTask = Task.Run(_callback, _cancellationToken);
        }

        return true;
    }

    #endregion Public 方法

    #region Public 方法

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            _tokenSource.Cancel();
        }
        catch { }

        _tokenSource.Dispose();

        _isDisposed = true;
    }

    #endregion Public 方法
}
