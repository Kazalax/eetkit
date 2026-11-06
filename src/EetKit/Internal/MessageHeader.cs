namespace EetKit.Internal;

/// <summary>Values of the Hlavicka element of one sent message.</summary>
internal sealed record MessageHeader(Guid UuidZpravy, DateTimeOffset DatOdesl, bool PrvniZaslani, bool Overeni);
