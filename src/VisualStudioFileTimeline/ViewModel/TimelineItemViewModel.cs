using System.Diagnostics;
using System.Windows.Input;

namespace VisualStudioFileTimeline.ViewModel;

[DebuggerDisplay("{Description,nq} at {Time,nq} [{FilePath,nq}]")]
public class TimelineItemViewModel(FileTimeline timeline,
                                   IFileTimelineItem timelineItem,
                                   TimelineToolWindowViewModel toolWindowViewModel)
    : NotifyPropertyChangedObject
{
    #region Private 字段

    private bool _isSelected = default;

    #endregion Private 字段

    #region Public 属性

    public IFileTimelineItem RawItem { get; } = timelineItem ?? throw new ArgumentNullException(nameof(timelineItem));

    public FileTimeline Timeline { get; } = timeline ?? throw new ArgumentNullException(nameof(timeline));

    #region Display

    public string? Description { get; } = timelineItem.Description;

    public string FilePath { get; } = timelineItem.FilePath;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            SetProperty(ref _isSelected, value);

            if (value)
            {
                toolWindowViewModel.SelectedItem(this);
            }
            else
            {
                toolWindowViewModel.UnselectedItem(this);
            }
        }
    }

    public DateTime Time { get; } = timelineItem.Time;

    public string Title { get; } = timelineItem.Title;

    #endregion Display

    #region Commands

    public ICommand DeleteCommand => toolWindowViewModel.DeleteCommand;

    public ICommand OpenWithExplorerCommand => toolWindowViewModel.OpenWithExplorerCommand;

    public ICommand RestoreContentCommand => toolWindowViewModel.RestoreContentCommand;

    public ICommand ViewContentCommand => toolWindowViewModel.ViewContentCommand;

    #endregion Commands

    #endregion Public 属性

    #region Public 方法

    /// <summary>
    /// 刷新时间展示
    /// </summary>
    public void RefreshTime() => RaisePropertyChanged(nameof(Time));

    /// <inheritdoc/>
    public override string ToString() => RawItem.ToString();

    #endregion Public 方法
}
