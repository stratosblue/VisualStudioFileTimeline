namespace VisualStudioFileTimeline;

public interface IFileTimelineProvider
{
    public string Name { get; }

    public string? Description { get; }

    Task<IEnumerable<IFileTimelineItem>> GetAsync(Uri resource, CancellationToken cancellationToken = default);
}

