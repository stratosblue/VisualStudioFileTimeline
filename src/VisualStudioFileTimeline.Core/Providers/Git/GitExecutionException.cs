using System.Runtime.Serialization;

namespace VisualStudioFileTimeline.Providers.Git;

public class GitExecutionException : VisualStudioFileTimelineException
{
    #region Public 属性

    public int ExitCode { get; }

    #endregion Public 属性

    #region Public 构造函数

    public GitExecutionException(int exitCode) : base()
    {
        ExitCode = exitCode;
    }

    public GitExecutionException(string message, int exitCode) : base(message)
    {
        ExitCode = exitCode;
    }

    public GitExecutionException(string message, Exception innerException, int exitCode) : base(message, innerException)
    {
        ExitCode = exitCode;
    }

    #endregion Public 构造函数

    #region Protected 构造函数

    protected GitExecutionException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    #endregion Protected 构造函数
}
