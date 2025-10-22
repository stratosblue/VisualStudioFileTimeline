namespace VisualStudioFileTimeline;

public record DefaultFileTimelineItem(string Title,
                                      string? Description,
                                      string FilePath,
                                      DateTime Time,
                                      IFileTimelineProvider Provider,
                                      FileTimelineItemFlag Flag)
    : IFileTimelineItem
{
    /// <inheritdoc/>
    public string Identifier => FilePath;

    /// <inheritdoc/>
    public string? SourceControlName => null;

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[{Time}] {Title}({FilePath}) ";
    }
};
