namespace EetKit;

/// <summary>Chyba jedné položky tržby zjištěná před odesláním.</summary>
/// <param name="Field">XML jméno položky, například <c>eic_popl</c>.</param>
/// <param name="Message">Popis chyby.</param>
public sealed record EetValidationError(string Field, string Message);
