namespace EetKit.Internal;

/// <summary>Always the same certificate, owned and disposed by the client that created it.</summary>
internal sealed class FixedCertificateProvider(EetCertificate certificate) : IEetCertificateProvider, IDisposable
{
    public EetCertificate GetCertificate() => certificate;

    public void Dispose() => certificate.Dispose();
}
