using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EetKit.Extensions.DependencyInjection;

/// <summary>Loads the certificate from <see cref="EetOptions"/> on first use and keeps it.</summary>
/// <remarks>
/// Takes the service provider instead of <c>IOptions&lt;EetOptions&gt;</c> so that it can be created
/// while the options are being built. A failed load is not remembered: the next sale tries again,
/// for example once a mounted secret appears.
/// </remarks>
internal sealed class OptionsCertificateProvider(IServiceProvider services) : IEetCertificateProvider, IDisposable
{
    private readonly object _lock = new();
    private EetCertificate? _certificate;

    public EetCertificate GetCertificate()
    {
        lock (_lock)
        {
            return _certificate ??= Load(services.GetRequiredService<IOptions<EetOptions>>().Value);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _certificate?.Dispose();
            _certificate = null;
        }
    }

    internal static EetCertificate Load(EetOptions options)
    {
        var password = options.CertificatePassword ?? string.Empty;
        return options.CertificateBytes is { Length: > 0 } bytes
            ? EetCertificate.FromPkcs12(bytes, password)
            : EetCertificate.FromPkcs12File(options.CertificatePath!, password);
    }
}
