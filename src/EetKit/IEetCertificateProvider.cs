namespace EetKit;

/// <summary>
/// Zdroj certifikátu poplatníka. Klient se na certifikát ptá při každém odeslání,
/// takže nový certifikát (například po obnově) se použije bez restartu.
/// </summary>
public interface IEetCertificateProvider
{
    /// <summary>Vrátí certifikát, kterým se podepíše další zpráva. Volající ho neuvolňuje.</summary>
    EetCertificate GetCertificate();
}
