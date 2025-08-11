using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using VisualStudioFileTimeline.View;

namespace VisualStudioFileTimeline.Commands;

/// <summary>
/// A command for showing a tool window.
/// </summary>
[VisualStudioContribution]
public class ShowToolWindowCommand : Command
{
    /// <inheritdoc />
    public override CommandConfiguration CommandConfiguration => new("%VSFT.GeneralTitle%")
    {
        Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
        Icon = new(ImageMoniker.KnownValues.Timeline, IconSettings.IconAndText),
    };

    /// <inheritdoc />
    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        await Extensibility.Shell().ShowToolWindowAsync<TimelineToolWindow>(activate: true, cancellationToken);
    }
}
