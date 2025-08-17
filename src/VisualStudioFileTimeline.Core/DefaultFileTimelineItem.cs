namespace VisualStudioFileTimeline;

public record DefaultFileTimelineItem(string Title,
                                      string? Description,
                                      string FilePath,
                                      DateTime Time,
                                      IFileTimelineProvider Provider)
    : IFileTimelineItem
{
    public string Identifier => FilePath;

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[{Time}] {Title}({FilePath}) ";
    }
};
