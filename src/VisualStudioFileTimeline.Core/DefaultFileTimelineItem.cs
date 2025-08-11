namespace VisualStudioFileTimeline;

public record DefaultFileTimelineItem(string Title,
                                      string? Description,
                                      string FilePath,
                                      DateTime Time,
                                      IFileTimelineProvider Provider)
    : IFileTimelineItem
{
    public string Identifier => FilePath;
};
