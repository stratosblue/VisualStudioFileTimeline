namespace VisualStudioFileTimeline.Providers.Git;

public class GitFileTimelineProvider : IFileTimelineProvider
{
    #region Public 属性

    public string? Description { get; }

    public string Name { get; }

    #endregion Public 属性

    #region Public 方法

    public Task<IEnumerable<IFileTimelineItem>> GetAsync(Uri resource, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> TryDropAsync(IFileTimelineItem item, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    #endregion Public 方法
}
