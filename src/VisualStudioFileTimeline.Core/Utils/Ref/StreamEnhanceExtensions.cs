namespace System.IO.Extensions;

/// <summary>
/// <see cref="Stream"/> 增强拓展方法
/// </summary>
public static class StreamEnhanceExtensions
{
    /// <summary>
    /// 将 <paramref name="target"/> 的读取位置移动到起始位置
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    public static Stream SeekToBegin(this Stream target)
    {
        target.Seek(0, SeekOrigin.Begin);
        return target;
    }
}
