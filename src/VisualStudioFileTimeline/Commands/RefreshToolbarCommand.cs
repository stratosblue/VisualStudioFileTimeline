using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using VisualStudioFileTimeline.ViewModel;

namespace VisualStudioFileTimeline.Commands;

[VisualStudioContribution]
public class RefreshToolbarCommand(FileTimelineViewModel fileTimelineViewModel) : Command
{
    #region Public 属性

    /// <inheritdoc />
    public override CommandConfiguration CommandConfiguration => new("%VSFT.Toolbar.Refresh%")
    {
        Icon = new CommandIconConfiguration(ImageMoniker.KnownValues.Refresh, IconSettings.IconOnly),
    };

    #endregion Public 属性

    #region Public 方法

    /// <inheritdoc />
    public override Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        return fileTimelineViewModel.ReloadCurrentFileAsync(cancellationToken);
    }

    #endregion Public 方法
}
