namespace VisualStudioFileTimeline;

/// <summary>
/// 文件时间线项目的标记
/// </summary>
[Flags]
public enum FileTimelineItemFlag
{
    /// <summary>
    /// 无
    /// </summary>
    None = 0,

    /// <summary>
    /// 可删除
    /// </summary>
    Deletable = 1 << 0,

    /// <summary>
    /// 具有本地文件
    /// </summary>
    HasLocalFile = 1 << 1,
}
