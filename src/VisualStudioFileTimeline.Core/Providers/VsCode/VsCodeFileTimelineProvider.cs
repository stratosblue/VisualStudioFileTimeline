using System.IO.Extensions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Dynamic;
using VisualStudioFileTimeline.Utils;

namespace VisualStudioFileTimeline.Providers.VsCode;

public class VsCodeFileTimelineProvider : IFileTimelineProvider, IFileTimelineStore
{
    #region Public 字段

    public const string EntriesFileName = "entries.json";

    #endregion Public 字段

    #region Public 属性

    public string? Description { get; } = "vscode本地历史记录兼容";

    public string Name { get; } = "vscodeLocalHistory";

    public string VsCodeLocalHistoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Code", "User", "History");

    #endregion Public 属性

    #region Public 方法

    public static string CreateFileTimelineItemTitleBySource(string? source)
    {
        return source switch
        {
            "searchReplace.source" => Resources.TimelineItemSource_SearchReplace,
            _ => Resources.TimelineItemSource_SavedFile,
        };
    }

    /// <inheritdoc/>
    public async Task<IFileTimelineItem> AddHistoryAsync(FileHistoryDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        var resource = descriptor.Resource;
        var historyFolderPath = GetVsCodeHistoryFolderPath(resource);
        DirectoryUtil.Ensure(historyFolderPath);

        var entriesFilePath = GetEntriesFilePath(historyFolderPath);

        using var stream = File.Open(entriesFilePath, FileMode.OpenOrCreate);

        string rawJson;
        if (stream.Length == 0) //新建
        {
            rawJson =
                $$"""
                {"version":1,"resource":"{{resource.ToVsCodeCompatiblePath()}},"entries":[]"}
                """;
        }
        else    //追加
        {
            rawJson = await stream.ReadAsStringAsync(cancellationToken);
        }

        var json = JSON.parse(rawJson)!;

        if (json.version != 1)
        {
            throw new InvalidOperationException($"Version \"{json.version}\" content is not yet supported.");
        }

        var entries = json.entries ??= JSON.parse("[]")!;

        var fileName = $"{VsCodeCompatibleUtil.RandomFileName(4)}{Path.GetExtension(resource.AbsolutePath)}";
        var filePath = Path.Combine(historyFolderPath, fileName);
        var timestamp = descriptor.Time.ToUnixTimeMilliseconds();

        var newEntry = JSON.parse($$"""{"id":"{{fileName}}","timestamp":{{timestamp}}}""")!;

        if (!string.IsNullOrWhiteSpace(descriptor.Source))
        {
            newEntry.source = descriptor.Source;
        }

        //TODO 清理，合并
        entries.add(newEntry);

        File.Copy(resource.AbsolutePath, filePath, true);

        stream.SeekToBegin();
        stream.SetLength(0);

        byte[] jsonData = Encoding.UTF8.GetBytes(JSON.stringify(json));
        await stream.WriteAsync(jsonData, 0, jsonData.Length, cancellationToken);
        await stream.FlushAsync(CancellationToken.None);

        return new DefaultFileTimelineItem(Title: CreateFileTimelineItemTitleBySource(descriptor.Source),
                                           Description: null,
                                           FilePath: filePath,
                                           Time: descriptor.Time,
                                           Provider: this);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IFileTimelineItem>> GetAsync(Uri resource, CancellationToken cancellationToken = default)
    {
        if (!TryOpenEntryFileStream(resource, out var historyFolderPath, out var entriesFileStream))
        {
            var allFiles = GetAllFiles(resource, historyFolderPath);
            return allFiles.Select(m =>
            {
                return new DefaultFileTimelineItem(Title: CreateFileTimelineItemTitleBySource(string.Empty),
                                                   Description: null,
                                                   FilePath: m.FullName,
                                                   Time: m.CreationTime,
                                                   Provider: this);
            });
        }
        else
        {
            using var _ = entriesFileStream;

            var allFiles = GetAllFiles(resource, historyFolderPath);

            if (allFiles.Count == 0)
            {
                return [];
            }

            var jsonDocument = await JsonDocument.ParseAsync(entriesFileStream!, options: default, cancellationToken: cancellationToken);

            Dictionary<string, (string? Source, long Timestamp)> entryInfoMap = [];

            //TODO 判断路径
            if (jsonDocument.RootElement.TryGetProperty("entries", out var entries)
                && entries.ValueKind == JsonValueKind.Array)
            {
                foreach (var jsonElement in entries.EnumerateArray())
                {
                    if (jsonElement.TryGetProperty("id", out var fileNameNode)
                        && fileNameNode.GetString() is string fileName
                        && jsonElement.TryGetProperty("timestamp", out var timestampNode)
                        && timestampNode.TryGetInt64(out var timestamp))
                    {
                        var source = jsonElement.TryGetProperty("source", out var sourceNode)
                                     ? sourceNode.GetRawText()
                                     : null;
                        entryInfoMap.Add(fileName, (source, timestamp));
                    }
                }
            }

            return allFiles.Select(m =>
            {
                string? title = null;
                DateTime? time = null;
                if (entryInfoMap.TryGetValue(Path.GetFileName(m.FullName), out var entryInfo) == true)
                {
                    title = CreateFileTimelineItemTitleBySource(entryInfo.Source);
                    time = DateTimeExtensions.FromUnixTimeMilliseconds(entryInfo.Timestamp);
                }
                return new DefaultFileTimelineItem(Title: title ?? CreateFileTimelineItemTitleBySource(null),
                                                   Description: null,
                                                   FilePath: m.FullName,
                                                   Time: time ?? m.CreationTime,
                                                   Provider: this);
            })!;
        }

        List<FileInfo> GetAllFiles(Uri resource, string historyFolderPath)
        {
            if (!Directory.Exists(historyFolderPath))
            {
                return [];
            }
            //没有条目文件
            return EnumerateAllHistoryFiles(historyFolderPath, Path.GetExtension(resource.AbsolutePath));
        }
    }

    /// <inheritdoc/>
    public override string ToString() => Name;

    /// <inheritdoc/>
    public Task<bool> TryDropAsync(IFileTimelineItem item, CancellationToken cancellationToken = default)
    {
        //TODO 完成删除
        return Task.FromResult(false);
    }

    #endregion Public 方法

    #region Private 方法

    private static string GetEntriesFilePath(string historyFolderPath)
    {
        return Path.Combine(historyFolderPath, EntriesFileName);
    }

    private List<FileInfo> EnumerateAllHistoryFiles(string historyFolderPath, string extension)
    {
        var directoryInfo = new DirectoryInfo(historyFolderPath);
        return directoryInfo.EnumerateFiles($"????{extension}", SearchOption.TopDirectoryOnly)
                            .OrderByDescending(m => m.CreationTime)
                            .ToList();
    }

    private string GetVsCodeHistoryFolderPath(Uri resource)
    {
        var historyFolderName = VsCodeCompatibleUtil.HashToHexString(resource.ToVsCodeCompatiblePath());
        return Path.Combine(VsCodeLocalHistoryPath, historyFolderName);
    }

    private bool TryOpenEntryFileStream(Uri resource,
                                        out string historyFolderPath,
                                        out FileStream? entriesFileStream)
    {
        historyFolderPath = GetVsCodeHistoryFolderPath(resource);
        entriesFileStream = default;

        if (!Directory.Exists(VsCodeLocalHistoryPath))
        {
            return false;
        }

        var entriesFilePath = GetEntriesFilePath(historyFolderPath);

        if (!File.Exists(entriesFilePath))
        {
            return false;
        }
        entriesFileStream = File.OpenRead(entriesFilePath);
        return true;
    }

    #endregion Private 方法
}
