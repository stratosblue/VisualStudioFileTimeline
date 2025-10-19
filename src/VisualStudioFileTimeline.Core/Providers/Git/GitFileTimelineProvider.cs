using System.Diagnostics;
using System.Text.RegularExpressions;

namespace VisualStudioFileTimeline.Providers.Git;

/// <summary>
/// 基于 git 的文件时间线提供器
/// </summary>
public class GitFileTimelineProvider : IFileTimelineProvider
{
    #region Private 字段

    private readonly string? _gitExecutableFilePath;

    #endregion Private 字段

    #region Public 属性

    /// <inheritdoc/>
    public string? Description { get; }

    /// <inheritdoc/>
    public string Name { get; } = "gitHistory";

    #endregion Public 属性

    #region Public 构造函数

    public GitFileTimelineProvider()
    {
        _gitExecutableFilePath = GetGitExecutableFilePath();
    }

    #endregion Public 构造函数

    #region Public 方法

    /// <inheritdoc/>
    public async Task<IEnumerable<IFileTimelineItem>> GetAsync(Uri resource, CancellationToken cancellationToken = default)
    {
        var filePath = resource.LocalPath.Replace('\\', '/');

        var fileDirectory = Path.GetDirectoryName(filePath);
        var rootDirectory = await ExecuteGitCommandAsync(fileDirectory, "rev-parse --show-toplevel", cancellationToken);

        rootDirectory = rootDirectory.Trim();

        var commitInfos = await GetGitCommitInfosAsync(rootDirectory, filePath, cancellationToken);

        if (commitInfos.Length > 0)
        {
            var relativePath = filePath.Substring(rootDirectory.Length + 1);

            var result = new List<IFileTimelineItem>();
            foreach (var commitInfo in commitInfos)
            {
                var item = new GitFileTimelineItem(RootDirectory: rootDirectory,
                                                   RelativeFilePath: relativePath,
                                                   CommitInfo: commitInfo,
                                                   Title: CreateTitle(commitInfo.Body),
                                                   Time: DateTimeOffset.FromUnixTimeSeconds(commitInfo.AuthorTimestamp).DateTime,
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

    #endregion Public 方法

    #region Private 方法

    private static readonly Regex s_commitMatchRegex = new("([a-zA-Z0-9]+)\\n(.+)\\n(.+)\\n(.+)\\n(.+)\\n(.+)\\n(.+)\\n([\\S\\s\\n]+?)\\n-~-~-~-\\n", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private async Task<GitCommitInfo[]> GetGitCommitInfosAsync(string rootDirectory, string filePath, CancellationToken cancellationToken)
    {
        ThrowIfGitNotFound();

        var gitOutput = await ExecuteGitCommandAsync(workingDirectory: rootDirectory,
                                                     command: $"--no-pager log --encoding=utf-8 --date=unix --follow --pretty=format:%H%n%an%n%ae%n%at%n%cn%n%ce%n%ct%n%B%n-~-~-~-%n --all \"{filePath}\"",
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

    #endregion Private 方法

    #region Internal 方法

    internal static async Task<string> ExecuteGitCommandAsync(string gitPath,
                                                              string workingDirectory,
                                                              string command,
                                                              CancellationToken cancellationToken)
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
        using var process = Process.Start(processStartInfo);

        var outputReadTask = process.StandardOutput.ReadToEndAsync();
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

        var output = await outputReadTask;
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
                                                 CancellationToken cancellationToken)
    {
        return ExecuteGitCommandAsync(gitPath: _gitExecutableFilePath!,
                                      workingDirectory: workingDirectory,
                                      command: command,
                                      cancellationToken: cancellationToken);
    }

    #endregion Internal 方法
}
