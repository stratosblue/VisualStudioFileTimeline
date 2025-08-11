using System.Text;

namespace VisualStudioFileTimeline.Providers.VsCode;

internal static class UriExtensions
{
    public static string ToVsCodeCompatiblePath(this Uri uri)
    {
        var path = uri.AbsolutePath;
        var builder = new StringBuilder($"{uri.Scheme}://", uri.AbsolutePath.Length * 2);

        var index = 0;
        for (var i = 0; i < uri.Segments.Length; i++)
        {
            var item = uri.Segments[i];
            if (index++ == 1
                && char.IsLetter(item[0])
                && item[1] == ':')
            {
                item = item.ToLowerInvariant();
            }
            builder.Append(Uri.EscapeDataString(item.Substring(0, item.Length - 1)));
            builder.Append(item[item.Length - 1]);
        }

        return builder.ToString();
    }
}
