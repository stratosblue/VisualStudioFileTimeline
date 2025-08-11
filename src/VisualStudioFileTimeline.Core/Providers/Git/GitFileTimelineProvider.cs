namespace VisualStudioFileTimeline.Providers.Git;

public class GitFileTimelineProvider : IFileTimelineProvider
{
    public string Name { get; }
    public string? Description { get; }

    public Task<IEnumerable<IFileTimelineItem>> GetAsync(Uri resource, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

