using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using static EetKit.Internal.EetNamespaces;

namespace EetKit.Internal;

/// <summary>
/// Checks the WS-Security signature of a SOAP message with the certificate it carries.
/// Only the shape EET uses is accepted: one signature in soapenv:Header/wsse:Security whose
/// single reference points at the envelope's own soapenv:Body, with unique wsu:Id values.
/// Trust in the certificate itself (I.CA chain, GFŘ subject) is not checked here.
/// </summary>
internal static class WsSecurityVerifier
{
    public static bool Verify(XmlDocument envelope)
    {
        Guard.NotNull(envelope, nameof(envelope));

        var root = envelope.DocumentElement;
        if (root is null || root.LocalName != "Envelope" || root.NamespaceURI != Soap)
        {
            return false;
        }

        var header = root["Header", Soap];
        var body = root["Body", Soap];
        var signatures = envelope.GetElementsByTagName("Signature", Ds).OfType<XmlElement>().ToList();
        if (header is null || body is null || signatures.Count != 1)
        {
            return false;
        }

        var signature = signatures[0];
        if (signature.ParentNode is not XmlElement security
            || security.LocalName != "Security" || security.NamespaceURI != Wsse
            || security.ParentNode != header)
        {
            return false;
        }

        var bodyId = body.GetAttribute("Id", Wsu);
        if (bodyId.Length == 0 || ElementsWithId(envelope, bodyId).Count != 1)
        {
            return false;
        }

        try
        {
            var signedXml = new WsuSignedXml(envelope);
            signedXml.LoadXml(signature);
            if (signedXml.SignedInfo is not { References.Count: 1 } signedInfo
                || ((Reference)signedInfo.References[0]!).Uri != "#" + bodyId)
            {
                return false;
            }

            var token = ReferencedToken(envelope, signature);
            if (token is null)
            {
                return false;
            }

            using var certificate = LoadCertificate(Convert.FromBase64String(token.InnerText));
            using var publicKey = certificate.GetRSAPublicKey();
            return publicKey is not null && signedXml.CheckSignature(publicKey);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // The token is the one KeyInfo/SecurityTokenReference/Reference points to, not just any token.
    private static XmlElement? ReferencedToken(XmlDocument envelope, XmlElement signature)
    {
        var reference = signature.GetElementsByTagName("Reference", Wsse).OfType<XmlElement>().SingleOrDefault();
        var uri = reference?.GetAttribute("URI");
        if (uri is null || uri.Length < 2 || uri[0] != '#')
        {
            return null;
        }

        var matches = ElementsWithId(envelope, uri.Substring(1));
        return matches.Count == 1 && matches[0].LocalName == "BinarySecurityToken" && matches[0].NamespaceURI == Wsse
            ? matches[0]
            : null;
    }

    private static List<XmlElement> ElementsWithId(XmlDocument document, string id) =>
        document.GetElementsByTagName("*").OfType<XmlElement>()
            .Where(e => e.GetAttribute("Id", Wsu) == id || e.GetAttribute("Id") == id)
            .ToList();

    private static X509Certificate2 LoadCertificate(byte[] der)
    {
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadCertificate(der);
#else
        return new X509Certificate2(der);
#endif
    }

    /// <summary>SignedXml only resolves plain Id attributes; WS-Security references use wsu:Id.</summary>
    private sealed class WsuSignedXml : SignedXml
    {
        public WsuSignedXml(XmlDocument document)
            : base(document)
        {
        }

        public override XmlElement? GetIdElement(XmlDocument? document, string idValue)
        {
            if (document is null)
            {
                return null;
            }

            var matches = ElementsWithId(document, idValue);
            return matches.Count == 1 ? matches[0] : null;
        }
    }
}
