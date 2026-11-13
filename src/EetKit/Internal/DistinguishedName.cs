using System.Text;

namespace EetKit.Internal;

/// <summary>
/// Minimal DER reader for an X.509 Name: SEQUENCE OF SET OF SEQUENCE { OID, value }.
/// Only what the trust check needs; netstandard2.0 has no ASN.1 reader of its own.
/// </summary>
internal static class DistinguishedName
{
    private const byte SequenceTag = 0x30;
    private const byte SetTag = 0x31;
    private const byte OidTag = 0x06;
    private const byte Utf8StringTag = 0x0C;
    private const byte PrintableStringTag = 0x13;
    private const byte Ia5StringTag = 0x16;
    private const byte BmpStringTag = 0x1E;
    private const byte LongFormFlag = 0x80;
    private const int MaxLengthBytes = 3;

    // 2.5.4.10 id-at-organizationName
    private static readonly byte[] OrganizationOid = [0x55, 0x04, 0x0A];

    /// <exception cref="FormatException">The data is not a well-formed Name.</exception>
    public static IReadOnlyList<string> Organizations(byte[] der)
    {
        var organizations = new List<string>();
        var (position, end) = Content(der, 0, SequenceTag);
        while (position < end)
        {
            var (setPosition, setEnd) = Content(der, position, SetTag);
            while (setPosition < setEnd)
            {
                var (attributePosition, attributeEnd) = Content(der, setPosition, SequenceTag);
                var (oidStart, oidEnd) = Content(der, attributePosition, OidTag);
                var valueTag = At(der, oidEnd);
                var (valueStart, valueEnd) = Content(der, oidEnd, valueTag);
                if (SameBytes(der, oidStart, oidEnd, OrganizationOid))
                {
                    organizations.Add(Decode(der, valueTag, valueStart, valueEnd));
                }

                if (valueEnd != attributeEnd)
                {
                    throw new FormatException("Unexpected data in a name attribute.");
                }

                setPosition = attributeEnd;
            }

            position = setEnd;
        }

        return organizations;
    }

    private static (int Start, int End) Content(byte[] der, int position, byte expectedTag)
    {
        if (At(der, position) != expectedTag)
        {
            throw new FormatException("Unexpected DER tag.");
        }

        var index = position + 1;
        int length = At(der, index++);
        if ((length & LongFormFlag) != 0)
        {
            var lengthBytes = length & ~LongFormFlag;
            if (lengthBytes is 0 or > MaxLengthBytes)
            {
                throw new FormatException("Unsupported DER length.");
            }

            length = 0;
            for (var i = 0; i < lengthBytes; i++)
            {
                length = (length << 8) | At(der, index++);
            }
        }

        if (length > der.Length - index)
        {
            throw new FormatException("DER length beyond the data.");
        }

        return (index, index + length);
    }

    private static byte At(byte[] der, int index) =>
        index >= 0 && index < der.Length ? der[index] : throw new FormatException("Truncated DER data.");

    private static bool SameBytes(byte[] der, int start, int end, byte[] expected)
    {
        if (end - start != expected.Length)
        {
            return false;
        }

        for (var i = 0; i < expected.Length; i++)
        {
            if (der[start + i] != expected[i])
            {
                return false;
            }
        }

        return true;
    }

    private static string Decode(byte[] der, byte tag, int start, int end) => tag switch
    {
        Utf8StringTag => Encoding.UTF8.GetString(der, start, end - start),
        PrintableStringTag or Ia5StringTag => Encoding.ASCII.GetString(der, start, end - start),
        BmpStringTag => Encoding.BigEndianUnicode.GetString(der, start, end - start),
        _ => throw new FormatException("Unsupported string type in a name."),
    };
}
