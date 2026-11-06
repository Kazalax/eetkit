using System.IO.Compression;
using System.Text;

namespace EetKit.Tests;

/// <summary>Access to the official files from docs/spec copied next to the test assembly.</summary>
internal static class SpecFiles
{
    private const string SpecFolder = "spec";
    private const string PlaygroundArchive = "CAEET_Playground_2026_v1.zip";
    private const string PasswordEntry = "password_pokladni_cert_playground.txt";

    public static string Path(string fileName) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, SpecFolder, fileName);

    /// <summary>Reads the playground .p12 for the given EIC and its password straight from the archive.</summary>
    public static (byte[] P12, string Password) TestCertificate(string eic)
    {
        using var archive = ZipFile.OpenRead(Path(PlaygroundArchive));
        var p12 = ReadEntry(archive, $"CA_EET-Playground-{eic}.p12");
        var password = Encoding.ASCII.GetString(ReadEntry(archive, PasswordEntry)).TrimEnd('\r', '\n');
        return (p12, password);
    }

    private static byte[] ReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name)
            ?? throw new FileNotFoundException($"Entry '{name}' is missing in {PlaygroundArchive}.");
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
