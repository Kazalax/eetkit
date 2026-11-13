using System.Globalization;
using System.Xml;
using static EetKit.Internal.EetNamespaces;

namespace EetKit.Internal;

/// <summary>What the parser needs to know about the message the response belongs to.</summary>
internal sealed record ResponseContext(
    Guid SentUuid,
    string? GlobalTransactionId,
    IReadOnlyList<string> LocalWarnings,
    DateTimeOffset Now,
    EetEnvironment Environment);

/// <summary>
/// Reads the server's SOAP response and classifies it (plan M2, decisions 5 to 7 and 10).
/// Elements are matched by namespace, not prefix: the server writes eet:, the samples v4:.
/// </summary>
internal static class ResponseParser
{
    private const int TemporaryTechnicalError = -1;
    private const int VerifiedOk = 0;
    private const int FirstRejectedCode = 2;
    private const int LastRejectedCode = 7;
    private const int TechnicalOrDataError = 8;

    private static readonly XmlReaderSettings SafeReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = false,
    };

    public static EetResult Parse(byte[] body, ResponseContext context)
    {
        Guard.NotNull(body, nameof(body));
        Guard.NotNull(context, nameof(context));

        XmlDocument document;
        try
        {
            document = Load(body);
        }
        catch (XmlException)
        {
            return Temporary(context, "Odpověď serveru není čitelné XML.");
        }

        var soapBody = document.DocumentElement is { LocalName: "Envelope", NamespaceURI: Soap } envelope
            ? envelope["Body", Soap]
            : null;
        if (soapBody is null)
        {
            return Temporary(context, "Odpověď serveru nemá SOAP tělo.");
        }

        if (soapBody["Fault", Soap] is { } fault)
        {
            return Temporary(context, "SOAP Fault: " + (fault["faultstring"]?.InnerText.Trim() ?? string.Empty));
        }

        var odpoved = soapBody["Odpoved", V4];
        var hlavicka = odpoved?["Hlavicka", V4];
        if (odpoved is null || hlavicka is null)
        {
            return Temporary(context, "Odpověď serveru neobsahuje element Odpoved.");
        }

        var uuid = Attribute(hlavicka, "uuid_zpravy");
        if (uuid is not null && (!Guid.TryParse(uuid, out var answered) || answered != context.SentUuid))
        {
            return Temporary(context, "Odpověď patří k jiné zprávě (uuid_zpravy se neshoduje).");
        }

        var warnings = odpoved.ChildNodes.OfType<XmlElement>()
            .Where(e => e.LocalName == "Varovani" && e.NamespaceURI == V4)
            .Select(e => new EetWarning(IntAttribute(e, "kod_varov") ?? 0, e.InnerText.Trim()))
            .ToList();

        if (odpoved["Potvrzeni", V4] is { } potvrzeni)
        {
            return Confirmation(document, hlavicka, potvrzeni, warnings, context);
        }

        if (odpoved["Chyba", V4] is { } chyba && IntAttribute(chyba, "kod") is { } code)
        {
            return new EetResult(Classify(code), context.SentUuid, context.LocalWarnings)
            {
                ErrorCode = code,
                ErrorText = chyba.InnerText.Trim(),
                Warnings = warnings,
                DatOdmit = TimeAttribute(hlavicka, "dat_odmit"),
                DatPrij = TimeAttribute(hlavicka, "dat_prij"),
                IsTest = BoolAttribute(chyba, "test"),
                GlobalTransactionId = context.GlobalTransactionId,
            };
        }

        return Temporary(context, "Odpověď serveru neobsahuje Potvrzeni ani Chyba.");
    }

    private static EetResult Confirmation(
        XmlDocument document, XmlElement hlavicka, XmlElement potvrzeni, List<EetWarning> warnings, ResponseContext context)
    {
        var signedByServer = WsSecurityVerifier.Verify(
            document, certificate => ServerCertificateTrust.IsTrusted(certificate, context.Now));
        var pok = Attribute(potvrzeni, "pok");
        if (!signedByServer || string.IsNullOrEmpty(pok))
        {
            return Temporary(context, "Podpis potvrzení se nepodařilo ověřit, potvrzení nejde použít.");
        }

        // A confirmation must name the message it confirms; error responses may omit it when the
        // server could not read the request, a confirmation never does.
        if (Attribute(hlavicka, "uuid_zpravy") is null)
        {
            return Temporary(context, "Potvrzení neuvádí uuid_zpravy, nejde ho přiřadit k odeslané zprávě.");
        }

        var isTest = BoolAttribute(potvrzeni, "test");
        if (isTest != (context.Environment == EetEnvironment.Playground))
        {
            return Temporary(context, "Potvrzení pochází z jiného prostředí, než do kterého se zpráva posílala.");
        }

        return new EetResult(EetStatus.Confirmed, context.SentUuid, context.LocalWarnings)
        {
            Pok = pok,
            Warnings = warnings,
            DatPrij = TimeAttribute(hlavicka, "dat_prij"),
            IsTest = isTest,
            GlobalTransactionId = context.GlobalTransactionId,
        };
    }

    private static EetStatus Classify(int code) => code switch
    {
        VerifiedOk => EetStatus.VerifiedOnly,
        TemporaryTechnicalError or TechnicalOrDataError => EetStatus.TemporaryError,
        >= FirstRejectedCode and <= LastRejectedCode => EetStatus.Rejected,
        < 0 => EetStatus.TemporaryError,
        _ => EetStatus.Rejected,
    };

    private static XmlDocument Load(byte[] body)
    {
        var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        using var stream = new MemoryStream(body);
        using var reader = XmlReader.Create(stream, SafeReaderSettings);
        document.Load(reader);
        return document;
    }

    private static EetResult Temporary(ResponseContext context, string text) =>
        new(EetStatus.TemporaryError, context.SentUuid, context.LocalWarnings)
        {
            ErrorText = text,
            GlobalTransactionId = context.GlobalTransactionId,
        };

    private static string? Attribute(XmlElement element, string name) =>
        element.HasAttribute(name) ? element.GetAttribute(name) : null;

    private static int? IntAttribute(XmlElement element, string name) =>
        int.TryParse(Attribute(element, name), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static bool BoolAttribute(XmlElement element, string name) =>
        Attribute(element, name) is { } value && XmlConvert.ToBoolean(value);

    private static DateTimeOffset? TimeAttribute(XmlElement element, string name) =>
        Attribute(element, name) is { } value
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : null;
}
