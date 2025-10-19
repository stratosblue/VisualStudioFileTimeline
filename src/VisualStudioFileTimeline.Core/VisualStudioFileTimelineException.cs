using System.Runtime.Serialization;

namespace VisualStudioFileTimeline;

public class VisualStudioFileTimelineException : Exception
{
    #region Public 构造函数

    public VisualStudioFileTimelineException()
    {
    }

    public VisualStudioFileTimelineException(string message) : base(message)
    {
    }

    public VisualStudioFileTimelineException(string message, Exception innerException) : base(message, innerException)
    {
    }

    #endregion Public 构造函数

    #region Protected 构造函数

    protected VisualStudioFileTimelineException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    #endregion Protected 构造函数
}
