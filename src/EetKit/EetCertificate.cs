using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EetKit.Internal;

namespace EetKit;

/// <summary>
/// Pokladní certifikát poplatníka s privátním klíčem, kterým se podepisují datové zprávy.
/// </summary>
public sealed class EetCertificate : IDisposable
{
    private const string LoadFailedMessage =
        "Certifikát .p12 se nepodařilo načíst. Zkontrolujte soubor a heslo.";

    private EetCertificate(X509Certificate2 certificate, RSA privateKey)
    {
        Certificate = certificate;
        PrivateKey = privateKey;
        Eic = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        NotAfter = new DateTimeOffset(certificate.NotAfter.ToUniversalTime(), TimeSpan.Zero);
    }

    /// <summary>EIČ poplatníka z položky CN certifikátu.</summary>
    public string Eic { get; }

    /// <summary>Konec platnosti certifikátu v UTC.</summary>
    public DateTimeOffset NotAfter { get; }

    internal X509Certificate2 Certificate { get; }

    internal RSA PrivateKey { get; }

    /// <summary>Načte certifikát z obsahu souboru .p12.</summary>
    /// <param name="data">Obsah souboru .p12 (PKCS#12).</param>
    /// <param name="password">Heslo k souboru.</param>
    /// <exception cref="EetCertificateException">Soubor nejde otevřít nebo neobsahuje klíč RSA.</exception>
    public static EetCertificate FromPkcs12(byte[] data, string password)
    {
        Guard.NotNull(data, nameof(data));
        Guard.NotNull(password, nameof(password));

        X509Certificate2 certificate;
        try
        {
            certificate = LoadPkcs12(data, password);
        }
        catch (CryptographicException ex)
        {
            throw new EetCertificateException(LoadFailedMessage, ex);
        }

        var privateKey = certificate.GetRSAPrivateKey();
        if (privateKey is null)
        {
            certificate.Dispose();
            throw new EetCertificateException("Certifikát .p12 neobsahuje privátní klíč RSA.");
        }

        return new EetCertificate(certificate, privateKey);
    }

    /// <summary>Načte certifikát ze souboru .p12.</summary>
    /// <param name="path">Cesta k souboru .p12.</param>
    /// <param name="password">Heslo k souboru.</param>
    /// <exception cref="EetCertificateException">Soubor nejde otevřít nebo neobsahuje klíč RSA.</exception>
    public static EetCertificate FromPkcs12File(string path, string password)
    {
        Guard.NotNull(path, nameof(path));

        return FromPkcs12(File.ReadAllBytes(path), password);
    }

    /// <summary>Vrátí varování k použití certifikátu pro daného poplatníka v daném okamžiku.</summary>
    /// <param name="eicPopl">EIČ poplatníka z datové zprávy.</param>
    /// <param name="now">Aktuální čas.</param>
    public IReadOnlyList<string> GetWarnings(string eicPopl, DateTimeOffset now)
    {
        var warnings = new List<string>();

        if (!string.Equals(Eic, eicPopl, StringComparison.Ordinal))
        {
            warnings.Add($"EIČ v certifikátu ({Eic}) se neshoduje s eic_popl ({eicPopl}).");
        }

        if (now > NotAfter)
        {
            warnings.Add(string.Format(
                CultureInfo.InvariantCulture,
                "Platnost certifikátu skončila {0:yyyy-MM-dd HH:mm:ss} UTC.",
                NotAfter.UtcDateTime));
        }

        return warnings;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        PrivateKey.Dispose();
        Certificate.Dispose();
    }

    private static X509Certificate2 LoadPkcs12(byte[] data, string password)
    {
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(data, password, X509KeyStorageFlags.EphemeralKeySet);
#elif NETSTANDARD2_0
        // netstandard2.0 has no EphemeralKeySet; the key may be persisted until the certificate is disposed.
        return new X509Certificate2(data, password, X509KeyStorageFlags.DefaultKeySet);
#else
        return new X509Certificate2(data, password, X509KeyStorageFlags.EphemeralKeySet);
#endif
    }
}
