namespace VisualStudioFileTimeline;

public interface IFileTimelineItem
{
    #region Public 属性

    public string? Description { get; }

    public string FilePath { get; }

    public string Identifier { get; }

    public IFileTimelineProvider Provider { get; }

    public DateTime Time { get; }

    public string Title { get; }

    #endregion Public 属性
}
