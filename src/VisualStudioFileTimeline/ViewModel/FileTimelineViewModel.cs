using System.Collections.Concurrent;
using Microsoft.VisualStudio.Shell;
using VisualStudioFileTimeline.Internal;

namespace VisualStudioFileTimeline.ViewModel;

public class FileTimelineViewModel : NotifyPropertyChangedObject, IDisposable
{
    #region Private 字段

    private readonly CancellationToken _cancellationToken;

    private readonly CancellationTokenSource _cancellationTokenSource;

    private readonly FileTimelineManager _fileTimelineManager;

    private readonly PeriodicAsyncTrigger _periodicAsyncTrigger;

    private Uri? _currentResource = null;

    private bool _isDisposed;

    #endregion Private 字段

    #region Public 属性

    public TimelineToolWindowViewModel ToolWindowViewModel { get; }

    #endregion Public 属性

    #region Public 构造函数

    public FileTimelineViewModel(FileTimelineManager fileTimelineManager,
                                 TimelineToolWindowViewModel toolWindowViewModel)
    {
        _fileTimelineManager = fileTimelineManager ?? throw new ArgumentNullException(nameof(fileTimelineManager));
        ToolWindowViewModel = toolWindowViewModel ?? throw new ArgumentNullException(nameof(toolWindowViewModel));

        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationToken = _cancellationTokenSource.Token;

        _periodicAsyncTrigger = new(() =>
        {
            CleanFileTimelineCache(_cancellationToken);
            return Task.CompletedTask;
        }, TimeSpan.FromMinutes(3));
    }

    #endregion Public 构造函数

    #region Public 方法

    public async Task ChangeCurrentFileAsync(Uri resource, CancellationToken token)
    {
        ThrowIfDisposed();

        if (string.Equals(_currentResource?.AbsolutePath, resource.AbsolutePath))
        {
            return;
        }
        _currentResource = resource;

        await SetCurrentFileTimelineAsync(resource, token);
    }

    public async Task ReloadCurrentFileAsync(CancellationToken token)
    {
        ThrowIfDisposed();

        if (_currentResource is { } currentResource)
        {
            _fileTimelineCache.TryRemove(currentResource.AbsolutePath, out _);
            await SetCurrentFileTimelineAsync(currentResource, token);
        }
    }

    public void UpdateCurrentFileTimelineItems(IFileTimelineItem fileTimelineItem)
    {
        ThrowIfDisposed();

        ToolWindowViewModel.UpdateCurrentFileTimelineItems(fileTimelineItem);
    }

    #region IDisposable

    public void Dispose()
    {
        if (!_isDisposed)
        {
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch { }

            _cancellationTokenSource.Dispose();
            _periodicAsyncTrigger.Dispose();

            _isDisposed = true;
        }
    }

    #endregion IDisposable

    #endregion Public 方法

    #region Private 方法

    #region FileTimelineCache

    private record class FileTimelineCacheEntry(FileTimeline Timeline, DateTime Time);

    private readonly ConcurrentDictionary<string, WeakReference<FileTimelineCacheEntry>> _fileTimelineCache = new(StringComparer.Ordinal);

    private void CleanFileTimelineCache(CancellationToken cancellationToken)
    {
        foreach (var key in _fileTimelineCache.Keys.ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_fileTimelineCache.TryGetValue(key, out var value)
                && !value.TryGetTarget(out _))
            {
                _fileTimelineCache.TryRemove(key, out _);
            }
        }
    }

    #endregion FileTimelineCache

    private async Task SetCurrentFileTimelineAsync(Uri resource, CancellationToken token)
    {
        //TODO 可配置的缓存时间
        const int CacheLifetime = -10;

        FileTimeline timeline;
        if (_fileTimelineCache.TryGetValue(resource.AbsolutePath, out var weakReference)
            && weakReference.TryGetTarget(out var cacheEntry)
            && cacheEntry is not null
            && cacheEntry.Time > DateTime.UtcNow.AddMinutes(CacheLifetime))
        {
            timeline = cacheEntry.Timeline;
        }
        else
        {
            timeline = await _fileTimelineManager.GetFileTimelineAsync(resource, token);
            _fileTimelineCache[resource.AbsolutePath] = new(new(timeline, DateTime.UtcNow));

            _periodicAsyncTrigger.TryToTrigger();
        }

        ToolWindowViewModel.SetCurrentFileTimeline(timeline);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(PeriodicAsyncTrigger));
        }
    }

    #endregion Private 方法
}
