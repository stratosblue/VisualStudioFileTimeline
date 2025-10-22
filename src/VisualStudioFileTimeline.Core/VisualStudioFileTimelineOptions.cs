using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace VisualStudioFileTimeline;

/// <summary>
/// 文件记录选项
/// </summary>
public record class VisualStudioFileTimelineOptions
{
    #region Private 字段

    private string? _temporaryDirectory;

    private string? _workingDirectory;

    #endregion Private 字段

    #region Public 属性

    public LocalHistoryOptions LocalHistory { get; set; } = new();

    public LogLevel LogLevel { get; set; } = LogLevel.Warning;

    public required string WorkingDirectory { get => _workingDirectory ??= GetDefaultWorkingDirectory(); init => _workingDirectory = value; }

    public required string TemporaryDirectory { get => _temporaryDirectory ??= GetDefaultTemporaryDirectory(); init => _temporaryDirectory = value; }

    #endregion Public 属性

    #region Public 方法

    public string EnsureTemporaryDirectory(string? subFolder = null)
    {
        var temporaryDirectory = TemporaryDirectory;
        if (string.IsNullOrWhiteSpace(temporaryDirectory))
        {
            throw new InvalidOperationException($"Invalid temporary directory \"{temporaryDirectory}\"");
        }
        if (string.IsNullOrWhiteSpace(subFolder))
        {
            DirectoryUtil.Ensure(temporaryDirectory);
            return temporaryDirectory;
        }

        temporaryDirectory = Path.Combine(temporaryDirectory, subFolder);
        DirectoryUtil.Ensure(temporaryDirectory);
        return temporaryDirectory;
    }

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

    public async Task WriteToFileAsync(string? filePath = null)
    {
        filePath ??= GetDefaultConfigurationFilePath();
        DirectoryUtil.Ensure(Path.GetDirectoryName(filePath));
        using var fs = File.OpenWrite(filePath);
        await JsonSerializer.SerializeAsync(fs, this);
    }

    #region 静态方法

    public static string GetDefaultConfigurationFilePath()
    {
        return Path.Combine(GetDefaultWorkingDirectory(), "configuration.json");
    }

    public static string GetDefaultTemporaryDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "VSFileTimeline");
    }

    public static string GetDefaultWorkingDirectory()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VSFileTimeline");
    }

    public static VisualStudioFileTimelineOptions LoadFromDefaultConfigurationFile(out string? loadMessage) => LoadFromFile(GetDefaultConfigurationFilePath(), out loadMessage);

    public static VisualStudioFileTimelineOptions LoadFromFile(string filePath, out string? loadMessage)
    {
        VisualStudioFileTimelineOptions? options = null;
        loadMessage = null;

        try
        {
            if (File.Exists(filePath))
            {
                options = JsonSerializer.Deserialize<VisualStudioFileTimelineOptions>(File.ReadAllText(filePath));
            }
        }
        catch (Exception ex)
        {
            loadMessage = $"Load options from {filePath} failed. {ex}";
        }

        return options ?? new()
        {
            WorkingDirectory = GetDefaultWorkingDirectory(),
            TemporaryDirectory = GetDefaultTemporaryDirectory(),
        };
    }

    public static void SaveToDefaultConfigurationFile(VisualStudioFileTimelineOptions options, out string? saveMessage) => SaveToFile(options, GetDefaultConfigurationFilePath(), out saveMessage);

    public static void SaveToFile(VisualStudioFileTimelineOptions options, string filePath, out string? saveMessage)
    {
        saveMessage = null;
        try
        {
            File.WriteAllText(filePath, JsonSerializer.Serialize(options));
        }
        catch (Exception ex)
        {
            saveMessage = $"Save options to {filePath} failed. {ex}";
        }
    }

    #endregion 静态方法

    #endregion Public 方法
}
