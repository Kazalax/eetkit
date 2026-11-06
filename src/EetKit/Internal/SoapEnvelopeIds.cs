using System.Security.Cryptography;
using System.Text;

namespace EetKit.Internal;

/// <summary>The wsu:Id and Id values that link the signature to the token and the body.</summary>
internal sealed record SoapEnvelopeIds(string Token, string Signature, string KeyInfo, string TokenReference, string Body)
{
    // The sample requests use 33 upper-case hex characters after the prefix.
    private const int HexLength = 33;

    public static SoapEnvelopeIds CreateRandom() => new(
        Token: "X509-" + RandomHex(),
        Signature: "SIG-" + RandomHex(),
        KeyInfo: "KI-" + RandomHex(),
        TokenReference: "STR-" + RandomHex(),
        Body: "id-" + RandomHex());

    private static string RandomHex()
    {
        var bytes = new byte[(HexLength + 1) / 2];
        using (var random = RandomNumberGenerator.Create())
        {
            random.GetBytes(bytes);
        }

        var hex = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            hex.Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return hex.ToString(0, HexLength);
    }
}
