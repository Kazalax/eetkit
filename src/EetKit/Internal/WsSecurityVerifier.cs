using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace EetKit.Internal;

/// <summary>Checks the WS-Security signature of a SOAP message with the certificate it carries.</summary>
internal static class WsSecurityVerifier
{
    public static bool Verify(XmlDocument envelope)
    {
        Guard.NotNull(envelope, nameof(envelope));

        var signature = FirstElement(envelope, "Signature", EetNamespaces.Ds);
        var token = FirstElement(envelope, "BinarySecurityToken", EetNamespaces.Wsse);
        if (signature is null || token is null)
        {
            return false;
        }

        try
        {
            using var certificate = LoadCertificate(Convert.FromBase64String(token.InnerText));
            using var publicKey = certificate.GetRSAPublicKey();
            if (publicKey is null)
            {
                return false;
            }

            var signedXml = new WsuSignedXml(envelope);
            signedXml.LoadXml(signature);
            return signedXml.CheckSignature(publicKey);
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

    private static XmlElement? FirstElement(XmlDocument document, string localName, string namespaceUri) =>
        document.GetElementsByTagName(localName, namespaceUri).OfType<XmlElement>().FirstOrDefault();

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
            var element = base.GetIdElement(document, idValue);
            if (element is not null || document is null)
            {
                return element;
            }

            return document.GetElementsByTagName("*").OfType<XmlElement>()
                .FirstOrDefault(e => e.GetAttribute("Id", EetNamespaces.Wsu) == idValue);
        }
    }
}
