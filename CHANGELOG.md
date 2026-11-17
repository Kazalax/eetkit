# Changelog

Formát podle [Keep a Changelog](https://keepachangelog.com/cs/1.1.0/), verze podle [SemVer](https://semver.org/lang/cs/).

## [Unreleased]

## [0.2.0] - 2026-11-17

### Přidáno

- Rozhraní `IEetClient` (`Validate` a `SendAsync`), které `EetClient` implementuje. `AddEet` ho registruje jako stejnou sdílenou instanci jako `EetClient`.
- `EetClient.Validate`: kontrola dat tržby bez odeslání, výsledek `EetValidationResult` s chybami a místními varováními.
- `EetClient.FromPkcs12(byte[], heslo, prostředí)`: klient s certifikátem z obsahu souboru .p12, například z databáze.
- `EetModelFactory.Result(...)`: vytvoří `EetResult` s libovolnými hodnotami pro testy kódu, který EetKit používá.

## [0.1.0] - 2026-11-16

### Přidáno

- `EetClient.SendAsync`: validace, převod na český čas, podpis WS-Security, odeslání na playground nebo produkci, výsledek `EetResult` s POK, kódem chyby a varováními.
- Načtení certifikátu .p12 od CA EET (starý formát RC2-40 a 3DES) na Windows i Linuxu.
- Ověření podpisu odpovědi: jen certifikát Generálního finančního ředitelství od I.CA, bez stahování čehokoli při ověřování.
- Třídění odpovědí na `Confirmed`, `VerifiedOnly`, `TemporaryError` a `Rejected`.
- `EetKit.Extensions.DependencyInjection`: `services.AddEet(...)` s kontrolou nastavení při startu.
- Ukázková aplikace pro ASP.NET Core, testy proti playgroundu (každou noc v CI).
- Testy na .NET Framework 4.8 (Windows), včetně odeslání na playground.
- Vydávání na NuGet z GitHub Actions přes Trusted Publishing, bez uloženého API klíče.
