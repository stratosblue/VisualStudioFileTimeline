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

    /// <summary>
    /// 源码控制名称(应当为a-z0-9_)
    /// </summary>
    public string? SourceControlName { get; }

    public DateTime Time { get; }

    public string Title { get; }

    #endregion Public 属性
}
