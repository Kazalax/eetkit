namespace EetKit.Internal;

/// <summary>Namespace URIs and the prefixes the official sample requests use for them.</summary>
internal static class EetNamespaces
{
    public const string Xmlns = "http://www.w3.org/2000/xmlns/";

    public const string SoapPrefix = "soapenv";
    public const string Soap = "http://schemas.xmlsoap.org/soap/envelope/";

    public const string V4Prefix = "v4";
    public const string V4 = "http://fs.gov.cz/eet/schema/v4";

    public const string WssePrefix = "wsse";
    public const string Wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";

    public const string WsuPrefix = "wsu";
    public const string Wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";

    public const string DsPrefix = "ds";
    public const string Ds = "http://www.w3.org/2000/09/xmldsig#";

    public const string EcPrefix = "ec";
    public const string ExcC14n = "http://www.w3.org/2001/10/xml-exc-c14n#";
}
