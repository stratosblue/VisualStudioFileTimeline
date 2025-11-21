namespace VisualStudioFileTimeline.Providers.Git;

public readonly record struct GitCommitInfo(string CommitId,
                                            string Author,
                                            string AuthorEmail,
                                            long AuthorTimestamp,
                                            string Committer,
                                            string CommitterEmail,
                                            long CommitterTimestamp,
                                            string Body);

/// <summary>
/// 使用提交者信息和提交message确定唯一提交，用于去重提交被修改的情况（暂不知道这样处理的对不对）
/// </summary>
public sealed class GitCommitInfoEqualityComparer : IEqualityComparer<GitCommitInfo>
{
    #region Public 属性

    public static GitCommitInfoEqualityComparer Shared { get; } = new();

    #endregion Public 属性

    #region Public 方法

    public bool Equals(GitCommitInfo x, GitCommitInfo y)
    {
        return x.Author == y.Author
               && x.AuthorEmail == y.AuthorEmail
               && x.AuthorTimestamp == y.AuthorTimestamp
               && x.Body == y.Body;
    }

    public int GetHashCode(GitCommitInfo obj)
    {
        return obj.Author.GetHashCode()
               ^ obj.AuthorEmail.GetHashCode()
               ^ obj.AuthorTimestamp.GetHashCode()
               ^ obj.Body.GetHashCode();
    }

    #endregion Public 方法
}
