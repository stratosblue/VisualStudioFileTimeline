using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;
using VisualStudioFileTimeline.ViewModel;

namespace VisualStudioFileTimeline.VisualStudio;

[VisualStudioContribution]
internal class TextViewOpenClosedListener(RunningDocTableEventsListener runningDocTableEventsListener,
                                          FileTimelineViewModel fileTimelineViewModel,
                                          FileTimelineExtension extension,
                                          VisualStudioExtensibility extensibility)
    : ExtensionPart(extension, extensibility), ITextViewOpenClosedListener
{
    #region Private 字段

    /// <summary>
    /// 引用以保证初始化
    /// </summary>
    private readonly RunningDocTableEventsListener _runningDocTableEventsListener = runningDocTableEventsListener;

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
        if (await VisualStudioShellUtilities.IsProvisionalOpenedAsync(textView.Uri, cancellationToken) is true)   //临时打开，不做处理
        {
            return;
        }

        await fileTimelineViewModel.ChangeCurrentFileAsync(textView.Uri, cancellationToken);
    }

    #endregion Public 方法
}
