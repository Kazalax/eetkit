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
    public void Verify_WrappedBodyWithDuplicateId_False()
    {
        var document = WrapSignedBody(SampleRequest.LoadDocument(Eic), keepIdOnFakeBody: true);

        Assert.False(WsSecurityVerifier.Verify(document));
    }

    [Fact]
    public void Verify_ReferenceNotToEnvelopeBody_False()
    {
        var document = WrapSignedBody(SampleRequest.LoadDocument(Eic), keepIdOnFakeBody: false);

        Assert.False(WsSecurityVerifier.Verify(document));
    }

    // Signature wrapping: the original signed Body moves into a wrapper in the Header and a fake
    // Body with a different amount takes its place.
    private static XmlDocument WrapSignedBody(XmlDocument document, bool keepIdOnFakeBody)
    {
        var envelope = document.DocumentElement!;
        var header = envelope["Header", EetNamespaces.Soap]!;
        var original = envelope["Body", EetNamespaces.Soap]!;

        var fake = (XmlElement)original.CloneNode(deep: true);
        if (!keepIdOnFakeBody)
        {
            fake.RemoveAttribute("Id", EetNamespaces.Wsu);
        }

        var data = fake.GetElementsByTagName("*").OfType<XmlElement>().Single(e => e.LocalName == "Data");
        data.SetAttribute("celk_trzba", "1.00");

        var wrapper = document.CreateElement("wrap", "Wrapper", "urn:attacker");
        envelope.RemoveChild(original);
        wrapper.AppendChild(original);
        header.AppendChild(wrapper);
        envelope.AppendChild(fake);
        return document;
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

    // Measured on the playground on 2026-11-06: 12 288 bytes accepted, 12 289 bytes rejected with error 7.
    private const int PlaygroundLimitBytes = 12_288;

    [Fact]
    public void Serialize_AtLimit_Passes()
    {
        var envelope = PaddedTo(SignedEnvelope(), PlaygroundLimitBytes);

        Assert.Equal(PlaygroundLimitBytes, EnvelopeSerializer.ToUtf8(envelope).Length);
    }

    [Fact]
    public void Serialize_OverLimit_ThrowsValidationException()
    {
        var envelope = PaddedTo(SignedEnvelope(), PlaygroundLimitBytes + 1);

        var exception = Assert.Throws<EetValidationException>(() => EnvelopeSerializer.ToUtf8(envelope));

        Assert.Equal("zprava", Assert.Single(exception.Errors).Field);
    }

    [Fact]
    public async Task Sign_Concurrent20_AllVerify()
    {
        var sample = SampleRequest.Load(Eic);
        var (p12, password) = SpecFiles.TestCertificate(Eic);
        using var certificate = EetCertificate.FromPkcs12(p12, password);

        var envelopes = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
        {
            var ids = SoapEnvelopeIds.CreateRandom();
            var envelope = SoapEnvelopeBuilder.Build(sample.Trzba, sample.Header, ids);
            WsSecuritySigner.Sign(envelope, certificate, ids);
            return envelope;
        }, TestContext.Current.CancellationToken)));

        Assert.All(envelopes, envelope => Assert.True(WsSecurityVerifier.Verify(envelope)));
    }

    // A comment in the unsigned header changes the size without touching the signature.
    private static XmlDocument PaddedTo(XmlDocument envelope, int targetBytes)
    {
        const int CommentMarkupBytes = 7;
        var header = envelope.DocumentElement!["Header", EetNamespaces.Soap]!;
        var current = Encoding.UTF8.GetByteCount(envelope.OuterXml);
        header.PrependChild(envelope.CreateComment(new string('x', targetBytes - current - CommentMarkupBytes)));
        return envelope;
    }
}
