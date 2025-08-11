namespace System.Extensions;

/// <summary>
/// <see cref="Guid"/> 拓展方法
/// </summary>
public static class GuidEnhanceExtensions
{
    /// <summary>
    /// 转化为十六进制字符串
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    public static string ToHexString(this in Guid guid) => guid.ToString("N");

    /// <summary>
    /// 转化为大写十六进制字符串
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    public static string ToUpperHexString(this in Guid guid) => guid.ToString("N").ToUpperInvariant();
}
