namespace EetKit;

/// <summary>Varování serveru o propustné chybě (POK se přesto vydá).</summary>
/// <param name="Code">Kód varování (<c>kod_varov</c>), viz Seznam kódů a textů varování v popisu rozhraní.</param>
/// <param name="Text">Text varování ze serveru (bez diakritiky).</param>
public sealed record EetWarning(int Code, string Text);
