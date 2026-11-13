using Microsoft.Extensions.Options;

namespace EetKit.Extensions.DependencyInjection;

/// <summary>Checks the configuration at startup, before the first sale is sent.</summary>
internal sealed class EetOptionsValidator(bool hasCustomCertificateProvider) : IValidateOptions<EetOptions>
{
    public ValidateOptionsResult Validate(string? name, EetOptions options)
    {
        var failures = new List<string>();

        if (options.Timeout < EetClientOptions.MinimumTimeout)
        {
            failures.Add("Eet: Timeout musí být aspoň 2 sekundy.");
        }

        if (!hasCustomCertificateProvider)
        {
            var hasPath = !string.IsNullOrWhiteSpace(options.CertificatePath);
            var hasBytes = options.CertificateBytes is { Length: > 0 };
            if (hasPath == hasBytes)
            {
                failures.Add("Eet: nastavte právě jedno z CertificatePath a CertificateBytes, nebo zaregistrujte vlastní IEetCertificateProvider.");
            }

            if (options.CertificatePassword is null)
            {
                failures.Add("Eet: chybí CertificatePassword.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
