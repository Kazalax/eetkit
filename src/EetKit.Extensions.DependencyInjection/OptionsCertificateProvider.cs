using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EetKit.Extensions.DependencyInjection;

/// <summary>Loads the certificate from <see cref="EetOptions"/> once, on first use.</summary>
/// <remarks>
/// Takes the service provider instead of <c>IOptions&lt;EetOptions&gt;</c>: the options validator
/// resolves this provider while the options are being created, and a constructor dependency on the
/// options would close a cycle that the container does not detect for factories (it deadlocks).
/// </remarks>
internal sealed class OptionsCertificateProvider : IEetCertificateProvider, IDisposable
{
    private readonly Lazy<EetCertificate> _certificate;

    public OptionsCertificateProvider(IServiceProvider services)
    {
        _certificate = new Lazy<EetCertificate>(() => Load(services.GetRequiredService<IOptions<EetOptions>>().Value));
    }

    public EetCertificate GetCertificate() => _certificate.Value;

    public void Dispose()
    {
        if (_certificate.IsValueCreated)
        {
            _certificate.Value.Dispose();
        }
    }

    private static EetCertificate Load(EetOptions options)
    {
        var password = options.CertificatePassword ?? string.Empty;
        return options.CertificateBytes is { Length: > 0 } bytes
            ? EetCertificate.FromPkcs12(bytes, password)
            : EetCertificate.FromPkcs12File(options.CertificatePath!, password);
    }
}
