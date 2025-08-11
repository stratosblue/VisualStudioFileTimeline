using System.Text;

namespace System.Extensions;

/// <summary>
/// <see cref="string"/> 拓展
/// </summary>
public static class StringEnhanceExtensions
{
    #region Compare

    /// <summary>
    /// 字符忽略大小写比较相等
    /// </summary>
    /// <param name="value"></param>
    /// <param name="compareValue"></param>
    /// <returns></returns>
    public static bool EqualsIgnoreCase(this string? value, string? compareValue) => string.Equals(value, compareValue, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 字符完全比较相等
    /// </summary>
    /// <param name="value"></param>
    /// <param name="compareValue"></param>
    /// <returns></returns>
    public static bool EqualsOrdinal(this string? value, string? compareValue) => string.Equals(value, compareValue, StringComparison.Ordinal);

    /// <summary>
    ///
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsNotNullOrEmpty(this string? value) => !string.IsNullOrEmpty(value);

    /// <summary>
    ///
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsNotNullOrWhiteSpace(this string? value) => !string.IsNullOrWhiteSpace(value);

    /// <summary>
    ///
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsNullOrEmpty(this string? value) => string.IsNullOrEmpty(value);

    /// <summary>
    ///
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsNullOrWhiteSpace(this string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// 字符忽略大小写比较不相等
    /// </summary>
    /// <param name="value"></param>
    /// <param name="compareValue"></param>
    /// <returns></returns>
    public static bool NotEqualsIgnoreCase(this string? value, string? compareValue) => value.NotEquals(compareValue, StringComparison.OrdinalIgnoreCase);

    #region Base

    /// <summary>
    /// 判断 <paramref name="value"/> 和 <paramref name="compareValue"/> 是否相等
    /// </summary>
    /// <param name="value"></param>
    /// <param name="compareValue"></param>
    /// <param name="comparisonType"></param>
    /// <returns></returns>
    public static bool NotEquals(this string? value, string? compareValue, StringComparison comparisonType = StringComparison.Ordinal) => !string.Equals(value, compareValue, comparisonType);

    #endregion Base

    #endregion Compare

    #region Convert

    /// <inheritdoc cref="GetBytes(ReadOnlySpan{char}, Encoding?)"/>
    public static ReadOnlySpan<byte> GetBytes(this string value, Encoding? encoding = null)
    {
        return value.AsSpan().GetBytes(encoding);
    }

    /// <summary>
    /// 获取 <paramref name="value"/> 以 <paramref name="encoding"/> 编码后的数据
    /// </summary>
    /// <param name="value"></param>
    /// <param name="encoding">默认为 <see cref="Encoding.UTF8"/></param>
    /// <returns></returns>
    public static ReadOnlySpan<byte> GetBytes(this ReadOnlySpan<char> value, Encoding? encoding = null)
    {
        if (value.IsEmpty)
        {
            return [];
        }

        encoding ??= Encoding.UTF8;

        var bufferSize = encoding.GetMaxByteCount(value.Length);
        var buffer = new byte[bufferSize];

        var length = encoding.GetBytes(value.ToArray(), 0, value.Length, buffer, 0);

        return buffer.AsSpan(0, length);
    }

    #endregion Convert
}
