namespace VisualStudioFileTimeline;

/// <summary>
/// 本地历史记录选项
/// </summary>
public class LocalHistoryOptions
{
    #region Public 属性

    /// <summary>
    /// 最新历史记录合并时间窗口
    /// </summary>
    public TimeSpan LastHistoryMergeWindow { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 历史记录最大保留数量，超过此数量时，最早的记录会被删除
    /// </summary>
    public int RetentionLimit { get; set; } = 100;

    /// <summary>
    /// 历史记录最大保留时间窗口
    /// TODO 添加一键清理功能
    /// </summary>
    public TimeSpan? RetentionWindow { get; set; } = TimeSpan.FromDays(100);

    #endregion Public 属性
}
