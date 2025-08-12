namespace VisualStudioFileTimeline;

public class FileTimeline(Uri resource)
{
    #region Public 属性

    public string FileName { get; } = Path.GetFileName(resource.AbsolutePath);

    public Uri Resource { get; } = resource;

    public IReadOnlyList<IFileTimelineItem> TimelineItems => SortedTimelineItems;

    #endregion Public 属性

    #region Internal 属性

    internal List<IFileTimelineItem> SortedTimelineItems { get; } = [];

    #endregion Internal 属性

    #region Public 方法

    public int AddOrUpdateItem(IFileTimelineItem item, out int removedIndex)
    {
        removedIndex = SortedTimelineItems.FindIndex(m => m.Identifier == item.Identifier);
        if (removedIndex >= 0)
        {
            SortedTimelineItems.RemoveAt(removedIndex);
        }
        var insertIndex = SortedTimelineItems.FindIndex(m => m.Time < item.Time);

        if (insertIndex >= 0)
        {
            SortedTimelineItems.Insert(insertIndex, item);
        }
        else
        {
            insertIndex = 0;
            SortedTimelineItems.Add(item);
        }

        return insertIndex;
    }

    public async Task<bool> DeleteItemAsync(IFileTimelineItem item, CancellationToken cancellationToken)
    {
        if (await item.Provider.TryDropAsync(item, cancellationToken))
        {
            SortedTimelineItems.Remove(item);
            return true;
        }
        return false;
    }

    #endregion Public 方法
}

internal sealed class DescendingDateTimeComparer : IComparer<DateTime>
{
    #region Public 属性

    public static DescendingDateTimeComparer Shared { get; } = new();

    #endregion Public 属性

    #region Public 方法

    public int Compare(DateTime x, DateTime y) => y.CompareTo(x);

    #endregion Public 方法
}
