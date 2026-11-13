namespace EetKit.Internal;

/// <summary>The only algorithms EET uses (popis rozhraní 5.2), for signing and for verifying.</summary>
internal static class XmlSignatureAlgorithms
{
    public const string ExcC14n = EetNamespaces.ExcC14n;
    public const string RsaSha256 = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    public const string Sha256 = "http://www.w3.org/2001/04/xmlenc#sha256";
}
