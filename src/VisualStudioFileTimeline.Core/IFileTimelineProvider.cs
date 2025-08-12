namespace VisualStudioFileTimeline;

/// <summary>
/// 文件时间线提供器
/// </summary>
public interface IFileTimelineProvider
{
    #region Public 属性

    /// <summary>
    /// 提供器描述
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// 提供器名称
    /// </summary>
    public string Name { get; }

    #endregion Public 属性

    #region Public 方法

    /// <summary>
    /// 获取资源 <paramref name="resource"/> 的时间线项
    /// </summary>
    /// <param name="resource"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<IFileTimelineItem>> GetAsync(Uri resource, CancellationToken cancellationToken = default);

    /// <summary>
    /// 尝试删除项
    /// </summary>
    /// <param name="item"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<bool> TryDropAsync(IFileTimelineItem item, CancellationToken cancellationToken = default);

    #endregion Public 方法
}
