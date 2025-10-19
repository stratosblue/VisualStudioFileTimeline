using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace VisualStudioFileTimeline;

public class FileTimelineManager
{
    #region Private 字段

    private readonly ILogger _logger;

    private readonly List<ProviderWithSwitch> _providers;

    private readonly List<StoreWithSwitch> _stores;

    #endregion Private 字段

    #region Public 属性

    public IFileTimelineStore CurrentStore => _stores.First(static m => m.IsEnable = true).Store;

    public IEnumerable<IFileTimelineProvider> Providers => _providers.Select(static m => m.Provider);

    public IEnumerable<IFileTimelineStore> Stores => _stores.Select(static m => m.Store);

    #endregion Public 属性

    #region Public 构造函数

    public FileTimelineManager(IEnumerable<IFileTimelineProvider> providers, IEnumerable<IFileTimelineStore> stores, ILogger<FileTimelineManager> logger)
    {
        var nameHashSet = new HashSet<string>();
        _providers = providers.Where(m => nameHashSet.Add(m.Name))
                              .Select(m => new ProviderWithSwitch(m))
                              .ToList();
        nameHashSet.Clear();
        _stores = stores.Where(m => nameHashSet.Add(m.Name))
                        .Select(m => new StoreWithSwitch(m))
                        .ToList();
        _logger = logger ?? NullLogger<FileTimelineManager>.Instance;
    }

    #endregion Public 构造函数

    private record ProviderWithSwitch(IFileTimelineProvider Provider)
    {
        public bool IsEnable { get; set; } = true;
    }

    private record StoreWithSwitch(IFileTimelineStore Store)
    {
        public bool IsEnable { get; set; } = true;
    }

    #region Public 方法

    public Task<AddHistoryResult> AddHistoryAsync(FileHistoryDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        return CurrentStore.AddHistoryAsync(descriptor, cancellationToken);
    }

    public void DisableProvider(IFileTimelineProvider provider) => _providers.First(m => m.Provider == provider).IsEnable = false;

    public void EnableProvider(IFileTimelineProvider provider) => _providers.First(m => m.Provider == provider).IsEnable = true;

    public async Task<FileTimeline> GetFileTimelineAsync(Uri resource, CancellationToken cancellationToken = default)
    {
        var result = new FileTimeline(resource);

        foreach (var provider in _providers)
        {
            try
            {
                if (provider.IsEnable)
                {
                    var items = await provider.Provider.GetAsync(resource, cancellationToken);
                    foreach (var item in items)
                    {
                        result.AddOrUpdateItem(item, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error at get file timeline with {Provider}.", provider);
            }
        }

        return result;
    }

    #endregion Public 方法
}
