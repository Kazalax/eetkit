using System.Xml;

namespace EetKit.Tests;

/// <summary>Real playground responses saved in tests/EetKit.Tests/Fixtures (see the README there).</summary>
internal static class Fixtures
{
    public const string Potvrzeni = "playground-potvrzeni.xml";
    public const string Chyba4 = "playground-chyba-4.xml";

    // The saved responses were received on this day; the server certificate is valid then.
    public static readonly DateTimeOffset CapturedAt = new(2026, 11, 6, 14, 11, 5, TimeSpan.Zero);

    public static string Path(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    public static byte[] Bytes(string name) => File.ReadAllBytes(Path(name));

    public static XmlDocument Document(string name) => TestXml.FromFile(Path(name));
}
