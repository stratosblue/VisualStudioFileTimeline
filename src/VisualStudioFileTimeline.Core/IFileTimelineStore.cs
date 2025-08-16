namespace VisualStudioFileTimeline;

public interface IFileTimelineStore
{
    #region Public 属性

    public string Name { get; }

    #endregion Public 属性

    #region Public 方法

    Task<IFileTimelineItem> AddHistoryAsync(FileHistoryDescriptor descriptor, CancellationToken cancellationToken = default);

    #endregion Public 方法
}
