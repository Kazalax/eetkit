using System.Xml;
using EetKit.Internal;
using Xunit;

namespace EetKit.Tests;

/// <summary>
/// Acceptance test of M1: signing the data of each official sample with its playground
/// certificate gives the same digest, signature and token as the sample, character for character.
/// </summary>
public sealed class SampleSignatureTests
{
    [Theory]
    [InlineData("CZ00000019")]
    [InlineData("CZ683555118")]
    [InlineData("CZ8551015704")]
    public void Sign_MatchesSample(string eic)
    {
        var sample = SampleRequest.Load(eic);
        var (p12, password) = SpecFiles.TestCertificate(eic);
        using var certificate = EetCertificate.FromPkcs12(p12, password);
        var envelope = SoapEnvelopeBuilder.Build(sample.Trzba, sample.Header, sample.Ids);

        WsSecuritySigner.Sign(envelope, certificate, sample.Ids);

        Assert.Equal(sample.DigestValue, Text(envelope, "DigestValue"));
        Assert.Equal(sample.SignatureValue, Text(envelope, "SignatureValue"));
        Assert.Equal(sample.TokenBase64, Text(envelope, "BinarySecurityToken"));
    }

    private static string Text(XmlDocument document, string localName) =>
        document.GetElementsByTagName("*").OfType<XmlElement>().Single(e => e.LocalName == localName).InnerText;
}
