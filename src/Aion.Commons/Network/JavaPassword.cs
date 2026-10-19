using System.Security.Cryptography;
using System.Text;

namespace Aion.Commons.Network;

/// <summary>Java String.getBytes(UTF_8) followed by a fixed-time byte comparison.</summary>
public static class JavaPassword
{
    private static readonly Encoding Utf8 = CreateEncoding();
    private static Encoding CreateEncoding()
    {
        var encoding = (Encoding)Encoding.UTF8.Clone();
        encoding.EncoderFallback = new EncoderReplacementFallback("?");
        return encoding;
    }
    public static bool EqualsUtf8(string expected, string received) =>
        CryptographicOperations.FixedTimeEquals(Utf8.GetBytes(expected), Utf8.GetBytes(received));
}
