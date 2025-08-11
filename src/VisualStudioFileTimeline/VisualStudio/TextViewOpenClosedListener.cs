using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Shell;
using VisualStudioFileTimeline.ViewModel;

namespace VisualStudioFileTimeline.VisualStudio;

[VisualStudioContribution]
internal class TextViewOpenClosedListener(DocumentEventsListener documentEventsListener,
                                          FileTimelineViewModel fileTimelineViewModel,
                                          FileTimelineExtension extension,
                                          VisualStudioExtensibility extensibility)
    : ExtensionPart(extension, extensibility), ITextViewOpenClosedListener
{
    #region Private 字段

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private IDisposable? _lastDocumentEventsSubscribeDisposer;

    private DocumentsExtensibility? _lastDocumentsExtensibility;

    #endregion Private 字段

    #region Public 属性

    public TextViewExtensionConfiguration TextViewExtensionConfiguration => new()
    {
        AppliesTo = [DocumentFilter.FromGlobPattern("**/*", false)],
    };

    #endregion Public 属性

    #region Public 方法

    public Task TextViewClosedAsync(ITextViewSnapshot textView, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task TextViewOpenedAsync(ITextViewSnapshot textView, CancellationToken cancellationToken)
    {
        if (_lastDocumentsExtensibility is null
            || _lastDocumentsExtensibility.IsDisposed)
        {
            if (await VisualStudioShellUtilities.IsProvisionalOpenedAsync(textView.Uri, cancellationToken) is true)   //临时打开，不做处理
            {
                return;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_lastDocumentsExtensibility is null
                    || _lastDocumentsExtensibility.IsDisposed)
                {
                    await fileTimelineViewModel.ChangeCurrentFileAsync(textView.Uri, cancellationToken);

                    _lastDocumentEventsSubscribeDisposer?.Dispose();

                    _lastDocumentsExtensibility = Extensibility.Documents();

                    _lastDocumentEventsSubscribeDisposer = await _lastDocumentsExtensibility.SubscribeAsync(documentEventsListener, null, cancellationToken);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    #endregion Public 方法

    #region Protected 方法

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            _semaphore.Dispose();
            _lastDocumentEventsSubscribeDisposer?.Dispose();
        }
        base.Dispose(isDisposing);
    }

    #endregion Protected 方法
}
