using System.Collections.Concurrent;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.RpcContracts.Documents;
using VisualStudioFileTimeline.ViewModel;

namespace VisualStudioFileTimeline.VisualStudio;

internal sealed class DocumentEventsListener : IDocumentEventsListener, IDisposable
{
    #region Private 字段

    private readonly CancellationTokenSource _cancellationTokenSource;

    private readonly VisualStudioExtensibility _extensibility;

    private readonly FileTimelineManager _fileTimelineManager;

    private readonly FileTimelineViewModel _fileTimelineViewModel;

    private readonly ConcurrentDictionary<string, bool> _savingFilesMap = new(StringComparer.Ordinal);

    private readonly ConcurrentQueue<SavingFilesQueueItem> _savingFilesQueue = new();

    #endregion Private 字段

    #region Public 构造函数

    public DocumentEventsListener(FileTimelineManager fileTimelineManager,
                                  FileTimelineViewModel fileTimelineViewModel,
                                  VisualStudioExtensibility extensibility)
    {
        _fileTimelineManager = fileTimelineManager;
        _fileTimelineViewModel = fileTimelineViewModel;
        _extensibility = extensibility;

        _cancellationTokenSource = new CancellationTokenSource();

        _ = StartSavingFilesMapCleanAsync(_cancellationTokenSource.Token);
    }

    #endregion Public 构造函数

    #region Public 方法

    public Task ClosedAsync(DocumentEventArgs e, CancellationToken token)
    {
        return Task.CompletedTask;
    }

    public Task HiddenAsync(DocumentEventArgs e, CancellationToken token)
    {
        return Task.CompletedTask;
    }

    public Task OpenedAsync(DocumentEventArgs e, CancellationToken token)
    {
        return Task.CompletedTask;
    }

    public Task RenamedAsync(RenamedDocumentEventArgs e, CancellationToken token)
    {
        //TODO 移动记录文件
        return Task.CompletedTask;
    }

    public async Task SavedAsync(DocumentEventArgs e, CancellationToken token)
    {
        var resource = e.Moniker;
        if (_savingFilesMap.TryGetValue(resource.AbsolutePath, out var shouldAddHistory)
            && shouldAddHistory)
        {
            var savedFileTimelineItem = await _fileTimelineManager.AddHistoryAsync(new(resource, DateTime.Now, null), token);
            _fileTimelineViewModel.UpdateCurrentFileTimelineItems(savedFileTimelineItem);
        }
    }

    public async Task SavingAsync(DocumentEventArgs e, CancellationToken token)
    {
        var filePath = e.Moniker.AbsolutePath;

        //仅有修改的需要保存为记录
        //由于需要在保存完成后进行历史记录，在保存中检查是否有修改，并将路径加入到一个查询字典，以在保存后可以检查文件是否需要进行历史记录
        var document = await _extensibility.Documents()
                                           .GetOpenDocumentAsync(e.Moniker, token);
        _savingFilesQueue.Enqueue(new(filePath, DateTime.UtcNow));
        _savingFilesMap[filePath] = document?.IsDirty == true;
    }

    public async Task ShownAsync(DocumentEventArgs e, CancellationToken token)
    {
        if (await VisualStudioShellUtilities.IsProvisionalOpenedAsync(e.Moniker, token) is true)   //临时打开，不做处理
        {
            return;
        }
        await _fileTimelineViewModel.ChangeCurrentFileAsync(e.Moniker, token);
    }

    #region Dispose

    public void Dispose()
    {
        try
        {
            _cancellationTokenSource.Cancel();
        }
        catch { }
        _cancellationTokenSource.Dispose();
    }

    #endregion Dispose

    #endregion Public 方法

    #region Private 方法

    private async Task StartSavingFilesMapCleanAsync(CancellationToken cancellationToken)
    {
        //清理保存检查字典
        var interval = TimeSpan.FromMinutes(3);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var time = DateTime.UtcNow - interval;
                while (_savingFilesQueue.TryPeek(out var item))
                {
                    if (item.Time < time)
                    {
                        _savingFilesQueue.TryDequeue(out item);
                        _savingFilesMap.TryRemove(item.FilePath, out _);
                    }
                }
            }
            catch
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            await Task.Delay(interval, cancellationToken);
        }
    }

    #endregion Private 方法

    private record struct SavingFilesQueueItem(string FilePath, DateTime Time);
}
