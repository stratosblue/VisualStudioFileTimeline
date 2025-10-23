using System.Buffers;
using System.Diagnostics;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using VisualStudioFileTimeline.Providers.Default;

namespace VisualStudioFileTimeline.Providers.Git;

/// <summary>
/// 基于 git 的文件时间线提供器
/// </summary>
public class GitFileTimelineProvider(VisualStudioFileTimelineOptions options, ILogger<GitFileTimelineProvider> logger)
    : IFileTimelineProvider, IDisposable
{
    #region Private 字段

    private readonly string? _gitExecutableFilePath = GetGitExecutableFilePath();

    #endregion Private 字段

    #region Public 属性

    /// <inheritdoc/>
    public string? Description { get; }

    /// <inheritdoc/>
    public string Name { get; } = "gitHistory";

    public string TemporaryDirectory { get; } = options.EnsureTemporaryDirectory("git_temp");

    #endregion Public 属性

    #region Public 方法

    /// <inheritdoc/>
    public async Task<IEnumerable<IFileTimelineItem>> GetAsync(Uri resource, CancellationToken cancellationToken = default)
    {
        var filePath = resource.LocalPath.Replace('\\', '/');

        var fileDirectory = Path.GetDirectoryName(filePath);
        var rootDirectory = await ExecuteGitCommandAsync(fileDirectory, "rev-parse --show-toplevel", null, cancellationToken);

        rootDirectory = rootDirectory.Trim();

        var commitInfos = await GetGitCommitInfosAsync(rootDirectory, filePath, cancellationToken);

        if (commitInfos.Length > 0)
        {
            var relativePath = filePath.Substring(rootDirectory.Length + 1);
            var fileName = Path.GetFileName(relativePath);
            var fileIdentifier = GetFileIdentifier(filePath);

            var result = new List<IFileTimelineItem>();
            foreach (var commitInfo in commitInfos)
            {
                var item = new GitFileTimelineItem(SourceFilePath: resource.LocalPath,
                                                   RootDirectory: rootDirectory,
                                                   RelativeFilePath: relativePath,
                                                   FileName: fileName,
                                                   FileIdentifier: fileIdentifier,
                                                   CommitInfo: commitInfo,
                                                   Title: CreateTitle(commitInfo.Body),
                                                   Time: DateTimeOffset.FromUnixTimeSeconds(commitInfo.AuthorTimestamp).ToLocalTime().DateTime,
                                                   Provider: this);
                result.Add(item);
            }
            return result;
        }
        return [];

        static string CreateTitle(string body)
        {
            const int TitleMaxLength = 50;
            if (body.Length > TitleMaxLength)
            {
                return $"{body.Substring(0, TitleMaxLength).Replace('\n', ' ').Replace('\r', ' ')}...";
            }
            return body.Replace('\n', ' ').Replace('\r', ' ');
        }
    }

    /// <inheritdoc/>
    public override string ToString() => Name;

    /// <inheritdoc/>
    public Task<bool> TryDropAsync(IFileTimelineItem item, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    #region Dispose

    public void Dispose()
    {
        try
        {
            //每次退出时清理几个文件
            foreach (var item in Directory.EnumerateFiles(TemporaryDirectory).Take(5))
            {
                try
                {
                    File.Delete(item);
                }
                catch
                {
                    if (File.Exists(item))
                    {
                        throw;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cleanup git temp files failed at dispose.");
        }
    }

    #endregion Dispose

    #endregion Public 方法

    #region Private 方法

    private static readonly Regex s_commitMatchRegex = new("([a-zA-Z0-9]+)\\n(.+)\\n(.+)\\n(.+)\\n(.+)\\n(.+)\\n(.+)\\n([\\S\\s\\n]+?)\\n-~-~-~-\\n", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private async Task<GitCommitInfo[]> GetGitCommitInfosAsync(string rootDirectory, string filePath, CancellationToken cancellationToken)
    {
        ThrowIfGitNotFound();

        var gitOutput = await ExecuteGitCommandAsync(workingDirectory: rootDirectory,
                                                     command: $"--no-pager log --encoding=utf-8 --date=unix --follow --pretty=format:%H%n%an%n%ae%n%at%n%cn%n%ce%n%ct%n%B%n-~-~-~-%n --all \"{filePath}\"",
                                                     encoding: Encoding.UTF8,
                                                     cancellationToken: cancellationToken);

        var matches = s_commitMatchRegex.Matches(gitOutput);
        if (matches.Count == 0)
        {
            return [];
        }

        var result = new GitCommitInfo[matches.Count];
        var index = 0;
        foreach (Match match in matches)
        {
            var groups = match.Groups;
            result[index++] = new GitCommitInfo(CommitId: groups[1].Value,
                                                Author: groups[2].Value,
                                                AuthorEmail: groups[3].Value,
                                                AuthorTimestamp: long.Parse(groups[4].Value),
                                                Committer: groups[5].Value,
                                                CommitterEmail: groups[6].Value,
                                                CommitterTimestamp: long.Parse(groups[7].Value),
                                                Body: groups[8].Value.Trim());
        }

        return result;
    }

    private void ThrowIfGitNotFound()
    {
        if (_gitExecutableFilePath is null)
        {
            throw new GitExecutionException("Not found git.", -1);
        }
    }

    #region PathHash

    private static readonly ConditionalWeakTable<string, string> s_pathIdentifierCache = new();

    private static string GetFileIdentifier(string fullPath)
    {
        if (s_pathIdentifierCache.TryGetValue(fullPath, out var value))
        {
            return value;
        }

        var spanMemory = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(fullPath.Length));
        try
        {
            //这里多少有点冗余操作
            var pathBytesLength = Encoding.UTF8.GetBytes(fullPath, 0, fullPath.Length, spanMemory, 0);

            Span<byte> buffer = stackalloc byte[sizeof(long)];
            var span = spanMemory.AsSpan();
            Crc32.Hash(span.Slice(0, pathBytesLength), buffer);

            value = SequenceFileNameUtil.Create(buffer);

            try
            {
                s_pathIdentifierCache.Add(fullPath, value);
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

    #endregion Private 方法

    #region Internal 方法

    internal static async Task<string> ExecuteGitCommandAsync(string gitPath,
                                                              string workingDirectory,
                                                              string command,
                                                              Encoding? encoding = null,
                                                              CancellationToken cancellationToken = default)
    {
        var processStartInfo = new ProcessStartInfo(gitPath)
        {
            UseShellExecute = false,
            Arguments = command,
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = false,
            CreateNoWindow = true,
        };

        if (encoding is not null)
        {
            processStartInfo.StandardOutputEncoding = encoding;
            processStartInfo.StandardErrorEncoding = encoding;
        }

        using var process = Process.Start(processStartInfo);

        using var memoryStream = new MemoryStream(8 * 1024);
        var outputReadTask = process.StandardOutput.BaseStream.CopyToAsync(memoryStream, 4096, cancellationToken);
        outputReadTask.Wait(cancellationToken);

        using var localCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await Task.WhenAny(outputReadTask, Task.Delay(Timeout.Infinite, localCts.Token));
        }
        finally
        {
            try
            {
                localCts.Cancel();
            }
            catch { }
        }

        cancellationToken.ThrowIfCancellationRequested();

        await outputReadTask;
        if (!memoryStream.TryGetBuffer(out var buffer))
        {
            throw new InvalidOperationException("Can not get git output data.");
        }
        var output = (encoding ?? Encoding.UTF8).GetString(buffer.Array, buffer.Offset, buffer.Count);
        var exitCode = process.ExitCode;

        if (exitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new GitExecutionException($"Git execution error occurred: {exitCode} - \"{error}\"", exitCode);
        }
        return output;
    }

    internal static string? GetGitExecutableFilePath()
    {
        var paths = Environment.GetEnvironmentVariable("PATH");
        //windows only
        foreach (var path in paths.Split(';'))
        {
            var gitExecutableFilePath = Path.Combine(path, "git.exe");
            if (File.Exists(gitExecutableFilePath))
            {
                return gitExecutableFilePath;
            }
        }
        return null;
    }

    internal Task<string> ExecuteGitCommandAsync(string workingDirectory,
                                                 string command,
                                                 Encoding? encoding = null,
                                                 CancellationToken cancellationToken = default)
    {
        return ExecuteGitCommandAsync(gitPath: _gitExecutableFilePath!,
                                      workingDirectory: workingDirectory,
                                      command: command,
                                      encoding: encoding,
                                      cancellationToken: cancellationToken);
    }

    #endregion Internal 方法
}
