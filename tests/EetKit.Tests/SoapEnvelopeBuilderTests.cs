using System.Xml;
using System.Xml.Schema;
using EetKit.Internal;
using Xunit;

namespace EetKit.Tests;

public sealed class SoapEnvelopeBuilderTests
{
    private const string V4 = "http://fs.gov.cz/eet/schema/v4";
    private const string Wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";

    public static TheoryData<string> SampleEics => ["CZ00000019", "CZ683555118", "CZ8551015704"];

    [Theory]
    [MemberData(nameof(SampleEics))]
    public void Build_ValidatesAgainstXsd(string eic)
    {
        var sample = SampleRequest.Load(eic);

        var envelope = SoapEnvelopeBuilder.Build(sample.Trzba, sample.Header, sample.Ids);

        Assert.Empty(XsdProblems(envelope));
    }

    [Fact]
    public void Build_InvalidData_FailsXsd()
    {
        var sample = SampleRequest.Load("CZ00000019");
        var trzba = sample.Trzba with { IdPokl = "Pokladna č.1" };

        var envelope = SoapEnvelopeBuilder.Build(trzba, sample.Header, sample.Ids);

        Assert.Single(XsdProblems(envelope));
    }

    private static List<string> XsdProblems(XmlDocument envelope)
    {
        var trzba = new XmlDocument();
        trzba.AppendChild(trzba.ImportNode(Element(envelope, "Trzba"), deep: true));
        trzba.Schemas.Add(V4, SpecFiles.Path("EETXMLSchema.xsd"));
        var problems = new List<string>();
        trzba.Validate((_, e) => problems.Add(e.Message));
        return problems;
    }

    [Theory]
    [MemberData(nameof(SampleEics))]
    public void Build_SampleData_AttributesEqualSample(string eic)
    {
        var sample = SampleRequest.Load(eic);
        var expected = SampleRequest.LoadDocument(eic);

        var envelope = SoapEnvelopeBuilder.Build(sample.Trzba, sample.Header, sample.Ids);

        Assert.Equal(Attributes(Element(expected, "Hlavicka")), Attributes(Element(envelope, "Hlavicka")));
        Assert.Equal(Attributes(Element(expected, "Data")), Attributes(Element(envelope, "Data")));
    }

    [Fact]
    public void Build_OmitsNullOptionalAttributes()
    {
        var sample = SampleRequest.Load("CZ683555118");
        var trzba = sample.Trzba with
        {
            EicPoverujiciho = null,
            PovereniVicePopl = null,
            UrcenoCerpZuct = null,
            CerpZuct = null,
        };

        var data = Element(SoapEnvelopeBuilder.Build(trzba, sample.Header, sample.Ids), "Data");

        Assert.False(data.HasAttribute("eic_poverujiciho"));
        Assert.False(data.HasAttribute("povereni_vice_popl"));
        Assert.False(data.HasAttribute("urceno_cerp_zuct"));
        Assert.False(data.HasAttribute("cerp_zuct"));
    }

    [Fact]
    public void Build_AlwaysWritesOvereni()
    {
        var sample = SampleRequest.Load("CZ00000019");

        var hlavicka = Element(SoapEnvelopeBuilder.Build(sample.Trzba, sample.Header, sample.Ids), "Hlavicka");

        Assert.Equal("false", hlavicka.GetAttribute("overeni"));
    }

    [Fact]
    public void Build_BodyHasWsuId()
    {
        var sample = SampleRequest.Load("CZ00000019");

        var body = Element(SoapEnvelopeBuilder.Build(sample.Trzba, sample.Header, sample.Ids), "Body");

        Assert.Equal(sample.Ids.Body, body.GetAttribute("Id", Wsu));
    }

    [Fact]
    public void CreateRandom_IdsHaveSampleShape()
    {
        var ids = SoapEnvelopeIds.CreateRandom();

        Assert.Matches(@"\AX509-[0-9A-F]{33}\z", ids.Token);
        Assert.Matches(@"\ASIG-[0-9A-F]{33}\z", ids.Signature);
        Assert.Matches(@"\AKI-[0-9A-F]{33}\z", ids.KeyInfo);
        Assert.Matches(@"\ASTR-[0-9A-F]{33}\z", ids.TokenReference);
        Assert.Matches(@"\Aid-[0-9A-F]{33}\z", ids.Body);
        Assert.NotEqual(ids.Token[5..], ids.Body[3..]);
    }

    private static XmlElement Element(XmlDocument document, string localName) =>
        document.GetElementsByTagName("*").OfType<XmlElement>().Single(e => e.LocalName == localName);

    private static SortedDictionary<string, string> Attributes(XmlElement element) =>
        new(element.Attributes.OfType<XmlAttribute>().ToDictionary(a => a.Name, a => a.Value), StringComparer.Ordinal);
}
