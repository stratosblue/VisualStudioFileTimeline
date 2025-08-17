using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
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

    private readonly ILogger _logger;

    private readonly PeriodicAsyncTrigger _periodicAsyncTrigger;

    private readonly RunningDocumentTable _runningDocumentTable;

    private readonly ConcurrentDictionary<uint, bool> _savingFilesMap = new();

    private readonly ConcurrentQueue<SavingFilesQueueItem> _savingFilesQueue = new();

    private bool _isDisposed;

    #endregion Private 字段

    #region Public 构造函数

    public RunningDocTableEventsListener(FileTimelineManager fileTimelineManager,
                                         FileTimelineViewModel fileTimelineViewModel,
                                         ILogger<RunningDocTableEventsListener> logger)
    {
        _fileTimelineManager = fileTimelineManager ?? throw new ArgumentNullException(nameof(fileTimelineManager));
        _fileTimelineViewModel = fileTimelineViewModel ?? throw new ArgumentNullException(nameof(fileTimelineViewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _runningDocumentTable = new RunningDocumentTable();
        _cookie = _runningDocumentTable.Advise(this);

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
            _logger.LogInformation("RunningDocTableEventsListener disposing.");

            try
            {
                _runningDocumentTable.Unadvise(_cookie);

                try
                {
                    _cancellationTokenSource.Cancel();
                }
                catch { }

                _cancellationTokenSource.Dispose();
                _periodicAsyncTrigger.Dispose();
            }
            finally
            {
                _logger.LogInformation("RunningDocTableEventsListener disposed.");
            }

            _isDisposed = true;
        }
    }

    public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) => 0;

    public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew) => 0;

    public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) => 0;

    public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => 0;

    public int OnAfterSave(uint docCookie)
    {
        _logger.LogInformation("Docuemnt [{Cookie}] OnAfterSave", docCookie);
        Uri? resource = null;

        try
        {
            ThrowIfDisposed();

            ThreadHelper.ThrowIfNotOnUIThread();

            if (_savingFilesMap.TryGetValue(docCookie, out var shouldAddHistory)
                && shouldAddHistory)
            {
                var runningDocumentInfo = _runningDocumentTable.GetDocumentInfo(docCookie);
                resource = new Uri(runningDocumentInfo.Moniker);

                _logger.LogInformation("Docuemnt [{Cookie}] start add history for {Moniker}.", docCookie, resource);

                //异步保存
                Task.Run(async () =>
                {
                    _logger.LogInformation("Docuemnt [{Cookie}] add history for {Moniker}.", docCookie, resource);
                    try
                    {
                        var descriptor = new FileHistoryDescriptor(resource, DateTime.Now, null);
                        var savedFileTimelineItem = await _fileTimelineManager.AddHistoryAsync(descriptor, _cancellationToken);
                        _fileTimelineViewModel.UpdateCurrentFileTimelineItems(savedFileTimelineItem);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Docuemnt [{Cookie}] add history for {Moniker} failed.", docCookie, resource);
                    }
                }).Forget();
            }
            else
            {
                _logger.LogInformation("Docuemnt [{Cookie}] do not need process.", docCookie);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Docuemnt [{Cookie}] {Moniker} OnAfterSave error.", docCookie, resource);
        }

        return 0;
    }

    public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
    {
        _logger.LogInformation("Docuemnt [{Cookie}] OnBeforeDocumentWindowShow", docCookie);
        object? pszMkDocument = null;

        try
        {
            ThrowIfDisposed();

            ThreadHelper.ThrowIfNotOnUIThread();

            if (pFrame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out pszMkDocument) != 0
                || pszMkDocument is not string moniker)
            {
                _logger.LogInformation("Docuemnt [{Cookie}] failed to obtain moniker, do not set view for it.", docCookie);
                return 0;
            }

            if (VisualStudioShellUtilities.IsProvisionalOpened(pFrame))   //临时打开，不做处理
            {
                _logger.LogInformation("Docuemnt [{Cookie}] do not set view for provisional docuemnt.", docCookie);
                return 0;
            }

            _logger.LogInformation("Docuemnt [{Cookie}] start set view for docuemnt {Moniker}.", docCookie, moniker);

            Task.Run(() =>
            {
                return _fileTimelineViewModel.ChangeCurrentFileAsync(new Uri(moniker), _cancellationToken);
            }, _cancellationToken).Forget();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Docuemnt [{Cookie}] {Moniker} OnBeforeDocumentWindowShow error.", docCookie, pszMkDocument);
        }

        return 0;
    }

    public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => 0;

    public int OnBeforeSave(uint docCookie)
    {
        _logger.LogInformation("Docuemnt [{Cookie}] OnBeforeSave", docCookie);

        try
        {
            ThrowIfDisposed();

            _periodicAsyncTrigger.TryToTrigger();

            ThreadHelper.ThrowIfNotOnUIThread();

            var runningDocumentInfo = _runningDocumentTable.GetDocumentInfo(docCookie);

            if (runningDocumentInfo.DocData is IVsPersistDocData docData
                && docData.IsDocDataDirty(out var isDirty) == 0
                && isDirty == 0)
            {
                _logger.LogInformation("Docuemnt [{Cookie}] cannot get dirty info, do not process docuemnt {Moniker}.", docCookie, runningDocumentInfo.Moniker);
                return 0;
            }

            //仅有修改的需要保存为记录
            //由于需要在保存完成后进行历史记录，在保存中检查是否有修改，并将路径加入到一个查询字典，以在保存后可以检查文件是否需要进行历史记录
            //HACK 保存完成后再进行备份是否合理？

            var savingFilesQueueItem = new SavingFilesQueueItem(docCookie, DateTime.UtcNow);

            _logger.LogInformation("Docuemnt [{Cookie}] is dirty. {Moniker} needs to be processed.", docCookie, runningDocumentInfo.Moniker);

            _savingFilesQueue.Enqueue(savingFilesQueueItem);
            _savingFilesMap[docCookie] = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Docuemnt [{Cookie}] OnBeforeSave error.", docCookie);
        }
        return 0;
    }

    #endregion Public 方法

    #region Private 方法

    private void CleanSavingFilesMap(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start CleanSavingFilesMap.");

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

                _logger.LogDebug("SavingFilesQueueItem {Item} removed.", item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CleanSavingFilesMap error.");
        }

        _logger.LogInformation("CleanSavingFilesMap finished.");
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
