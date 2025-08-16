using System.Collections.Concurrent;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VisualStudioFileTimeline.Internal;
using VisualStudioFileTimeline.ViewModel;

namespace VisualStudioFileTimeline.VisualStudio;

/// <summary>
/// 文档事件监听器
/// </summary>
internal sealed class RunningDocTableEventsListener
    : IVsRunningDocTableEvents3, IDisposable
{
    #region Private 字段

    private readonly CancellationToken _cancellationToken;

    private readonly CancellationTokenSource _cancellationTokenSource;

    private readonly uint _cookie;

    private readonly FileTimelineManager _fileTimelineManager;

    private readonly FileTimelineViewModel _fileTimelineViewModel;

    private readonly PeriodicAsyncTrigger _periodicAsyncTrigger;

    private readonly RunningDocumentTable _runningDocumentTable;

    private readonly ConcurrentDictionary<uint, bool> _savingFilesMap = new();

    private readonly ConcurrentQueue<SavingFilesQueueItem> _savingFilesQueue = new();

    private bool _isDisposed;

    #endregion Private 字段

    #region Public 构造函数

    public RunningDocTableEventsListener(FileTimelineManager fileTimelineManager,
                                         FileTimelineViewModel fileTimelineViewModel)
    {
        _runningDocumentTable = new RunningDocumentTable();
        _cookie = _runningDocumentTable.Advise(this);

        _fileTimelineManager = fileTimelineManager;
        _fileTimelineViewModel = fileTimelineViewModel;

        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationToken = _cancellationTokenSource.Token;

        _periodicAsyncTrigger = new(() =>
        {
            CleanSavingFilesMap(_cancellationToken);
            return Task.CompletedTask;
        }, TimeSpan.FromMinutes(3));
    }

    #endregion Public 构造函数

    #region Public 方法

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _runningDocumentTable.Unadvise(_cookie);

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

    public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) => 0;

    public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew) => 0;

    public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) => 0;

    public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => 0;

    public int OnAfterSave(uint docCookie)
    {
        ThrowIfDisposed();

        ThreadHelper.ThrowIfNotOnUIThread();

        if (_savingFilesMap.TryGetValue(docCookie, out var shouldAddHistory)
            && shouldAddHistory)
        {
            var runningDocumentInfo = _runningDocumentTable.GetDocumentInfo(docCookie);
            var resource = new Uri(runningDocumentInfo.Moniker);

            //异步保存
            _ = Task.Run(async () =>
            {
                await Task.Yield();
                var descriptor = new FileHistoryDescriptor(resource, DateTime.Now, null);
                var savedFileTimelineItem = await _fileTimelineManager.AddHistoryAsync(descriptor, _cancellationToken);
                _fileTimelineViewModel.UpdateCurrentFileTimelineItems(savedFileTimelineItem);
            });
        }
        return 0;
    }

    public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
    {
        ThrowIfDisposed();

        ThreadHelper.ThrowIfNotOnUIThread();

        if (pFrame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out var pszMkDocument) != 0
            || pszMkDocument is not string moniker)
        {
            return 0;
        }

        if (VisualStudioShellUtilities.IsProvisionalOpened(pFrame))   //临时打开，不做处理
        {
            return 0;
        }

        _ = _fileTimelineViewModel.ChangeCurrentFileAsync(new Uri(moniker), _cancellationToken);

        return 0;
    }

    public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => 0;

    public int OnBeforeSave(uint docCookie)
    {
        ThrowIfDisposed();

        _periodicAsyncTrigger.TryToTrigger();

        ThreadHelper.ThrowIfNotOnUIThread();

        var runningDocumentInfo = _runningDocumentTable.GetDocumentInfo(docCookie);

        if (runningDocumentInfo.DocData is IVsPersistDocData docData
            && docData.IsDocDataDirty(out var isDirty) == 0
            && isDirty == 0)
        {
            return 0;
        }

        //仅有修改的需要保存为记录
        //由于需要在保存完成后进行历史记录，在保存中检查是否有修改，并将路径加入到一个查询字典，以在保存后可以检查文件是否需要进行历史记录
        //HACK 保存完成后再进行备份是否合理？

        _savingFilesQueue.Enqueue(new(docCookie, DateTime.UtcNow));
        _savingFilesMap[docCookie] = true;

        return 0;
    }

    #endregion Public 方法

    #region Private 方法

    private void CleanSavingFilesMap(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromMinutes(3);

        try
        {
            var time = DateTime.UtcNow - interval;
            while (_savingFilesQueue.TryPeek(out var item)
                   && !cancellationToken.IsCancellationRequested)
            {
                if (item.Time > time)
                {
                    break;
                }
                _savingFilesQueue.TryDequeue(out item);
                _savingFilesMap.TryRemove(item.DocCookie, out _);
            }
        }
        catch { }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(PeriodicAsyncTrigger));
        }
    }

    #endregion Private 方法

    private record struct SavingFilesQueueItem(uint DocCookie, DateTime Time);
}
