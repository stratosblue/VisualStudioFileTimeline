using System.Buffers;
using System.IO.Extensions;
using System.IO.Hashing;
using System.Text;
using System.Text.Json;
using VisualStudioFileTimeline.Utils;

namespace VisualStudioFileTimeline.Providers.Default;

public class LocalHistoryFileTimelineProvider : IFileTimelineProvider, IFileTimelineStore
{
    #region Private 字段

    /// <summary>
    /// 合并窗口
    /// TODO 配置化
    /// </summary>
    private readonly TimeSpan _mergeWindow = TimeSpan.FromSeconds(10);

    #endregion Private 字段

    #region Public 字段

    public const string MetadataFileName = ".metadata.json";

    public const int Version = 1;

    #endregion Public 字段

    #region Public 属性

    public string? Description { get; } = Resources.ProviderDescription_LocalHistory;

    public string LocalHistoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VSFileTimeline", "History");

    public string Name { get; } = "localHistory";

    #endregion Public 属性

    #region Public 方法

    public static string CreateFileTimelineItemTitleBySource(string? source)
    {
        return source switch
        {
            _ => Resources.TimelineItemSource_SavedFile,
        };
    }

    /// <inheritdoc/>
    public async Task<IFileTimelineItem> AddHistoryAsync(FileHistoryDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        var resource = descriptor.Resource;
        var sourceFile = resource.AbsolutePath;

        var metadataInfo = await GetMetadataInfoAsync(sourceFile, cancellationToken);
        var metadata = metadataInfo.Metadata;
        var historyFolderPath = metadataInfo.HistoryFolderPath;
        var currentTimestamp = descriptor.Time.ToUnixTimeMilliseconds();

        DirectoryUtil.Ensure(historyFolderPath);

        string historyFilePath;

        if (metadata.Entries.OrderByDescending(m => m.Value.Timestamp)
                            .FirstOrDefault() is { } lastEntry
            && lastEntry.Key is { Length: > 0 } historyFileName
            && lastEntry.Value is { } entryInfo
            && entryInfo.Timestamp >= currentTimestamp - _mergeWindow.TotalMilliseconds)    //合并
        {
            //合并到上一次保存
            entryInfo.Timestamp = currentTimestamp;
            historyFilePath = Path.Combine(historyFolderPath, historyFileName);
        }
        else    //添加
        {
            var fileName = SequenceFileNameUtil.GenerateName(Path.GetExtension(sourceFile));
            historyFilePath = Path.Combine(historyFolderPath, fileName);

            var newEntryInfo = new FileTimelineMetadataEntryInfo(descriptor.Source)
            {
                Timestamp = currentTimestamp,
            };

            //TODO 清理
            metadata.Entries.Add(fileName, newEntryInfo);
        }

        File.Copy(sourceFile, historyFilePath, true);
        File.SetCreationTimeUtc(historyFilePath, DateTimeOffset.FromUnixTimeMilliseconds(currentTimestamp).DateTime);

        await SaveMetadataAsync(metadataInfo, CancellationToken.None);

        return new DefaultFileTimelineItem(Title: CreateFileTimelineItemTitleBySource(descriptor.Source),
                                           Description: null,
                                           FilePath: historyFilePath,
                                           Time: descriptor.Time,
                                           Provider: this);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IFileTimelineItem>> GetAsync(Uri resource, CancellationToken cancellationToken = default)
    {
        var sourceFile = resource.AbsolutePath;

        var metadataInfo = await GetMetadataInfoAsync(sourceFile, cancellationToken);
        var metadata = metadataInfo.Metadata;
        var historyFolderPath = metadataInfo.HistoryFolderPath;

        //目录不存在，返回空
        if (!Directory.Exists(historyFolderPath))
        {
            return [];
        }

        var allFiles = GetAllFiles(sourceFile, historyFolderPath);
        if (allFiles.Count == 0)
        {
            return [];
        }

        var metadataMismatched = false;
        var result = allFiles.Select(m =>
        {
            string? source = null;
            DateTime? time = null;
            if (!metadata.Entries.TryGetValue(Path.GetFileName(m.FullName), out var entryInfo))
            {
                metadataMismatched = true;
                time = m.CreationTime;
            }
            else
            {
                source = entryInfo.Source;
                time = DateTimeExtensions.FromUnixTimeMilliseconds(entryInfo.Timestamp);
            }
            return new DefaultFileTimelineItem(Title: CreateFileTimelineItemTitleBySource(source),
                                               Description: null,
                                               FilePath: m.FullName,
                                               Time: time ?? m.CreationTime,
                                               Provider: this);
        }).ToList();

        if (metadataMismatched
            || result.Count != metadata.Entries.Count) //元数据不匹配，重新生成元数据
        {
            _ = Task.Run(async () =>
            {
                //移除多余的
                foreach (var key in metadata.Entries.Keys.ToList())
                {
                    if (!result.Any(m => Path.GetFileName(m.FilePath) == key))
                    {
                        metadata.Entries.Remove(key);
                    }
                }

                //添加缺少的
                foreach (var item in result)
                {
                    var fileName = Path.GetFileName(item.FilePath);
                    if (!metadata.Entries.ContainsKey(fileName))
                    {
                        metadata.Entries.Add(fileName, new(null) { Timestamp = item.Time.ToUnixTimeMilliseconds() });
                    }
                }

                await SaveMetadataAsync(metadataInfo, CancellationToken.None);
            });
        }

        return result;

        List<FileInfo> GetAllFiles(string sourceFile, string historyFolderPath)
        {
            if (!Directory.Exists(historyFolderPath))
            {
                return [];
            }
            return EnumerateAllHistoryFiles(historyFolderPath, Path.GetExtension(sourceFile));
        }
    }

    /// <inheritdoc/>
    public override string ToString() => Name;

    /// <inheritdoc/>
    public async Task<bool> TryDropAsync(IFileTimelineItem item, CancellationToken cancellationToken = default)
    {
        if (item.Provider != this)
        {
            return false;
        }

        try
        {
            File.Delete(item.FilePath);
        }
        catch
        {
            if (File.Exists(item.FilePath))
            {
                return false;
            }
        }

        var metadataInfo = await GetMetadataInfoAsync(item.FilePath, cancellationToken);

        if (metadataInfo.Metadata.Entries.Remove(Path.GetFileName(item.FilePath)))
        {
            await SaveMetadataAsync(metadataInfo, CancellationToken.None);
        }

        return true;
    }

    #endregion Public 方法

    #region Private 方法

    private static string GetMetadataFilePath(string historyFolderPath)
    {
        return Path.Combine(historyFolderPath, MetadataFileName);
    }

    private List<FileInfo> EnumerateAllHistoryFiles(string historyFolderPath, string extension)
    {
        var directoryInfo = new DirectoryInfo(historyFolderPath);
        return directoryInfo.EnumerateFiles($"????????{extension}", SearchOption.TopDirectoryOnly)
                            .OrderByDescending(m => m.CreationTime)
                            .ToList();
    }

    private string GetHistoryFolderPath(string path, out string identifier)
    {
        var pathMemory = ArrayPool<char>.Shared.Rent(path.Length);
        try
        {
            var length = path.AsSpan().ToLowerInvariant(pathMemory.AsSpan());

            var spanMemory = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(path.Length));
            var span = spanMemory.AsSpan();
            try
            {
                length = Encoding.UTF8.GetBytes(pathMemory, 0, length, spanMemory, 0);

                Span<byte> buffer = stackalloc byte[sizeof(int)];
                Crc32.Hash(span.Slice(0, length), buffer);

                identifier = SequenceFileNameUtil.Create(buffer);

                return Path.Combine(LocalHistoryPath, identifier);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(spanMemory);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(pathMemory);
        }
    }

    private async Task<MetadataInfo> GetMetadataInfoAsync(string sourceFile, CancellationToken cancellationToken)
    {
        var historyFolderPath = GetHistoryFolderPath(sourceFile, out var identifier);
        var entriesFilePath = GetMetadataFilePath(historyFolderPath);
        FileTimelineMetadata? metadata = null;

        if (File.Exists(entriesFilePath))
        {
            using var stream = File.Open(entriesFilePath, FileMode.Open);

            try
            {
                metadata = await JsonSerializer.DeserializeAsync<FileTimelineMetadata>(stream, JsonSerializerOptions.Default, cancellationToken);
            }
            catch { }
        }

        metadata ??= new(Version, sourceFile, []);

        return new MetadataInfo(sourceFile, identifier, metadata, historyFolderPath);
    }

    private async Task SaveMetadataAsync(MetadataInfo metadataInfo, CancellationToken cancellationToken)
    {
        var historyFolderPath = metadataInfo.HistoryFolderPath;
        DirectoryUtil.Ensure(historyFolderPath);
        var entriesFilePath = GetMetadataFilePath(historyFolderPath);

        using var stream = File.Open(entriesFilePath, FileMode.OpenOrCreate);

        stream.SeekToBegin();
        stream.SetLength(0);

        await JsonSerializer.SerializeAsync(stream, metadataInfo.Metadata, JsonSerializerOptions.Default, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    #endregion Private 方法

    private record MetadataInfo(string SourceFile, string Identifier, FileTimelineMetadata Metadata, string HistoryFolderPath);
}
