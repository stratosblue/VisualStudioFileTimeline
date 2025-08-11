using System.Text;

namespace System.IO.Extensions;

/// <summary>
/// <see cref="Stream"/> 便捷读取拓展方法
/// </summary>
public static class StreamConvenientReadExtensions
{
    /// <summary>
    /// 将 <paramref name="source"/> 读取为 <see cref="byte"/> 数组 (性能敏感场景应当合理评估)
    /// </summary>
    /// <param name="source"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<byte[]> ReadAsByteArrayAsync(this Stream source, CancellationToken cancellationToken = default)
    {
        using var memoryStream = await source.ReadAsMemoryStreamAsync(cancellationToken);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// 将 <paramref name="source"/> 读取为 <see cref="MemoryStream"/> (性能敏感场景应当合理评估)
    /// </summary>
    /// <param name="source"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<MemoryStream> ReadAsMemoryStreamAsync(this Stream source, CancellationToken cancellationToken = default)
    {
        var capacity = 0;

        try
        {
            //尝试获取 capacity
            if (source.CanRead
                && source.Length is { } length
                && length < int.MaxValue)
            {
                capacity = (int)length;
            }
        }
        catch { }

        var memoryStream = new MemoryStream(capacity);
        try
        {
            await source.CopyToAsync(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }
        catch
        {
            memoryStream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 将 <paramref name="source"/> 读取为 <see cref="MemoryStream"/> (性能敏感场景应当合理评估)
    /// </summary>
    /// <param name="source"></param>
    /// <param name="memoryStreamFactory"><see cref="MemoryStream"/> 的创建委托</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<MemoryStream> ReadAsMemoryStreamAsync(this Stream source, Func<Stream, MemoryStream> memoryStreamFactory, CancellationToken cancellationToken = default)
    {
        var memoryStream = memoryStreamFactory(source);
        try
        {
            await source.CopyToAsync(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }
        catch
        {
            memoryStream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 将 <paramref name="source"/> 使用 <see cref="Encoding.UTF8"/> 读取为 <see cref="string"/> (性能敏感场景应当合理评估)
    /// </summary>
    /// <param name="source"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<string> ReadAsStringAsync(this Stream source, CancellationToken cancellationToken = default)
    {
        return source.ReadAsStringAsync(Encoding.UTF8, cancellationToken);
    }

    /// <summary>
    /// 将 <paramref name="source"/> 读取为 <see cref="string"/> (性能敏感场景应当合理评估)
    /// </summary>
    /// <param name="source"></param>
    /// <param name="encoding">使用的字符串编码</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<string> ReadAsStringAsync(this Stream source, Encoding encoding, CancellationToken cancellationToken = default)
    {
        using var streamReader = new StreamReader(source, encoding, true, 4096, leaveOpen: true);
        return await streamReader.ReadToEndAsync();
    }
}
