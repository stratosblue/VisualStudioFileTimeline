using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.ToolWindows;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;
using VisualStudioFileTimeline.Commands;
using VisualStudioFileTimeline.Internal;
using VisualStudioFileTimeline.ViewModel;

namespace VisualStudioFileTimeline.View;

[VisualStudioContribution]
public class TimelineToolWindow : ToolWindow
{
    #region Private 字段

    private readonly TimelineToolWindowContent _content;

    private readonly FileTimelineViewModel _fileTimelineViewModel;

    #endregion Private 字段

    #region Private 属性

    [VisualStudioContribution]
    private static ToolbarConfiguration Toolbar => new("%VSFT.GeneralTitle%")
    {
        Children = [ToolbarChild.Command<RefreshToolbarCommand>()],
    };

    #endregion Private 属性

    #region Public 属性

    /// <inheritdoc />
    public override ToolWindowConfiguration ToolWindowConfiguration => new()
    {
        Placement = ToolWindowPlacement.DocumentWell,
        DockDirection = Dock.Right,
        AllowAutoCreation = false,
        //Toolbar = new ToolWindowToolbar(Toolbar), //卡死，先关了
        VisibleWhen = ActivationConstraint.SolutionState(SolutionState.Exists),
    };

    #endregion Public 属性

    #region Public 构造函数

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineToolWindow" /> class.
    /// </summary>
    public TimelineToolWindow(VisualStudioExtensibility extensibility, FileTimelineViewModel fileTimelineViewModel, TimelineToolWindowAccessor toolWindowAccessor)
        : base(extensibility)
    {
        Title = Resources.Caption_ToolWindow;

        _fileTimelineViewModel = fileTimelineViewModel;
        _content = new(fileTimelineViewModel.ToolWindowViewModel);
        toolWindowAccessor.TimelineToolWindow = this;
    }

    #endregion Public 构造函数

    #region Public 方法

    /// <inheritdoc />
    public override Task<IRemoteUserControl> GetContentAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IRemoteUserControl>(_content);
    }

    public void SetCurrentFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            Title = Resources.GeneralTitle;
            return;
        }
        Title = $"{Resources.GeneralTitle} - {fileName}";
    }

    #endregion Public 方法

    #region Protected 方法

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _content.Dispose();
        }

        base.Dispose(disposing);
    }

    #endregion Protected 方法
}
