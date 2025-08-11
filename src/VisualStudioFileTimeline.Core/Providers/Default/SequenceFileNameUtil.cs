using System.Buffers.Binary;

namespace VisualStudioFileTimeline.Providers.Default;

public static class SequenceFileNameUtil
{
    #region Public 字段

    public const string NameChars = "abcdefghijklmnopqrstuvwxyz0123456789";

    #endregion Public 字段

    #region Public 方法

    public static string Create(Span<byte> bytes)
    {
        return System.Buffers.BaseAnyEncoding.EncodeToString(bytes, NameChars.AsSpan());
    }

    public static string GenerateName(string extension) => $"{GenerateName(DateTimeOffset.UtcNow)}{extension}";

    public static string GenerateName() => GenerateName(DateTimeOffset.UtcNow);

    public static string GenerateName(DateTimeOffset time)
    {
        var milliseconds = time.ToUnixTimeMilliseconds();
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, milliseconds);
        return Create(bytes.Slice(2));
    }

    #endregion Public 方法
}
