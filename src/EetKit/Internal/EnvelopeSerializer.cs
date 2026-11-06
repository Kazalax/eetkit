using System.Globalization;
using System.Text;
using System.Xml;

namespace EetKit.Internal;

/// <summary>Turns the signed envelope into the bytes sent over HTTP.</summary>
internal static class EnvelopeSerializer
{
    /// <summary>
    /// Limit of the whole signed message. The documentation says 12 kB without defining the unit,
    /// the stricter reading is used. To be confirmed on the playground (error 7) in M2.
    /// </summary>
    public const int MaxMessageBytes = 12_000;

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>UTF-8 without BOM and XML declaration, exactly as built (never reformatted).</summary>
    /// <exception cref="EetValidationException">The message is larger than <see cref="MaxMessageBytes"/>.</exception>
    public static byte[] ToUtf8(XmlDocument envelope)
    {
        Guard.NotNull(envelope, nameof(envelope));

        var bytes = Utf8WithoutBom.GetBytes(envelope.OuterXml);
        if (bytes.Length > MaxMessageBytes)
        {
            throw new EetValidationException(
            [
                new EetValidationError(
                    "zprava",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "podepsaná zpráva má {0} bajtů, limit je {1}",
                        bytes.Length,
                        MaxMessageBytes)),
            ]);
        }

        return bytes;
    }
}
