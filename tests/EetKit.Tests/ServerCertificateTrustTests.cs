using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using EetKit.Internal;
using Xunit;

namespace EetKit.Tests;

public sealed class ServerCertificateTrustTests
{
    private static Func<X509Certificate2, bool> TrustedAt(DateTimeOffset now) =>
        certificate => ServerCertificateTrust.IsTrusted(certificate, now);

    [Fact]
    public void PlaygroundConfirmation_VerifiedAndTrusted()
    {
        Assert.True(WsSecurityVerifier.Verify(Fixtures.Document(Fixtures.Potvrzeni), TrustedAt(Fixtures.CapturedAt)));
    }

    [Fact]
    public void SampleRequest_SignedByCashRegisterCertificate_NotTrusted()
    {
        var sample = SampleRequest.LoadDocument("CZ00000019");

        Assert.True(WsSecurityVerifier.Verify(sample));
        Assert.False(WsSecurityVerifier.Verify(sample, TrustedAt(Fixtures.CapturedAt)));
    }

    [Fact]
    public void PlaygroundConfirmation_AfterServerCertificateExpired_NotTrusted()
    {
        var afterExpiry = new DateTimeOffset(2027, 6, 9, 0, 0, 0, TimeSpan.Zero);

        Assert.False(WsSecurityVerifier.Verify(Fixtures.Document(Fixtures.Potvrzeni), TrustedAt(afterExpiry)));
    }

    [Fact]
    public void PlaygroundConfirmation_TamperedPok_False()
    {
        var document = Fixtures.Document(Fixtures.Potvrzeni);
        var potvrzeni = document.GetElementsByTagName("Potvrzeni", EetNamespaces.V4).OfType<XmlElement>().Single();
        potvrzeni.SetAttribute("pok", "00000000-0000-4000-8000-000000000000-ff");

        Assert.False(WsSecurityVerifier.Verify(document, TrustedAt(Fixtures.CapturedAt)));
    }

    [Fact]
    public void EmbeddedCertificates_MatchIcaFingerprints()
    {
        // SHA-256 fingerprints published on https://www.ica.cz/korenove-certifikaty (checked 2026-11-06).
        Assert.Equal("D279C01A12E8DD9A6230E459FAA447CEB336998477338C2EE4135C96737418EB", Sha256(ServerCertificateTrust.Root));
        Assert.Equal("DF5BAF6D7E1A7D14E9911C5B8C676EC6EBCAD9354A74F4AC7314E133E07A94DE", Sha256(ServerCertificateTrust.Issuer));
    }

    [Fact]
    public void Signature_WithRsaSha1_Rejected()
    {
        var document = SignedWith(SignedXml.XmlDsigRSASHA1Url, SignedXml.XmlDsigSHA1Url);

        Assert.False(WsSecurityVerifier.Verify(document));
    }

    [Fact]
    public void Signature_WithInclusiveCanonicalization_Rejected()
    {
        var document = SignedWith(
            "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256",
            "http://www.w3.org/2001/04/xmlenc#sha256",
            SignedXml.XmlDsigC14NTransformUrl);

        Assert.False(WsSecurityVerifier.Verify(document));
    }

    [Fact]
    public void Signature_TestHelperWithEetAlgorithms_Accepted()
    {
        // Guards the two tests above: the helper itself produces signatures the verifier accepts.
        var document = SignedWith(
            "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256",
            "http://www.w3.org/2001/04/xmlenc#sha256");

        Assert.True(WsSecurityVerifier.Verify(document));
    }

    private static string Sha256(X509Certificate2 certificate) =>
        Convert.ToHexString(SHA256.HashData(certificate.RawData));

    // Signs the sample body with SignedXml and the given algorithms, in the WS-Security shape
    // the verifier expects (token, reference to the token, signature in the Security header).
    private static XmlDocument SignedWith(string signatureMethod, string digestMethod, string? canonicalization = null)
    {
        canonicalization ??= SignedXml.XmlDsigExcC14NTransformUrl;
        var sample = SampleRequest.Load("CZ00000019");
        var (p12, password) = SpecFiles.TestCertificate("CZ00000019");
        using var certificate = EetCertificate.FromPkcs12(p12, password);
        var ids = SoapEnvelopeIds.CreateRandom();
        var document = SoapEnvelopeBuilder.Build(sample.Trzba, sample.Header, ids);

        var signedXml = new WsuIdSignedXml(document) { SigningKey = certificate.PrivateKey };
        signedXml.SignedInfo!.CanonicalizationMethod = canonicalization;
        signedXml.SignedInfo.SignatureMethod = signatureMethod;
        var reference = new Reference("#" + ids.Body) { DigestMethod = digestMethod };
        reference.AddTransform(canonicalization == SignedXml.XmlDsigC14NTransformUrl
            ? new XmlDsigC14NTransform()
            : new XmlDsigExcC14NTransform());
        signedXml.AddReference(reference);
        var tokenReference = document.CreateElement(EetNamespaces.WssePrefix, "SecurityTokenReference", EetNamespaces.Wsse);
        var wsseReference = document.CreateElement(EetNamespaces.WssePrefix, "Reference", EetNamespaces.Wsse);
        wsseReference.SetAttribute("URI", "#" + ids.Token);
        tokenReference.AppendChild(wsseReference);
        signedXml.KeyInfo = new KeyInfo();
        signedXml.KeyInfo.AddClause(new KeyInfoNode(tokenReference));
        signedXml.ComputeSignature();

        var header = document.DocumentElement!["Header", EetNamespaces.Soap]!;
        var security = document.CreateElement(EetNamespaces.WssePrefix, "Security", EetNamespaces.Wsse);
        var token = document.CreateElement(EetNamespaces.WssePrefix, "BinarySecurityToken", EetNamespaces.Wsse);
        var tokenId = document.CreateAttribute(EetNamespaces.WsuPrefix, "Id", EetNamespaces.Wsu);
        tokenId.Value = ids.Token;
        token.Attributes.Append(tokenId);
        token.InnerText = Convert.ToBase64String(certificate.Certificate.RawData);
        security.AppendChild(token);
        security.AppendChild(document.ImportNode(signedXml.GetXml(), deep: true));
        header.AppendChild(security);
        return document;
    }

    private sealed class WsuIdSignedXml(XmlDocument document) : SignedXml(document)
    {
        public override XmlElement? GetIdElement(XmlDocument? document, string idValue) =>
            document?.GetElementsByTagName("*").OfType<XmlElement>()
                .SingleOrDefault(e => e.GetAttribute("Id", EetNamespaces.Wsu) == idValue);
    }
}
