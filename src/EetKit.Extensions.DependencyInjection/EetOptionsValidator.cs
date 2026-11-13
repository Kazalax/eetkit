using Microsoft.Extensions.Options;

namespace EetKit.Extensions.DependencyInjection;

/// <summary>
/// Checks the configuration at startup, before the first sale is sent. Without a custom provider
/// it also opens the certificate, so a wrong path or password stops the application at start.
/// </summary>
internal sealed class EetOptionsValidator(Func<bool> hasCustomCertificateProvider) : IValidateOptions<EetOptions>
{
    public ValidateOptionsResult Validate(string? name, EetOptions options)
    {
        var failures = new List<string>();

        if (options.Timeout < EetClientOptions.MinimumTimeout)
        {
            failures.Add("Eet: Timeout musí být aspoň 2 sekundy.");
        }

        if (!hasCustomCertificateProvider())
        {
            ValidateCertificate(options, failures);
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateCertificate(EetOptions options, List<string> failures)
    {
        var hasPath = !string.IsNullOrWhiteSpace(options.CertificatePath);
        var hasBytes = options.CertificateBytes is { Length: > 0 };
        if (hasPath == hasBytes)
        {
            failures.Add("Eet: nastavte právě jedno z CertificatePath a CertificateBytes, nebo zaregistrujte vlastní IEetCertificateProvider.");
            return;
        }

        if (options.CertificatePassword is null)
        {
            failures.Add("Eet: chybí CertificatePassword.");
            return;
        }

        try
        {
            using var certificate = OptionsCertificateProvider.Load(options);
        }
        catch (EetCertificateException ex)
        {
            // The message never contains the password (see EetCertificate).
            failures.Add("Eet: " + ex.Message);
        }
    }
}
