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
    private const int ExpectedChainLength = 3;

    private static readonly Lazy<X509Certificate2> RootCertificate = new(() => LoadResource(RootResource));
    private static readonly Lazy<X509Certificate2> IssuerCertificate = new(() => LoadResource(IssuerResource));

    /// <summary>I.CA Root CA/RSA 05/2022.</summary>
    public static X509Certificate2 Root => RootCertificate.Value;

    /// <summary>I.CA Public CA/RSA 06/2022, issuer of the GFŘ signing certificates.</summary>
    public static X509Certificate2 Issuer => IssuerCertificate.Value;

    public static bool IsTrusted(X509Certificate2 certificate, DateTimeOffset now)
    {
        Guard.NotNull(certificate, nameof(certificate));

        // Cheap local checks first: a certificate from any other issuer never reaches chain
        // building, so nothing it names (AIA URLs) can make the library go to the network.
        return HasRequiredOrganization(certificate)
            && AllowsDigitalSignature(certificate)
            && IssuedByBundledIssuer(certificate)
            && ChainsToBundledRoot(certificate, now);
    }

    private static bool AllowsDigitalSignature(X509Certificate2 certificate) =>
        certificate.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault() is { } keyUsage
        && (keyUsage.KeyUsages & X509KeyUsageFlags.DigitalSignature) != 0;

    private static bool IssuedByBundledIssuer(X509Certificate2 certificate) =>
        certificate.IssuerName.RawData.SequenceEqual(Issuer.SubjectName.RawData);

    // Exactly signer, bundled issuer, bundled root; no other path is accepted.
    private static bool HasExpectedShape(X509Chain chain) =>
        chain.ChainElements.Count == ExpectedChainLength
        && SameCertificate(chain.ChainElements[1].Certificate, Issuer)
        && SameCertificate(chain.ChainElements[2].Certificate, Root);

    private static bool SameCertificate(X509Certificate2 left, X509Certificate2 right) =>
        left.RawData.SequenceEqual(right.RawData);

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
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(Root);
        return chain.Build(certificate) && HasExpectedShape(chain);
#else
        // netstandard2.0 has neither a custom trust store nor a switch for downloads. Both bundled
        // certificates are supplied, so nothing needs downloading; the root is unknown to the
        // system, so exactly that one problem is allowed and the chain shape is checked instead.
        chain.ChainPolicy.ExtraStore.Add(Root);
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        return chain.Build(certificate)
            && HasExpectedShape(chain)
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
