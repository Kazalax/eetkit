using System.Security.Cryptography;
using System.Xml;
using static EetKit.Internal.EetNamespaces;

namespace EetKit.Internal;

/// <summary>
/// Adds the WS-Security header that signs soapenv:Body, in exactly the shape of the official
/// sample requests. SignedInfo is assembled by hand because the signature value covers its
/// canonical form including the ds: and ec: prefixes, which SignedXml does not produce.
/// </summary>
internal static class WsSecuritySigner
{
    private const string Base64Encoding =
        "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary";
    private const string X509TokenType =
        "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3";
    private const string RsaSha256 = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    private const string Sha256 = "http://www.w3.org/2001/04/xmlenc#sha256";
    private const string SignedInfoInclusivePrefixes = "soapenv v4";
    private const string BodyInclusivePrefixes = "v4";

    public static void Sign(XmlDocument envelope, EetCertificate certificate, SoapEnvelopeIds ids)
    {
        Guard.NotNull(envelope, nameof(envelope));
        Guard.NotNull(certificate, nameof(certificate));
        Guard.NotNull(ids, nameof(ids));

        var header = SoapElement(envelope, "Header");
        var body = SoapElement(envelope, "Body");

        var digest = Digest(body);

        var security = Element(envelope, WssePrefix, "Security", Wsse);
        Declare(security, WssePrefix, Wsse);
        Declare(security, WsuPrefix, Wsu);
        security.AppendChild(BinarySecurityToken(envelope, certificate, ids.Token));

        var signature = Element(envelope, DsPrefix, "Signature", Ds);
        signature.SetAttribute("Id", ids.Signature);
        Declare(signature, DsPrefix, Ds);
        var signedInfo = SignedInfo(envelope, ids.Body, digest);
        signature.AppendChild(signedInfo);
        var signatureValue = signature.AppendChild(Element(envelope, DsPrefix, "SignatureValue", Ds))!;
        signature.AppendChild(KeyInfo(envelope, ids));
        security.AppendChild(signature);

        // SignedInfo is canonicalized in place, so the namespaces of its ancestors are in scope.
        header.AppendChild(security);
        var canonicalSignedInfo = ExclusiveCanonicalizer.Canonicalize(signedInfo, SignedInfoInclusivePrefixes);
        var signatureBytes = certificate.PrivateKey.SignData(
            canonicalSignedInfo, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        signatureValue.InnerText = Convert.ToBase64String(signatureBytes);
    }

    private static string Digest(XmlElement body)
    {
        var canonicalBody = ExclusiveCanonicalizer.Canonicalize(body, BodyInclusivePrefixes);
#if NET5_0_OR_GREATER
        return Convert.ToBase64String(SHA256.HashData(canonicalBody));
#else
        using var sha256 = SHA256.Create();
        return Convert.ToBase64String(sha256.ComputeHash(canonicalBody));
#endif
    }

    private static XmlElement BinarySecurityToken(XmlDocument document, EetCertificate certificate, string tokenId)
    {
        var token = Element(document, WssePrefix, "BinarySecurityToken", Wsse);
        token.SetAttribute("EncodingType", Base64Encoding);
        token.SetAttribute("ValueType", X509TokenType);
        token.SetAttribute("Id", Wsu, tokenId);
        token.Attributes["Id", Wsu]!.Prefix = WsuPrefix;
        token.InnerText = Convert.ToBase64String(certificate.Certificate.RawData);
        return token;
    }

    private static XmlElement SignedInfo(XmlDocument document, string bodyId, string digest)
    {
        var signedInfo = Element(document, DsPrefix, "SignedInfo", Ds);

        var canonicalization = Element(document, DsPrefix, "CanonicalizationMethod", Ds);
        canonicalization.SetAttribute("Algorithm", ExcC14n);
        canonicalization.AppendChild(InclusiveNamespaces(document, SignedInfoInclusivePrefixes));
        signedInfo.AppendChild(canonicalization);

        var signatureMethod = Element(document, DsPrefix, "SignatureMethod", Ds);
        signatureMethod.SetAttribute("Algorithm", RsaSha256);
        signedInfo.AppendChild(signatureMethod);

        var reference = Element(document, DsPrefix, "Reference", Ds);
        reference.SetAttribute("URI", "#" + bodyId);
        var transforms = Element(document, DsPrefix, "Transforms", Ds);
        var transform = Element(document, DsPrefix, "Transform", Ds);
        transform.SetAttribute("Algorithm", ExcC14n);
        transform.AppendChild(InclusiveNamespaces(document, BodyInclusivePrefixes));
        transforms.AppendChild(transform);
        reference.AppendChild(transforms);
        var digestMethod = Element(document, DsPrefix, "DigestMethod", Ds);
        digestMethod.SetAttribute("Algorithm", Sha256);
        reference.AppendChild(digestMethod);
        var digestValue = Element(document, DsPrefix, "DigestValue", Ds);
        digestValue.InnerText = digest;
        reference.AppendChild(digestValue);
        signedInfo.AppendChild(reference);

        return signedInfo;
    }

    private static XmlElement InclusiveNamespaces(XmlDocument document, string prefixList)
    {
        var inclusive = Element(document, EcPrefix, "InclusiveNamespaces", ExcC14n);
        inclusive.SetAttribute("PrefixList", prefixList);
        Declare(inclusive, EcPrefix, ExcC14n);
        return inclusive;
    }

    private static XmlElement KeyInfo(XmlDocument document, SoapEnvelopeIds ids)
    {
        var keyInfo = Element(document, DsPrefix, "KeyInfo", Ds);
        keyInfo.SetAttribute("Id", ids.KeyInfo);

        var tokenReference = Element(document, WssePrefix, "SecurityTokenReference", Wsse);
        tokenReference.SetAttribute("Id", Wsu, ids.TokenReference);
        tokenReference.Attributes["Id", Wsu]!.Prefix = WsuPrefix;

        var reference = Element(document, WssePrefix, "Reference", Wsse);
        reference.SetAttribute("URI", "#" + ids.Token);
        reference.SetAttribute("ValueType", X509TokenType);

        tokenReference.AppendChild(reference);
        keyInfo.AppendChild(tokenReference);
        return keyInfo;
    }

    private static XmlElement SoapElement(XmlDocument envelope, string localName) =>
        envelope.DocumentElement?[localName, Soap]
        ?? throw new ArgumentException($"The envelope has no soapenv:{localName}.", nameof(envelope));

    private static XmlElement Element(XmlDocument document, string prefix, string localName, string namespaceUri) =>
        document.CreateElement(prefix, localName, namespaceUri);

    private static void Declare(XmlElement element, string prefix, string uri)
    {
        var declaration = element.OwnerDocument.CreateAttribute("xmlns", prefix, Xmlns);
        declaration.Value = uri;
        element.Attributes.Append(declaration);
    }
}
