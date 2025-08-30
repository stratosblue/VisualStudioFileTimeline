using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Shell;
using VisualStudioFileTimeline.ViewModel;

namespace VisualStudioFileTimeline.View;

[Guid("73bc0a27-0d23-4ef7-93b3-09bee11b68bd")]
public class TimelineToolWindow : ToolWindowPane
{
    #region Private 字段

    private readonly TimelineToolWindowControl _content;

    #endregion Private 字段

    #region Public 构造函数

    public TimelineToolWindow() : base(null)
    {
        Caption = Resources.Caption_ToolWindow;

        _content = new TimelineToolWindowControl();
        Content = _content;
    }

    #endregion Public 构造函数

    #region Protected 方法

    protected override void Initialize()
    {
        base.Initialize();

        var viewModel = ((AsyncPackage)Package).GetRequiredService<FileTimelineViewModel>();

        _content.ViewModel = viewModel.ToolWindowViewModel;
    }

    #endregion Protected 方法
}
