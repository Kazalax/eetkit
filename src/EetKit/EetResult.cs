namespace EetKit;

/// <summary>Výsledek odeslání tržby do EET.</summary>
public sealed class EetResult
{
    internal EetResult(EetStatus status, Guid messageUuid, IReadOnlyList<string> localWarnings)
    {
        Status = status;
        MessageUuid = messageUuid;
        LocalWarnings = localWarnings;
    }

    /// <summary>Jak odeslání dopadlo.</summary>
    public EetStatus Status { get; }

    /// <summary>Potvrzovací kód POK, který patří na účtenku. Jen u <see cref="EetStatus.Confirmed"/>.</summary>
    public string? Pok { get; internal init; }

    /// <summary>Kód chyby ze serveru (<c>kod</c>), u chyb přenosu prázdný.</summary>
    public int? ErrorCode { get; internal init; }

    /// <summary>Popis chyby ze serveru nebo z přenosu.</summary>
    public string? ErrorText { get; internal init; }

    /// <summary>Varování serveru (0 až 10).</summary>
    public IReadOnlyList<EetWarning> Warnings { get; internal init; } = [];

    /// <summary>Varování zjištěná knihovnou před odesláním (data tržby, certifikát).</summary>
    public IReadOnlyList<string> LocalWarnings { get; }

    /// <summary><c>uuid_zpravy</c> odeslané zprávy.</summary>
    public Guid MessageUuid { get; }

    /// <summary>Čas přijetí zprávy serverem (<c>dat_prij</c>).</summary>
    public DateTimeOffset? DatPrij { get; internal init; }

    /// <summary>Čas odmítnutí zprávy serverem (<c>dat_odmit</c>).</summary>
    public DateTimeOffset? DatOdmit { get; internal init; }

    /// <summary>Odpověď pochází z neprodukčního prostředí (<c>test</c>).</summary>
    public bool IsTest { get; internal init; }

    /// <summary>Hodnota hlavičky <c>X-Global-Transaction-Id</c>, užitečná při řešení problémů s podporou.</summary>
    public string? GlobalTransactionId { get; internal init; }

    /// <summary>Tržbu je potřeba poslat znovu.</summary>
    public bool IsRetryable => Status == EetStatus.TemporaryError;
}
