using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EetKit.Extensions.DependencyInjection;

/// <summary>Registrace EET do kontejneru služeb.</summary>
public static class EetServiceCollectionExtensions
{
    /// <summary>Jméno HTTP klienta v <c>IHttpClientFactory</c>, pro vlastní nastavení handleru.</summary>
    public const string HttpClientName = "EetKit";

    // Production balances by DNS; pooled connections are renewed so new ones ask DNS again.
    private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Zaregistruje <see cref="EetClient"/> jako jednu sdílenou instanci, dostupnou i jako <see cref="IEetClient"/>.
    /// Certifikát se vezme z <see cref="EetOptions"/>, nebo z vlastního <see cref="IEetCertificateProvider"/>, pokud je
    /// zaregistrovaný (před voláním i po něm). Nastavení i certifikát se zkontrolují při startu aplikace.
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

        var defaultProvider = ServiceDescriptor.Singleton<IEetCertificateProvider>(
            sp => sp.GetRequiredService<OptionsCertificateProvider>());

        services.AddOptions<EetOptions>().Configure(configure).ValidateOnStart();

        // Whether a custom provider exists is read from the registrations, never by resolving it:
        // a provider that depends on the options would otherwise deadlock their creation.
        services.AddSingleton<IValidateOptions<EetOptions>>(
            new EetOptionsValidator(() => !IsLastProviderRegistration(services, defaultProvider)));
        services.TryAddSingleton<OptionsCertificateProvider>();
        services.TryAdd(defaultProvider);

#if NET5_0_OR_GREATER
        services.AddHttpClient(HttpClientName).ConfigurePrimaryHttpMessageHandler(() =>
            new SocketsHttpHandler { PooledConnectionLifetime = PooledConnectionLifetime });
#else
        // TODO: on .NET Framework DNS refresh relies on ServicePointManager defaults; verify once net48 tests exist.
        services.AddHttpClient(HttpClientName);
#endif

        services.TryAddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EetOptions>>().Value;
            return new EetClient(
                sp.GetRequiredService<IEetCertificateProvider>(),
                new EetClientOptions { Environment = options.Environment, Timeout = options.Timeout },
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName),
                sp.GetService<TimeProvider>());
        });
        services.TryAddSingleton<IEetClient>(sp => sp.GetRequiredService<EetClient>());

        return services;
    }

    private static bool IsLastProviderRegistration(IServiceCollection services, ServiceDescriptor descriptor) =>
        ReferenceEquals(services.LastOrDefault(d => d.ServiceType == typeof(IEetCertificateProvider)), descriptor);
}
