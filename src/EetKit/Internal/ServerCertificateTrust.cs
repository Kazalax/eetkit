using System.Security.Cryptography.X509Certificates;

namespace EetKit.Internal;

/// <summary>
/// Decides whether a certificate may sign EET responses: it must chain to the I.CA root bundled
/// with the library (via the bundled I.CA Public CA/RSA 06/2022) and belong to Generální finanční
/// ředitelství. Revocation is not checked, see plan M2 decision 4.
/// </summary>
internal static class ServerCertificateTrust
{
    private const string RequiredOrganization = "O=Generální finanční ředitelství";
    private const string RootResource = "EetKit.Certificates.ica-root-rsa-2022.cer";
    private const string IssuerResource = "EetKit.Certificates.ica-public-rsa-2022.cer";

    private static readonly Lazy<X509Certificate2> RootCertificate = new(() => LoadResource(RootResource));
    private static readonly Lazy<X509Certificate2> IssuerCertificate = new(() => LoadResource(IssuerResource));

    /// <summary>I.CA Root CA/RSA 05/2022.</summary>
    public static X509Certificate2 Root => RootCertificate.Value;

    /// <summary>I.CA Public CA/RSA 06/2022, issuer of the GFŘ signing certificates.</summary>
    public static X509Certificate2 Issuer => IssuerCertificate.Value;

    public static bool IsTrusted(X509Certificate2 certificate, DateTimeOffset now)
    {
        Guard.NotNull(certificate, nameof(certificate));

        return HasRequiredOrganization(certificate) && ChainsToBundledRoot(certificate, now);
    }

    private static bool HasRequiredOrganization(X509Certificate2 certificate) =>
        certificate.SubjectName.Decode(X500DistinguishedNameFlags.UseNewLines)
            .Split('\n')
            .Any(part => string.Equals(part.Trim(), RequiredOrganization, StringComparison.Ordinal));

    private static bool ChainsToBundledRoot(X509Certificate2 certificate, DateTimeOffset now)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationTime = now.UtcDateTime;
        chain.ChainPolicy.ExtraStore.Add(Issuer);

#if NET5_0_OR_GREATER
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(Root);
        return chain.Build(certificate);
#else
        // Without a custom trust store the root is unknown to the system: allow exactly that one
        // problem and then require that the chain really ends in the bundled root.
        chain.ChainPolicy.ExtraStore.Add(Root);
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        if (!chain.Build(certificate))
        {
            return false;
        }

        var last = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
        return string.Equals(last.Thumbprint, Root.Thumbprint, StringComparison.OrdinalIgnoreCase)
            && chain.ChainStatus.All(status =>
                status.Status is X509ChainStatusFlags.NoError or X509ChainStatusFlags.UntrustedRoot);
#endif
    }

    private static X509Certificate2 LoadResource(string name)
    {
        using var stream = typeof(ServerCertificateTrust).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded certificate {name} is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadCertificate(buffer.ToArray());
#else
        return new X509Certificate2(buffer.ToArray());
#endif
    }
}
