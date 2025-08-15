namespace VisualStudioFileTimeline.ViewModel;

public class FileTimelineViewModel(FileTimelineManager fileTimelineManager, TimelineToolWindowViewModel toolWindowViewModel)
    : NotifyPropertyChangedObject
{
    #region Private 字段

    private Uri? _currentResource = null;

    #endregion Private 字段

    #region Public 属性

    public TimelineToolWindowViewModel ToolWindowViewModel { get; } = toolWindowViewModel;

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
