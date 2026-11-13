namespace EetKit;

/// <summary>Nastavení klienta EET.</summary>
public sealed class EetClientOptions
{
    /// <summary>Nejkratší povolený timeout. Seminář pro vývojáře uvádí mezní dobu odezvy aspoň 2 s.</summary>
    public static readonly TimeSpan MinimumTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Výchozí timeout jednoho odeslání.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Prostředí, do kterého se tržby posílají. Výchozí je playground.</summary>
    public EetEnvironment Environment { get; set; } = EetEnvironment.Playground;

    /// <summary>Jak dlouho čekat na odpověď serveru. Po uplynutí je výsledek <see cref="EetStatus.TemporaryError"/>.</summary>
    public TimeSpan Timeout { get; set; } = DefaultTimeout;
}
