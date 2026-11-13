using EetKit.Extensions.DependencyInjection;
using EetKit.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace EetKit.Tests;

public sealed class DependencyInjectionTests
{
    private const string Eic = "CZ00000019";

    private static ServiceProvider Build(Action<EetOptions> configure, Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddEet(configure);
        extra?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static void TestCertificate(EetOptions options)
    {
        var (p12, password) = SpecFiles.TestCertificate(Eic);
        options.CertificateBytes = p12;
        options.CertificatePassword = password;
    }

    [Fact]
    public void AddEet_ResolvesClientAndCertificate()
    {
        using var provider = Build(TestCertificate);

        Assert.NotNull(provider.GetRequiredService<EetClient>());
        Assert.Equal(Eic, provider.GetRequiredService<IEetCertificateProvider>().GetCertificate().Eic);
    }

    [Fact]
    public void MissingCertificate_FailsValidation()
    {
        using var provider = Build(o => o.CertificatePassword = "heslo");

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EetOptions>>().Value);
    }

    [Fact]
    public void MissingPassword_FailsValidation()
    {
        using var provider = Build(o => o.CertificatePath = "/tmp/neexistuje.p12");

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EetOptions>>().Value);
    }

    [Fact]
    public void TimeoutTooShort_FailsValidation()
    {
        using var provider = Build(o =>
        {
            TestCertificate(o);
            o.Timeout = TimeSpan.FromSeconds(1);
        });

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EetOptions>>().Value);
    }

    [Fact]
    public void CustomCertificateProvider_UsedWithoutCertificateOptions()
    {
        var (p12, password) = SpecFiles.TestCertificate(Eic);
        using var certificate = EetCertificate.FromPkcs12(p12, password);
        var custom = new CustomProvider(certificate);

        using var provider = Build(_ => { }, services => services.AddSingleton<IEetCertificateProvider>(custom));

        Assert.Same(custom, provider.GetRequiredService<IEetCertificateProvider>());
        Assert.NotNull(provider.GetRequiredService<IOptions<EetOptions>>().Value);
    }

    [Fact]
    public async Task Client_UsesHttpClientFromFactory()
    {
        var handler = StubHttpHandler.Returning(Fixtures.Bytes(Fixtures.Chyba4));
        using var provider = Build(TestCertificate, services =>
            services.AddHttpClient(EetServiceCollectionExtensions.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler));

        var client = provider.GetRequiredService<EetClient>();
        await client.SendAsync(SampleRequest.Load(Eic).Trzba with { DatTrzby = DateTimeOffset.UtcNow }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(handler.Requests);
    }

    private sealed class CustomProvider(EetCertificate certificate) : IEetCertificateProvider
    {
        public EetCertificate GetCertificate() => certificate;
    }
}
