using System.IO.Compression;
using System.Text;

namespace EetKit.Sample.WinForms;

/// <summary>
/// Reads a playground test certificate from the official archive next to the program.
/// A real cash register loads its own .p12, for example with <c>EetClient.FromPkcs12(path, password, environment)</c>.
/// </summary>
internal static class PlaygroundCertificate
{
    public const string Eic = "CZ00000019";

    private const string ArchiveName = "CAEET_Playground_2026_v1.zip";
    private const string PasswordEntry = "password_pokladni_cert_playground.txt";

    public static EetCertificate Load()
    {
        using var archive = ZipFile.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ArchiveName));
        var p12 = Read(archive, $"CA_EET-Playground-{Eic}.p12");
        var password = Encoding.ASCII.GetString(Read(archive, PasswordEntry)).TrimEnd('\r', '\n');
        return EetCertificate.FromPkcs12(p12, password);
    }

    private static byte[] Read(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new FileNotFoundException($"{name} chybí v archivu {ArchiveName}.");
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}

/// <summary>Hands the client a certificate that is already loaded, for example from a database.</summary>
internal sealed class LoadedCertificateProvider(EetCertificate certificate) : IEetCertificateProvider
{
    public EetCertificate GetCertificate() => certificate;
}
