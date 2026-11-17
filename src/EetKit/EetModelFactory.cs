namespace EetKit;

/// <summary>
/// Vytváří výsledky EetKit s libovolnými hodnotami, pro testy kódu, který EetKit používá
/// (například falešný <see cref="IEetClient"/>). Skutečné výsledky vrací jen <see cref="EetClient"/>.
/// </summary>
public static class EetModelFactory
{
    /// <summary>Vytvoří <see cref="EetResult"/> se zadanými hodnotami.</summary>
    /// <param name="status">Jak odeslání dopadlo.</param>
    /// <param name="pok">Potvrzovací kód POK.</param>
    /// <param name="errorCode">Kód chyby ze serveru.</param>
    /// <param name="errorText">Popis chyby.</param>
    /// <param name="warnings">Varování serveru, výchozí žádná.</param>
    /// <param name="localWarnings">Místní varování, výchozí žádná.</param>
    /// <param name="messageUuid"><c>uuid_zpravy</c>, výchozí je nový náhodný.</param>
    /// <param name="datPrij">Čas přijetí zprávy serverem.</param>
    /// <param name="datOdmit">Čas odmítnutí zprávy serverem.</param>
    /// <param name="isTest">Odpověď z neprodukčního prostředí.</param>
    /// <param name="globalTransactionId">Hodnota hlavičky <c>X-Global-Transaction-Id</c>.</param>
    public static EetResult Result(
        EetStatus status,
        string? pok = null,
        int? errorCode = null,
        string? errorText = null,
        IReadOnlyList<EetWarning>? warnings = null,
        IReadOnlyList<string>? localWarnings = null,
        Guid? messageUuid = null,
        DateTimeOffset? datPrij = null,
        DateTimeOffset? datOdmit = null,
        bool isTest = false,
        string? globalTransactionId = null) =>
        new(status, messageUuid ?? Guid.NewGuid(), localWarnings ?? [])
        {
            Pok = pok,
            ErrorCode = errorCode,
            ErrorText = errorText,
            Warnings = warnings ?? [],
            DatPrij = datPrij,
            DatOdmit = datOdmit,
            IsTest = isTest,
            GlobalTransactionId = globalTransactionId,
        };
}
