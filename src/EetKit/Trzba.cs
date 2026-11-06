namespace EetKit;

/// <summary>
/// Údaje o evidované tržbě. Jména odpovídají položkám datové zprávy
/// (viz Popis položek datové zprávy).
/// </summary>
public sealed record Trzba
{
    /// <summary>EIČ poplatníka (<c>eic_popl</c>), formát CZ a 8 až 10 číslic.</summary>
    public required string EicPopl { get; init; }

    /// <summary>EIČ pověřujícího poplatníka (<c>eic_poverujiciho</c>), nepovinné.</summary>
    public string? EicPoverujiciho { get; init; }

    /// <summary>Pověření více poplatníky (<c>povereni_vice_popl</c>), nepovinné.</summary>
    public bool? PovereniVicePopl { get; init; }

    /// <summary>Označení evidenční jednotky (<c>id_jednotky</c>), 1 až 999999999.</summary>
    public required int IdJednotky { get; init; }

    /// <summary>Označení pokladního zařízení (<c>id_pokl</c>), 1 až 20 povolených znaků.</summary>
    public required string IdPokl { get; init; }

    /// <summary>Pořadové číslo účtenky (<c>porad_cis</c>), 1 až 25 povolených znaků.</summary>
    public required string PoradCis { get; init; }

    /// <summary>Datum a čas uskutečnění tržby (<c>dat_trzby</c>).</summary>
    public required DateTimeOffset DatTrzby { get; init; }

    /// <summary>Celková částka tržby (<c>celk_trzba</c>), nejvýš 2 desetinná místa.</summary>
    public required decimal CelkTrzba { get; init; }

    /// <summary>Částka určená k následnému čerpání nebo zúčtování (<c>urceno_cerp_zuct</c>), nepovinné.</summary>
    public decimal? UrcenoCerpZuct { get; init; }

    /// <summary>Částka následného čerpání nebo zúčtování (<c>cerp_zuct</c>), nepovinné.</summary>
    public decimal? CerpZuct { get; init; }
}
