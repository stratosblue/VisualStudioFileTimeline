using Microsoft.Extensions.Logging;

namespace VisualStudioFileTimeline;

/// <summary>
/// 文件记录选项
/// </summary>
public record class VisualStudioFileTimelineOptions
{
    #region Public 属性

    public LogLevel LogLevel { get; set; } = LogLevel.Warning;

    public required string WorkingDirectory { get; init; }

    #endregion Public 属性

    #region Public 方法

    public string EnsureWorkingDirectory(string? subFolder = null)
    {
        var workingDirectory = WorkingDirectory;
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new InvalidOperationException($"Invalid working directory \"{workingDirectory}\"");
        }
        if (string.IsNullOrWhiteSpace(subFolder))
        {
            DirectoryUtil.Ensure(workingDirectory);
            return workingDirectory;
        }

        workingDirectory = Path.Combine(workingDirectory, subFolder);
        DirectoryUtil.Ensure(workingDirectory);
        return workingDirectory;
    }

    #endregion Public 方法
}
