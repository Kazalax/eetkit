namespace EetKit;

/// <summary>Prostředí systému EET, do kterého se tržby posílají.</summary>
public enum EetEnvironment
{
    /// <summary>Neprodukční testovací prostředí s testovacími certifikáty.</summary>
    Playground,

    /// <summary>Ostrý provoz.</summary>
    Production,
}
