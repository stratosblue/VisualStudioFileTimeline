using System.Buffers;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Text;
using VisualStudioFileTimeline.Providers.Default;
using VisualStudioFileTimeline.Providers.Git;

namespace VisualStudioFileTimeline;

public record GitFileTimelineItem(string SourceFilePath,
                                  string RootDirectory,
                                  string RelativeFilePath,
                                  GitCommitInfo CommitInfo,
                                  string Title,
                                  DateTime Time,
                                  GitFileTimelineProvider Provider)
    : IFileTimelineItem
{
    /// <inheritdoc/>
    public string Identifier => CommitInfo.CommitId;

    /// <inheritdoc/>
    public string? Description => CommitInfo.Body;

    /// <inheritdoc/>
    public string FilePath { get; } = Path.Combine(Path.GetTempPath(), $"{GetPathHash(SourceFilePath, RelativeFilePath)}_{CommitInfo.CommitId}");

    /// <inheritdoc/>
    public bool IsReadOnly => true;

    IFileTimelineProvider IFileTimelineItem.Provider => Provider;

    /// <inheritdoc/>
    public string? SourceControlName => "git";

    public async Task LoadAsTempFileAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(FilePath))
        {
            return;
        }
        var content = await Provider.ExecuteGitCommandAsync(RootDirectory, $"show {CommitInfo.CommitId}:\"{RelativeFilePath}\"", cancellationToken);
        File.WriteAllText(FilePath, content);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[{Time}] {Title}({CommitInfo.CommitId})";
    }

    #region PathHash

    private static readonly ConditionalWeakTable<string, string> s_pathHashCache = new();

    private static string GetPathHash(string fullPath, string path)
    {
        if (s_pathHashCache.TryGetValue(fullPath, out var value))
        {
            return value;
        }

        var spanMemory = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(Math.Max(fullPath.Length, path.Length)));
        try
        {
            var pathBytesLength = Encoding.UTF8.GetBytes(fullPath, 0, fullPath.Length, spanMemory, 0);

            Span<byte> buffer = stackalloc byte[sizeof(long) + sizeof(long)];
            var span = spanMemory.AsSpan();
            Crc64.Hash(span.Slice(0, pathBytesLength), buffer);

            pathBytesLength = Encoding.UTF8.GetBytes(path, 0, path.Length, spanMemory, 0);

            Crc64.Hash(span.Slice(0, pathBytesLength), buffer.Slice(sizeof(long)));
            value = SequenceFileNameUtil.Create(buffer);

            try
            {
                s_pathHashCache.Add(fullPath, value);
            }
            catch { }

            return value;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(spanMemory);
        }
    }

    #endregion PathHash
};
