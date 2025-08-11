using System.Runtime.Serialization;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.UI;
using VisualStudioFileTimeline.Internal;

namespace VisualStudioFileTimeline.ViewModel;

[DataContract]
public class FileTimelineViewModel(FileTimelineManager fileTimelineManager,
                                   TimelineToolWindowAccessor toolWindowAccessor,
                                   VisualStudioExtensibility extensibility)
    : NotifyPropertyChangedObject
{
    #region Private 字段

    private Uri? _currentResource = null;

    #endregion Private 字段

    #region Public 属性

    [DataMember]
    public TimelineToolWindowViewModel ToolWindowViewModel { get; } = new(extensibility);

    #endregion Public 属性

    #region Public 方法

    public async Task ChangeCurrentFileAsync(Uri resource, CancellationToken token)
    {
        if (string.Equals(_currentResource?.AbsolutePath, resource.AbsolutePath))
        {
            return;
        }
        _currentResource = resource;
        var fileTimeline = await fileTimelineManager.GetFileTimelineAsync(resource, token);
        ToolWindowViewModel.SetCurrentFileTimeline(fileTimeline);

        if (toolWindowAccessor.TimelineToolWindow is { } toolWindow)
        {
            toolWindow.SetCurrentFileName(fileTimeline.FileName);
        }
    }

    public async Task ReloadCurrentFileAsync(CancellationToken token)
    {
        if (_currentResource is { } currentResource)
        {
            var fileTimeline = await fileTimelineManager.GetFileTimelineAsync(currentResource, token);
            ToolWindowViewModel.SetCurrentFileTimeline(fileTimeline);
        }
    }

    public void UpdateCurrentFileTimelineItems(IFileTimelineItem fileTimelineItem)
    {
        ToolWindowViewModel.UpdateCurrentFileTimelineItems(fileTimelineItem);
    }

    #endregion Public 方法
}
