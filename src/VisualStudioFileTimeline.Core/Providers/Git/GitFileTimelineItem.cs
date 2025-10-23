using VisualStudioFileTimeline.Providers.Git;

namespace VisualStudioFileTimeline;

public record GitFileTimelineItem(string SourceFilePath,
                                  string RootDirectory,
                                  string RelativeFilePath,
                                  string FileName,
                                  string FileIdentifier,
                                  GitCommitInfo CommitInfo,
                                  string Title,
                                  DateTime Time,
                                  GitFileTimelineProvider Provider)
    : IFileTimelineItem
{
    /// <inheritdoc/>
    public string Identifier => CommitInfo.CommitId;

    /// <inheritdoc/>
    public string? Description => CommitInfo.Body;

    /// <inheritdoc/>
    public string FilePath { get; } = Path.Combine(Provider.TemporaryDirectory, $"{FileIdentifier}_{CommitInfo.CommitId.Substring(20)}_{FileName}");

    /// <inheritdoc/>
    public FileTimelineItemFlag Flag => FileTimelineItemFlag.None;

    IFileTimelineProvider IFileTimelineItem.Provider => Provider;

    /// <inheritdoc/>
    public string? SourceControlName => "git";

    public async Task LoadAsTempFileAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(FilePath))
        {
            return;
        }
        var content = await Provider.ExecuteGitCommandAsync(RootDirectory, $"show --encoding=utf-8 {CommitInfo.CommitId}:\"{RelativeFilePath}\"", System.Text.Encoding.UTF8, cancellationToken);
        DirectoryUtil.Ensure(Provider.TemporaryDirectory);
        File.WriteAllText(FilePath, content);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[{Time}] {Title}({CommitInfo.CommitId})";
    }
};
