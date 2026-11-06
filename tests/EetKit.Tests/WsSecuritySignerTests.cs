using System.Text;
using System.Xml;
using EetKit.Internal;
using Xunit;

namespace EetKit.Tests;

public sealed class WsSecuritySignerTests
{
    private const string Eic = "CZ00000019";

    private static XmlDocument SignedEnvelope()
    {
        var sample = SampleRequest.Load(Eic);
        var (p12, password) = SpecFiles.TestCertificate(Eic);
        using var certificate = EetCertificate.FromPkcs12(p12, password);
        var ids = SoapEnvelopeIds.CreateRandom();
        var envelope = SoapEnvelopeBuilder.Build(sample.Trzba, sample.Header, ids);
        WsSecuritySigner.Sign(envelope, certificate, ids);
        return envelope;
    }

    private static XmlElement Body(XmlDocument document) =>
        document.GetElementsByTagName("*").OfType<XmlElement>().Single(e => e.LocalName == "Body");

    [Fact]
    public void Verify_SampleRequest_True()
    {
        Assert.True(WsSecurityVerifier.Verify(SampleRequest.LoadDocument(Eic)));
    }

    [Fact]
    public void Verify_TamperedAmount_False()
    {
        var document = SampleRequest.LoadDocument(Eic);
        var data = document.GetElementsByTagName("*").OfType<XmlElement>().Single(e => e.LocalName == "Data");
        data.SetAttribute("celk_trzba", "1.00");

        Assert.False(WsSecurityVerifier.Verify(document));
    }

    [Fact]
    public void Verify_OwnSignature_True()
    {
        Assert.True(WsSecurityVerifier.Verify(SignedEnvelope()));
    }

    [Fact]
    public void Verify_UnsignedEnvelope_False()
    {
        var sample = SampleRequest.Load(Eic);

        Assert.False(WsSecurityVerifier.Verify(SoapEnvelopeBuilder.Build(sample.Trzba, sample.Header, sample.Ids)));
    }

    [Fact]
    public void Sign_NoTimestampNoAddressing()
    {
        var xml = SignedEnvelope().OuterXml;

        Assert.DoesNotContain("Timestamp", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("http://www.w3.org/2005/08/addressing", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_NoBomNoDeclaration()
    {
        var bytes = EnvelopeSerializer.ToUtf8(SignedEnvelope());

        Assert.Equal((byte)'<', bytes[0]);
        Assert.StartsWith("<soapenv:Envelope", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_SampleSize_UnderLimit()
    {
        Assert.True(EnvelopeSerializer.ToUtf8(SignedEnvelope()).Length < EnvelopeSerializer.MaxMessageBytes);
    }

    [Fact]
    public void Serialize_OverLimit_ThrowsValidationException()
    {
        var envelope = SignedEnvelope();
        Body(envelope).AppendChild(envelope.CreateComment(new string('x', EnvelopeSerializer.MaxMessageBytes)));

        var exception = Assert.Throws<EetValidationException>(() => EnvelopeSerializer.ToUtf8(envelope));

        Assert.Equal("zprava", Assert.Single(exception.Errors).Field);
    }
}
