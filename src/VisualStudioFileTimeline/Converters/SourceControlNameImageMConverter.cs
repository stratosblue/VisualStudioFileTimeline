using System.Globalization;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.PlatformUI;

namespace VisualStudioFileTimeline.Converters;

public class SourceControlNameImageMonikerConverter : ValueConverter<string?, ImageMoniker>
{
    #region Protected 方法

    protected override ImageMoniker Convert(string? value, object parameter, CultureInfo culture)
    {
        return value switch
        {
            "git" => KnownMonikers.GitNoColor,
            _ => KnownMonikers.Document,
        };
    }

    #endregion Protected 方法
}
