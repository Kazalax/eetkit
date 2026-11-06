using System.Globalization;
using System.Xml;
using static EetKit.Internal.EetNamespaces;

namespace EetKit.Internal;

/// <summary>Builds the unsigned SOAP envelope with the v4:Trzba element, shaped like the official samples.</summary>
internal static class SoapEnvelopeBuilder
{
    public static XmlDocument Build(Trzba trzba, MessageHeader header, SoapEnvelopeIds ids)
    {
        Guard.NotNull(trzba, nameof(trzba));
        Guard.NotNull(header, nameof(header));
        Guard.NotNull(ids, nameof(ids));

        var document = new XmlDocument { PreserveWhitespace = true };

        var envelope = document.CreateElement(SoapPrefix, "Envelope", Soap);
        DeclareNamespace(envelope, SoapPrefix, Soap);
        DeclareNamespace(envelope, V4Prefix, V4);
        document.AppendChild(envelope);

        envelope.AppendChild(document.CreateElement(SoapPrefix, "Header", Soap));

        var body = document.CreateElement(SoapPrefix, "Body", Soap);
        DeclareNamespace(body, WsuPrefix, Wsu);
        var bodyId = document.CreateAttribute(WsuPrefix, "Id", Wsu);
        bodyId.Value = ids.Body;
        body.Attributes.Append(bodyId);
        envelope.AppendChild(body);

        var trzbaElement = document.CreateElement(V4Prefix, "Trzba", V4);
        trzbaElement.AppendChild(BuildHlavicka(document, header));
        trzbaElement.AppendChild(BuildData(document, trzba));
        body.AppendChild(trzbaElement);

        return document;
    }

    private static XmlElement BuildHlavicka(XmlDocument document, MessageHeader header)
    {
        var hlavicka = document.CreateElement(V4Prefix, "Hlavicka", V4);
        hlavicka.SetAttribute("dat_odesl", EetFormat.Timestamp(header.DatOdesl));
        hlavicka.SetAttribute("overeni", EetFormat.Bool(header.Overeni));
        hlavicka.SetAttribute("prvni_zaslani", EetFormat.Bool(header.PrvniZaslani));
        hlavicka.SetAttribute("uuid_zpravy", EetFormat.Uuid(header.UuidZpravy));
        return hlavicka;
    }

    private static XmlElement BuildData(XmlDocument document, Trzba trzba)
    {
        var data = document.CreateElement(V4Prefix, "Data", V4);
        data.SetAttribute("celk_trzba", EetFormat.Amount(trzba.CelkTrzba));
        SetOptional(data, "cerp_zuct", trzba.CerpZuct is { } cerp ? EetFormat.Amount(cerp) : null);
        data.SetAttribute("dat_trzby", EetFormat.Timestamp(trzba.DatTrzby));
        data.SetAttribute("eic_popl", trzba.EicPopl);
        SetOptional(data, "eic_poverujiciho", trzba.EicPoverujiciho);
        data.SetAttribute("id_jednotky", trzba.IdJednotky.ToString(CultureInfo.InvariantCulture));
        data.SetAttribute("id_pokl", trzba.IdPokl);
        data.SetAttribute("porad_cis", trzba.PoradCis);
        SetOptional(data, "povereni_vice_popl", trzba.PovereniVicePopl is { } povereni ? EetFormat.Bool(povereni) : null);
        SetOptional(data, "urceno_cerp_zuct", trzba.UrcenoCerpZuct is { } urceno ? EetFormat.Amount(urceno) : null);
        return data;
    }

    private static void SetOptional(XmlElement element, string name, string? value)
    {
        if (value is not null)
        {
            element.SetAttribute(name, value);
        }
    }

    // DOM elements carry no xmlns attributes by themselves; canonicalization needs them explicitly.
    private static void DeclareNamespace(XmlElement element, string prefix, string uri)
    {
        var declaration = element.OwnerDocument.CreateAttribute("xmlns", prefix, Xmlns);
        declaration.Value = uri;
        element.Attributes.Append(declaration);
    }
}
