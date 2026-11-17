namespace EetKit;

/// <summary>Výsledek kontroly dat tržby před odesláním.</summary>
/// <param name="Errors">Chyby podle schématu XSD. Tržba s chybou se neodešle, server by ji odmítl.</param>
/// <param name="Warnings">
/// Místní varování podle propustných kontrol serveru (například neobvyklý tvar <c>id_jednotky</c>), POK se přesto vydá.
/// </param>
public sealed record EetValidationResult(IReadOnlyList<EetValidationError> Errors, IReadOnlyList<string> Warnings)
{
    /// <summary>Tržba nemá žádnou chybu a dá se odeslat.</summary>
    public bool IsValid => Errors.Count == 0;
}
