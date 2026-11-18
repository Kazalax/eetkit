# EetKit: EET 2.0 pro .NET a C#

[![CI](https://github.com/Kazalax/eetkit/actions/workflows/ci.yml/badge.svg)](https://github.com/Kazalax/eetkit/actions/workflows/ci.yml)
[![Ověřeno proti playgroundu](https://github.com/Kazalax/eetkit/actions/workflows/playground.yml/badge.svg)](https://github.com/Kazalax/eetkit/actions/workflows/playground.yml)

EetKit je open-source knihovna pro C# a .NET, která odesílá tržby do elektronické evidence tržeb EET 2.0 (datové rozhraní v4.1, povinnost evidovat platí od 1. 1. 2027, leden je pilotní provoz). Zkontroluje data tržby, sestaví a podepíše datovou zprávu (SOAP, WS-Security), pošle ji Finanční správě ČR, ověří podpis odpovědi a vrátí výsledek s kódem POK pro účtenku. Je určená pro vývojáře pokladních systémů, e-shopů a dalších aplikací, které tržby evidují.

[English version](https://github.com/Kazalax/eetkit/blob/main/README.en.md)

> Není oficiálním produktem Finanční správy ČR. Za splnění evidenční povinnosti odpovídá poplatník.

## Instalace

```bash
dotnet add package EetKit
```

Pro ASP.NET Core a další aplikace s DI je tu ještě `EetKit.Extensions.DependencyInjection`.

| Platforma | Stav |
| --- | --- |
| .NET 8 a .NET 10 | testováno na Linuxu i Windows |
| .NET Framework 4.7.2 a novější | testováno na .NET Framework 4.8 (Windows), používá cíl `netstandard2.0` |
| ostatní verze .NET (6, 7, 9, .NET Core 3.1) | měly by běžet přes cíl `netstandard2.0` nebo `net8.0`, netestováno |

Automatické testy běží při každé změně na všech testovaných platformách. Odeslání na skutečný playground finanční správy běží každou noc na .NET 10 (Linux) a .NET Framework 4.8 (Windows).

## Příklad

```csharp
using var client = EetClient.FromPkcs12("certifikat.p12", heslo, EetEnvironment.Playground);

var trzba = new Trzba("CZ00000019", idJednotky: 303, idPokl: "POKLADNA/1", poradCis: "2027/0001",
    datTrzby: DateTimeOffset.Now, celkTrzba: 65.00m);

var vysledek = await client.SendAsync(trzba);
if (vysledek.Status == EetStatus.Confirmed)
    Console.WriteLine($"POK na účtenku: {vysledek.Pok}");
else if (vysledek.IsRetryable)
    Console.WriteLine("Poslat znovu později s new SendOptions(PrvniZaslani: false)");
```

Certifikát uložený jinde než v souboru, třeba v databázi, načte `EetClient.FromPkcs12(bajty, heslo, EetEnvironment.Playground)`.

## Výsledek odeslání

| `Status` | Co to znamená | Co dál |
| --- | --- | --- |
| `Confirmed` | Server tržbu přijal | POK z `Pok` patří na účtenku |
| `VerifiedOnly` | Ověřovací režim (`Overeni = true`), kód 0 | Zpráva je v pořádku, POK se nevydává |
| `TemporaryError` | Timeout, výpadek sítě, chyba serveru (kódy -1 a 8), jakákoli chyba HTTP, odpověď bez platného podpisu | Poslat znovu s `PrvniZaslani = false`, i když zprávu server možná dostal (tak to předepisuje popis položek datové zprávy). Opakované zaslání může vrátit jiný POK |
| `Rejected` | Server zprávu odmítl (kódy 2 až 7 a neznámé kladné kódy) | Opravit data nebo certifikát, opakování nepomůže |

Neplatná data se nikdy neodešlou: `SendAsync` vyhodí `EetValidationException` se seznamem polí. Místní varování (například neobvyklý tvar `id_jednotky`, prošlý certifikát nebo EIČ, které nesedí s certifikátem) jsou v `LocalWarnings`, varování serveru ve `Warnings`.

## Playground

Testovací prostředí finanční správy funguje stejně jako ostré, jen se nic neeviduje. Testovací certifikáty pro tři EIČ (`CZ00000019`, `CZ683555118`, `CZ8551015704`) i heslo k nim jsou v archivu [CAEET_Playground_2026_v1.zip](https://github.com/Kazalax/eetkit/blob/main/docs/spec/CAEET_Playground_2026_v1.zip). POK z playgroundu končí `-ff` a `IsTest` je `true`.

Ukázková aplikace pošle tržbu na playground jedním tlačítkem:

```bash
dotnet run --project samples/EetKit.Sample.AspNetCore
```

Pak otevřete http://localhost:5080.

Pro pokladny na .NET Frameworku je tu ukázka ve WinForms (spustí se jen na Windows):

```bash
dotnet run --project samples/EetKit.Sample.WinForms
```

## ASP.NET Core

```csharp
builder.Services.AddEet(o =>
{
    o.Environment = EetEnvironment.Production;
    o.CertificatePath = builder.Configuration["Eet:CertificatePath"];
    o.CertificatePassword = builder.Configuration["Eet:CertificatePassword"];
});
```

`AddEet` zaregistruje `EetClient` jako jednu sdílenou instanci, kterou si necháte předat do konstruktoru. Při startu aplikace se zkontroluje nastavení a certifikát se zkusí otevřít, takže špatná cesta nebo heslo zastaví start. Vlastní zdroj certifikátu (například trezor klíčů) zaregistrujte jako `IEetCertificateProvider`.

Klient je zaregistrovaný i jako `IEetClient`. V testech kódu, který tržby posílá, ho nahradíte vlastní implementací a výsledky pro ni vytvoří `EetModelFactory.Result(...)`.

## Co knihovna hlídá

- Podpis přesně podle vzorových zpráv finanční správy (shoda na bajt je ověřená testem).
- Starý formát `.p12`, jak ho vydává CA EET, se načte na Windows i Linuxu bez zásahu do OpenSSL.
- Potvrzení s POK se přijme jen s platným podpisem certifikátu Generálního finančního ředitelství od I.CA, ze správného prostředí a k odeslané zprávě. Chybové odpovědi server nepodepisuje.
- Zpráva nad 12 288 bajtů se neodešle (limit ověřený na playgroundu).
- Heslo, klíč ani obsah certifikátu se nikam nelogují.

## Časté otázky

### Jak odeslat tržbu do EET 2.0 z C#?

Nainstalujte balíček `EetKit`, načtěte pokladní certifikát `.p12` a zavolejte `EetClient.SendAsync` (viz [příklad](#příklad)). Když je výsledek `Confirmed`, vytiskněte na účtenku kód z `Pok`.

### Co dělat, když EET neodpoví nebo vypadne internet?

`SendAsync` počká nejvýš 5 sekund (nastavitelné, minimum 2 s) a vrátí `TemporaryError` s `IsRetryable = true`. Výjimku nevyhazuje. Tržbu pošlete znovu později se `SendOptions(PrvniZaslani: false)`. Každý pokus dostane nové `uuid_zpravy`. Frontu, která by tržby dopsílala sama, knihovna zdarma nemá.

### Kde vzít certifikát?

Pokladní certifikát vydává certifikační autorita CA EET. Žádá se přes daňovou informační schránku DIS+ na portálu MOJE daně. Knihovna načte soubor `.p12` tak, jak ho CA EET vydá, na Windows i Linuxu.

### Jak to vyzkoušet bez ostrého certifikátu?

Na playgroundu, testovacím prostředí finanční správy. Testovací certifikáty jsou v tomto repozitáři (viz [Playground](#playground)) a `EetEnvironment.Playground` je výchozí nastavení.

### Co znamená chyba 4 nebo jiný kód?

Kladné kódy 2 až 7 znamenají, že server zprávu odmítl. Opakování nepomůže, je potřeba opravit data nebo certifikát. Text chyby je v `ErrorText`, kód v `ErrorCode`. Kódy -1 a 8 jsou dočasné (`TemporaryError`). Přehled je v tabulce [Výsledek odeslání](#výsledek-odeslání).

### Podporuje knihovna staré EET (rozhraní v3)?

Ne, jen EET 2.0 s rozhraním v4.1. Staré rozhraní není s EET 2.0 kompatibilní.

### Jak přejít z knihovny Mews.Fiscalizations.Czechia (Mews.Eet)?

Podle [návodu na přechod](https://github.com/Kazalax/eetkit/blob/main/docs/prechod-z-mews-eet.md). Je v něm srovnání kódu před a po, tabulka, co čemu odpovídá, a co v EET 2.0 zmizelo (DPH, BKP, PKP).

### Funguje to na .NET Framework?

Ano, od verze 4.7.2. Automatické testy i odeslání na playground běží na .NET Framework 4.8 (viz [Instalace](#instalace)).

## Články

- [EET 2.0 pro vývojáře: co se změnilo proti starému EET](https://kazalax.github.io/eetkit/clanky/eet2-co-se-zmenilo-pro-vyvojare/)

## Integrace na klíč

Potřebujete EET zapojit do vlastní pokladny, frontu tržeb pro výpadky sítě nebo automatickou obnovu certifikátů? Napište na bedrichmaly79@gmail.com.

## Licence

[MIT](https://github.com/Kazalax/eetkit/blob/main/LICENSE)
