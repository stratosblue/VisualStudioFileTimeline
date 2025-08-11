namespace VisualStudioFileTimeline;

public interface IFileTimelineStore
{
    public string Name { get; }

    Task<IFileTimelineItem> AddHistoryAsync(FileHistoryDescriptor descriptor, CancellationToken cancellationToken = default);
}

