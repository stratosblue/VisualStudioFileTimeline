using Microsoft.VisualStudio.Extensibility.UI;
using VisualStudioFileTimeline.ViewModel;

namespace VisualStudioFileTimeline.View;

internal class TimelineToolWindowContent(TimelineToolWindowViewModel toolWindowViewModel)
    : RemoteUserControl(dataContext: toolWindowViewModel)
{
}
