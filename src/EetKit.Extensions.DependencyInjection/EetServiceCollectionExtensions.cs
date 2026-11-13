using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EetKit.Extensions.DependencyInjection;

/// <summary>Registrace EET do kontejneru služeb.</summary>
public static class EetServiceCollectionExtensions
{
    /// <summary>Jméno HTTP klienta v <c>IHttpClientFactory</c>, pro vlastní nastavení handleru.</summary>
    public const string HttpClientName = "EetKit";

    /// <summary>
    /// Zaregistruje <see cref="EetClient"/>. Certifikát se vezme z <see cref="EetOptions"/>,
    /// nebo z vlastního <see cref="IEetCertificateProvider"/>, pokud je zaregistrovaný.
    /// Nastavení se kontroluje při startu aplikace.
    /// </summary>
    public static IServiceCollection AddEet(this IServiceCollection services, Action<EetOptions> configure)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
#else
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }
#endif

        services.AddOptions<EetOptions>().Configure(configure).ValidateOnStart();
        services.AddSingleton<IValidateOptions<EetOptions>>(sp =>
            new EetOptionsValidator(HasCustomCertificateProvider(sp)));
        services.TryAddSingleton<OptionsCertificateProvider>();
        services.TryAddSingleton<IEetCertificateProvider>(sp => sp.GetRequiredService<OptionsCertificateProvider>());

        // IHttpClientFactory recycles handlers (2 min by default), so DNS changes are picked up.
        services.AddHttpClient(HttpClientName);
        services.AddTransient(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EetOptions>>().Value;
            return new EetClient(
                sp.GetRequiredService<IEetCertificateProvider>(),
                new EetClientOptions { Environment = options.Environment, Timeout = options.Timeout },
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName),
                sp.GetService<TimeProvider>());
        });

        return services;
    }

    private static bool HasCustomCertificateProvider(IServiceProvider services) =>
        services.GetRequiredService<IEetCertificateProvider>() is not OptionsCertificateProvider;
}
