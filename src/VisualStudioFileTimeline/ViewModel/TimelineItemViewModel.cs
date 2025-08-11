using System.Diagnostics;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.Extensibility.UI;

namespace VisualStudioFileTimeline.ViewModel;

[DataContract]
[DebuggerDisplay("{Description,nq} at {Time,nq} [{FilePath,nq}]")]
public class TimelineItemViewModel(FileTimeline timeline, IFileTimelineItem timelineItem, TimelineToolWindowViewModel toolWindowViewModel) : NotifyPropertyChangedObject
{
    #region Private 字段

    private bool _isSelected = default;

    #endregion Private 字段

    #region Public 属性

    public IFileTimelineItem RawItem { get; } = timelineItem ?? throw new ArgumentNullException(nameof(timelineItem));

    public FileTimeline Timeline { get; } = timeline ?? throw new ArgumentNullException(nameof(timeline));

    #region Display

    [DataMember]
    public string? Description { get; } = timelineItem.Description;

    [DataMember]
    public string FilePath { get; } = timelineItem.FilePath;

    [DataMember]
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

    [DataMember]
    public DateTime Time { get; } = timelineItem.Time;

    [DataMember]
    public string Title { get; } = timelineItem.Title;

    #endregion Display

    #region Commands

    [DataMember]
    public AsyncCommand DeleteCommand { get; } = toolWindowViewModel.DeleteCommand;

    [DataMember]
    public AsyncCommand OpenWithExplorerCommand { get; } = toolWindowViewModel.OpenWithExplorerCommand;

    [DataMember]
    public AsyncCommand RestoreContentCommand { get; } = toolWindowViewModel.RestoreContentCommand;

    [DataMember]
    public AsyncCommand ViewContentCommand { get; } = toolWindowViewModel.ViewContentCommand;

    #endregion Commands

    #endregion Public 属性
}
