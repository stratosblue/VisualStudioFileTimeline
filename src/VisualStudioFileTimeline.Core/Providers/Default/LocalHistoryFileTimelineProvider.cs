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
        var historyFolderPath = GetHistoryFolderPath(sourceFile);
        DirectoryUtil.Ensure(historyFolderPath);

        var entriesFilePath = GetMetadataFilePath(historyFolderPath);

        using var stream = File.Open(entriesFilePath, FileMode.OpenOrCreate);

        FileTimelineMetadata? metadata = null;

        try
        {
            metadata = await JsonSerializer.DeserializeAsync<FileTimelineMetadata>(stream, JsonSerializerOptions.Default, cancellationToken);
        }
        catch { }

        metadata ??= new(Version, sourceFile, []);
        var currentTimestamp = descriptor.Time.ToUnixTimeMilliseconds();
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

        stream.SeekToBegin();
        stream.SetLength(0);

        await JsonSerializer.SerializeAsync(stream, metadata, JsonSerializerOptions.Default, CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);

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
        if (!TryOpenMetadataFileStream(sourceFile, out var historyFolderPath, out var metadataFileStream))
        {
            //没有元数据文件
            var allFiles = GetAllFiles(sourceFile, historyFolderPath);

            if (allFiles.Count == 0)
            {
                return [];
            }

            DirectoryUtil.Ensure(historyFolderPath);

            //创建新的
            var entries = allFiles.ToDictionary(m => m.FullName, m => new FileTimelineMetadataEntryInfo(null) { Timestamp = m.CreationTime.ToUnixTimeMilliseconds() });
            var metadata = new FileTimelineMetadata(Version, historyFolderPath, entries);

            using var newMetadataFileStream = File.OpenWrite(GetMetadataFilePath(historyFolderPath));

            await JsonSerializer.SerializeAsync(newMetadataFileStream, metadata, JsonSerializerOptions.Default, cancellationToken);

            return allFiles.Select(m =>
            {
                return new DefaultFileTimelineItem(Title: CreateFileTimelineItemTitleBySource(null),
                                                   Description: null,
                                                   FilePath: m.FullName,
                                                   Time: m.CreationTime,
                                                   Provider: this);
            });
        }
        else
        {
            using var _ = metadataFileStream;

            var allFiles = GetAllFiles(sourceFile, historyFolderPath);

            if (allFiles.Count == 0)
            {
                return [];
            }

            var metadata = await JsonSerializer.DeserializeAsync<FileTimelineMetadata>(metadataFileStream!, JsonSerializerOptions.Default, cancellationToken);

            return allFiles.Select(m =>
            {
                string? title = null;
                DateTime? time = null;
                if (metadata?.Entries.TryGetValue(Path.GetFileName(m.FullName), out var entryInfo) == true)
                {
                    title = CreateFileTimelineItemTitleBySource(entryInfo.Source);
                    time = DateTimeExtensions.FromUnixTimeMilliseconds(entryInfo.Timestamp);
                }
                return new DefaultFileTimelineItem(Title: title ?? CreateFileTimelineItemTitleBySource(null),
                                                   Description: null,
                                                   FilePath: m.FullName,
                                                   Time: time ?? m.CreationTime,
                                                   Provider: this);
            });
        }

        List<FileInfo> GetAllFiles(string sourceFile, string historyFolderPath)
        {
            if (!Directory.Exists(historyFolderPath))
            {
                return [];
            }
            return EnumerateAllHistoryFiles(historyFolderPath, Path.GetExtension(sourceFile));
        }
    }

    public override string ToString() => Name;

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

    private string GetHistoryFolderPath(string path)
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

                var historyFolderName = SequenceFileNameUtil.Create(buffer);

                return Path.Combine(LocalHistoryPath, historyFolderName);
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

    private bool TryOpenMetadataFileStream(string sourceFile,
                                           out string historyFolderPath,
                                           out FileStream? metadataFileStream)
    {
        historyFolderPath = GetHistoryFolderPath(sourceFile);
        metadataFileStream = default;
        if (!Directory.Exists(LocalHistoryPath))
        {
            return false;
        }

        var metadataFilePath = GetMetadataFilePath(historyFolderPath);

        if (!File.Exists(metadataFilePath))
        {
            return false;
        }
        metadataFileStream = File.OpenRead(metadataFilePath);
        return true;
    }

    #endregion Private 方法
}
