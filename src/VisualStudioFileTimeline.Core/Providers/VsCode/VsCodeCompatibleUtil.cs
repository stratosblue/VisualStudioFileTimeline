using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace VisualStudioFileTimeline.Providers.VsCode;

public static class VsCodeCompatibleUtil
{
    #region Compatible

    private static readonly ReadOnlyMemory<char> s_hexChars = "0123456789abcdef".ToCharArray();

    private static readonly ReadOnlyMemory<char> s_windowsSafePathFirstChars = "BDEFGHIJKMOQRSTUVWXYZbdefghijkmoqrstuvwxyz0123456789".ToCharArray();

    public static string HashToHexString(string value)
    {
        var hash = Hash(value);
        var isNegative = false;
        if (hash < 0)
        {
            isNegative = true;
            hash = Math.Abs(hash);
        }

        Span<byte> data = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(data, hash);

        Span<char> hexBuffer = stackalloc char[8];
        ToHexString(data, hexBuffer);

        var trimZeroIndex = 0;
        while (hexBuffer[trimZeroIndex] == '0')
        {
            trimZeroIndex++;
        }

        var stringLength = 9 - trimZeroIndex;
        Span<char> stringBuffer = stackalloc char[stringLength];

        if (isNegative)
        {
            stringBuffer[0] = '-';
            hexBuffer.Slice(trimZeroIndex).CopyTo(stringBuffer.Slice(1));
        }
        else
        {
            stringLength--;
            hexBuffer.Slice(trimZeroIndex).CopyTo(stringBuffer);
        }

        return stringBuffer.Slice(0, stringLength).ToString();

        static void ToHexString(Span<byte> data, Span<char> buffer)
        {
            var hexChars = s_hexChars.Span;
            for (int i = 0; i < data.Length; i++)
            {
                var b = data[i];
                buffer[i * 2] = hexChars[b >> 4];
                buffer[i * 2 + 1] = hexChars[b & 0x0F];
            }
        }
    }

    public static string RandomFileName(int length)
    {
        var windowsSafePathFirstChars = s_windowsSafePathFirstChars.Span;
        var maxValue = windowsSafePathFirstChars.Length;
        Span<char> stringBuffer = stackalloc char[length];
        var random = new Random();
        for (var i = 0; i < length; i++)
        {
            stringBuffer[i] = windowsSafePathFirstChars[random.Next(maxValue)];
        }

        return stringBuffer.ToString();
    }

    #endregion Compatible

    #region Variant

    public static int Hash(string s) => StringHash(s, 0);

    #endregion Variant

    #region Raw

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NumberHash(int val, int initialHashVal)
    {
        return (initialHashVal << 5) - initialHashVal + val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int StringHash(string s, int hashVal)
    {
        hashVal = NumberHash(149417, hashVal);
        foreach (char c in s.AsSpan())
        {
            hashVal = NumberHash(c, hashVal);
        }
        return hashVal;
    }

    #endregion Raw
}
