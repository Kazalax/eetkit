using System.Xml;

namespace EetKit.Tests;

/// <summary>Loads test XML without DTDs or external resources (the .NET Framework defaults allow both).</summary>
internal static class TestXml
{
    private static readonly XmlReaderSettings Settings = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

    public static XmlDocument FromFile(string path)
    {
        using var reader = XmlReader.Create(path, Settings);
        return Load(reader);
    }

    public static XmlDocument FromString(string xml)
    {
        using var text = new StringReader(xml);
        using var reader = XmlReader.Create(text, Settings);
        return Load(reader);
    }

    private static XmlDocument Load(XmlReader reader)
    {
        var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        document.Load(reader);
        return document;
    }
}
