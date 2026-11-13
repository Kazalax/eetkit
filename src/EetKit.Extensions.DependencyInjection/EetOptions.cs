namespace EetKit.Extensions.DependencyInjection;

/// <summary>Nastavení EET pro <see cref="EetServiceCollectionExtensions.AddEet"/>.</summary>
public sealed class EetOptions
{
    /// <summary>Prostředí. Výchozí je playground, produkci je potřeba zvolit výslovně.</summary>
    public EetEnvironment Environment { get; set; } = EetEnvironment.Playground;

    /// <summary>Cesta k souboru .p12. Buď tohle, nebo <see cref="CertificateBytes"/>.</summary>
    public string? CertificatePath { get; set; }

    /// <summary>Obsah souboru .p12. Buď tohle, nebo <see cref="CertificatePath"/>.</summary>
    public byte[]? CertificateBytes { get; set; }

    /// <summary>Heslo k .p12. Nikdy ho nedávejte do zdrojového kódu, použijte tajné nastavení.</summary>
    public string? CertificatePassword { get; set; }

    /// <summary>Timeout jednoho odeslání, nejméně 2 s.</summary>
    public TimeSpan Timeout { get; set; } = EetClientOptions.DefaultTimeout;
}
