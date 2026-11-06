namespace EetKit;

/// <summary>Výsledek jednoho odeslání tržby.</summary>
public enum EetStatus
{
    /// <summary>Server tržbu přijal a vrátil POK.</summary>
    Confirmed,

    /// <summary>Ověřovací režim: server zprávu zkontroloval a vrátil kód 0, POK nevydal.</summary>
    VerifiedOnly,

    /// <summary>Doručení je nejisté nebo server dočasně nefunguje. Tržbu je potřeba poslat znovu s <c>PrvniZaslani = false</c>.</summary>
    TemporaryError,

    /// <summary>Server zprávu odmítl. Opakování nepomůže, je potřeba opravit data nebo certifikát.</summary>
    Rejected,
}
