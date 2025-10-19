using VisualStudioFileTimeline.Providers.Git;

namespace VisualStudioFileTimeline;

public record GitFileTimelineItem(string RootDirectory,
                                  string RelativeFilePath,
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
    public string FilePath { get; } = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));

    /// <inheritdoc/>
    public bool IsReadOnly => true;

    IFileTimelineProvider IFileTimelineItem.Provider => Provider;

    public async Task LoadAsTempFileAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(FilePath))
        {
            return;
        }
        var content = await Provider.ExecuteGitCommandAsync(RootDirectory, $"show {CommitInfo.CommitId}:\"{RelativeFilePath}\"", cancellationToken);
        File.WriteAllText(FilePath, content);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[{Time}] {Title}({CommitInfo.CommitId})";
    }
};
