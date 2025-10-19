namespace VisualStudioFileTimeline;

public interface IFileTimelineItem
{
    #region Public 属性

    public string? Description { get; }

    public string FilePath { get; }

    public string Identifier { get; }

    /// <summary>
    /// 是否为只读
    /// </summary>
    public bool IsReadOnly { get; }

    public IFileTimelineProvider Provider { get; }

    public DateTime Time { get; }

    public string Title { get; }

    #endregion Public 属性
}
