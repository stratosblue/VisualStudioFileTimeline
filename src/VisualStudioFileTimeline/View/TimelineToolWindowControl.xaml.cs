using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using VisualStudioFileTimeline.ViewModel;

namespace VisualStudioFileTimeline.View;

public partial class TimelineToolWindowControl : UserControl
{
    #region Private 字段

    private FileTimelineViewModel? _timelineViewModel;

    private TimelineToolWindowViewModel? _viewModel;

    #endregion Private 字段

    #region Public 属性

    public TimelineToolWindowViewModel? ViewModel
    {
        get => _viewModel ??= DataContext as TimelineToolWindowViewModel;
        set
        {
            _viewModel = value;
            _timelineViewModel = value?.Package.GetService<FileTimelineViewModel>();
            DataContext = value;
        }
    }

    #endregion Public 属性

    #region Public 构造函数

    public TimelineToolWindowControl()
    {
        InitializeComponent();
        IsVisibleChanged += ControlIsVisibleChanged;
    }

    #endregion Public 构造函数

    #region Private 方法

    private void ControlIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (e.NewValue is true)
        {
            if (_timelineViewModel is { } timelineViewModel)
            {
                _ = Task.Run(timelineViewModel.ChangeToActiveDocumentFileAsync);
            }
            viewModel.IsVisible = true;
        }
        else if (e.NewValue is false)
        {
            viewModel.IsVisible = false;
        }
    }

    #endregion Private 方法
}
