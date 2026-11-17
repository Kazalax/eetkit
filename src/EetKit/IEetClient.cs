namespace EetKit;

/// <summary>
/// Kontrola a odeslání tržby do EET 2.0. Implementuje ho <see cref="EetClient"/>; v testech kódu,
/// který EetKit používá, ho jde nahradit vlastní implementací s výsledky z <see cref="EetModelFactory"/>.
/// </summary>
public interface IEetClient
{
    /// <summary>Zkontroluje data tržby stejně jako <see cref="SendAsync"/>, ale nic neodešle.</summary>
    /// <param name="trzba">Údaje o tržbě.</param>
    /// <returns>Chyby, kvůli kterým by se tržba neodeslala, a místní varování k datům tržby.</returns>
    EetValidationResult Validate(Trzba trzba);

    /// <summary>
    /// Zkontroluje, podepíše a odešle tržbu. Každé volání je nová zpráva s novým <c>uuid_zpravy</c>.
    /// </summary>
    /// <param name="trzba">Údaje o tržbě.</param>
    /// <param name="options">První zaslání nebo opakování, ověřovací režim.</param>
    /// <param name="cancellationToken">Zrušení volajícím vyhodí <see cref="OperationCanceledException"/>.</param>
    /// <exception cref="EetValidationException">Data tržby nejsou platná. Nic se neodeslalo.</exception>
    Task<EetResult> SendAsync(Trzba trzba, SendOptions? options = null, CancellationToken cancellationToken = default);
}
