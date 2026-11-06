namespace EetKit;

/// <summary>Volby jednoho odeslání tržby.</summary>
/// <param name="PrvniZaslani">
/// <c>true</c> při prvním pokusu o odeslání tržby, <c>false</c> při každém opakování (<c>prvni_zaslani</c>).
/// </param>
/// <param name="Overeni">Ověřovací režim (<c>overeni</c>): server zprávu zkontroluje a místo POK vrátí kód 0.</param>
public sealed record SendOptions(bool PrvniZaslani = true, bool Overeni = false);
