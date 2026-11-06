using System.Globalization;
using System.Xml;
using EetKit.Internal;

namespace EetKit.Tests;

/// <summary>One official sample request from docs/spec, taken apart into the values that produced it.</summary>
internal sealed record SampleRequest(
    string Eic,
    Trzba Trzba,
    MessageHeader Header,
    SoapEnvelopeIds Ids,
    string DigestValue,
    string SignatureValue,
    string TokenBase64)
{
    private const string Wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";

    public static XmlDocument LoadDocument(string eic)
    {
        var document = new XmlDocument { PreserveWhitespace = true };
        document.Load(SpecFiles.Path($"{eic}.eet.v4.req.xml"));
        return document;
    }

    public static SampleRequest Load(string eic)
    {
        var document = LoadDocument(eic);
        var hlavicka = Single(document, "Hlavicka");
        var data = Single(document, "Data");

        var trzba = new Trzba
        {
            EicPopl = data.GetAttribute("eic_popl"),
            EicPoverujiciho = Optional(data, "eic_poverujiciho"),
            PovereniVicePopl = Optional(data, "povereni_vice_popl") is { } povereni ? bool.Parse(povereni) : null,
            IdJednotky = int.Parse(data.GetAttribute("id_jednotky"), CultureInfo.InvariantCulture),
            IdPokl = data.GetAttribute("id_pokl"),
            PoradCis = data.GetAttribute("porad_cis"),
            DatTrzby = Time(data.GetAttribute("dat_trzby")),
            CelkTrzba = Amount(data.GetAttribute("celk_trzba")),
            UrcenoCerpZuct = Optional(data, "urceno_cerp_zuct") is { } urceno ? Amount(urceno) : null,
            CerpZuct = Optional(data, "cerp_zuct") is { } cerp ? Amount(cerp) : null,
        };

        var header = new MessageHeader(
            Guid.Parse(hlavicka.GetAttribute("uuid_zpravy")),
            Time(hlavicka.GetAttribute("dat_odesl")),
            bool.Parse(hlavicka.GetAttribute("prvni_zaslani")),
            bool.Parse(hlavicka.GetAttribute("overeni")));

        var token = Single(document, "BinarySecurityToken");
        var ids = new SoapEnvelopeIds(
            Token: token.GetAttribute("Id", Wsu),
            Signature: Single(document, "Signature").GetAttribute("Id"),
            KeyInfo: Single(document, "KeyInfo").GetAttribute("Id"),
            TokenReference: Single(document, "SecurityTokenReference").GetAttribute("Id", Wsu),
            Body: Single(document, "Body").GetAttribute("Id", Wsu));

        return new SampleRequest(
            eic,
            trzba,
            header,
            ids,
            Single(document, "DigestValue").InnerText,
            Single(document, "SignatureValue").InnerText,
            token.InnerText);
    }

    private static XmlElement Single(XmlDocument document, string localName)
    {
        var matches = document.GetElementsByTagName("*").OfType<XmlElement>().Where(e => e.LocalName == localName).ToList();
        return matches.Count == 1
            ? matches[0]
            : throw new InvalidOperationException($"Expected one {localName}, found {matches.Count}.");
    }

    private static string? Optional(XmlElement element, string name) =>
        element.HasAttribute(name) ? element.GetAttribute(name) : null;

    private static DateTimeOffset Time(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.None);

    private static decimal Amount(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
}
