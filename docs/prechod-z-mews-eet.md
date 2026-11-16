# Přechod z Mews.Fiscalizations.Czechia na EetKit

Knihovna [Mews.Fiscalizations.Czechia](https://www.nuget.org/packages/Mews.Fiscalizations.Czechia/) (jmenný prostor `Mews.Eet`) odesílala tržby do původního EET s rozhraním v3. Poslední verze 5.0.2 vyšla v říjnu 2022 a v lednu 2023 Mews projekt ze svého repozitáře odstranil. EET 2.0 používá rozhraní v4.1, které se starým není kompatibilní, takže aplikace postavené na Mews musí odesílání přepsat.

## Instalace

```bash
dotnet remove package Mews.Fiscalizations.Czechia
dotnet add package EetKit
```

Aplikace s ASP.NET Core nebo jiným DI kontejnerem si přidají ještě `EetKit.Extensions.DependencyInjection` (viz [README](https://github.com/Kazalax/eetkit/blob/main/README.md#aspnet-core)).

## Před a po

Původní kód s Mews:

```csharp
using Mews.Eet;
using Mews.Eet.Dto;
using Mews.Eet.Dto.Identifiers;

var certificate = new Certificate(password: heslo, data: File.ReadAllBytes("certifikat.p12"));
var record = new RevenueRecord(
    identification: new Identification(
        taxPayerIdentifier: new TaxIdentifier("CZ00000019"),
        registryIdentifier: new RegistryIdentifier("POKLADNA/1"),
        premisesIdentifier: new PremisesIdentifier(11),
        certificate: certificate),
    revenue: new Revenue(gross: new CurrencyValue(65.00m)),
    billNumber: new BillNumber("2027/0001"));

var client = new EetClient(certificate, EetEnvironment.Playground, httpTimeout: TimeSpan.FromSeconds(5));
var response = await client.SendRevenueAsync(record);
if (response.IsSuccess)
    Console.WriteLine($"FIK: {response.Success.FiscalCode}");
else if (response.IsError)
    Console.WriteLine($"Chyba {response.Error.Reason.Code}: {response.Error.Reason.Message}");
```

Totéž s EetKit:

```csharp
using EetKit;

using var client = EetClient.FromPkcs12("certifikat.p12", heslo, EetEnvironment.Playground);

var trzba = new Trzba("CZ00000019", idJednotky: 303, idPokl: "POKLADNA/1", poradCis: "2027/0001",
    datTrzby: DateTimeOffset.Now, celkTrzba: 65.00m);

var result = await client.SendAsync(trzba);
if (result.Status == EetStatus.Confirmed)
    Console.WriteLine($"POK: {result.Pok}");
else
    Console.WriteLine($"{result.Status}, chyba {result.ErrorCode}: {result.ErrorText}");
```

Kód pro Mews jsme přeložili proti balíčku Mews.Fiscalizations.Czechia 5.0.2. Kód pro EetKit, včetně ukázek níže, jsme 16. 11. 2026 spustili proti playgroundu a vrátil POK.

## Co čemu odpovídá

| Mews | XML v EET 3 | EetKit | XML v EET 2.0 |
| --- | --- | --- | --- |
| `TaxIdentifier` | `dic_popl` | `Trzba.EicPopl` | `eic_popl` |
| pověřující poplatník | `dic_poverujiciho` | `Trzba.EicPoverujiciho` | `eic_poverujiciho` |
| `PremisesIdentifier` | `id_provoz` | `Trzba.IdJednotky` | `id_jednotky` |
| `RegistryIdentifier` | `id_pokl` | `Trzba.IdPokl` | `id_pokl` |
| `BillNumber` | `porad_cis` | `Trzba.PoradCis` | `porad_cis` |
| `Revenue.Accepted` | `dat_trzby` | `Trzba.DatTrzby` (povinné) | `dat_trzby` |
| `Revenue.Gross` | `celk_trzba` | `Trzba.CelkTrzba` | `celk_trzba` |
| `Revenue.Deposit` | `urceno_cerp_zuct` | `Trzba.UrcenoCerpZuct` | `urceno_cerp_zuct` |
| `Revenue.UsedDeposit` | `cerp_zuct` | `Trzba.CerpZuct` | `cerp_zuct` |
| `isFirstAttempt: false` | `prvni_zaslani` | `new SendOptions(PrvniZaslani: false)` | `prvni_zaslani` |
| `EetMode.Verification` | `overeni` | `new SendOptions(Overeni: true)` | `overeni` |
| `Success.FiscalCode` (FIK) | `fik` | `EetResult.Pok` | `pok` |
| `Error.Reason.Code` a `Message` | `Chyba` (`kod` a text) | `EetResult.ErrorCode` a `ErrorText` | `Chyba` (`kod` a text) |
| `Warnings` | `Varovani` (`kod_varov`) | `EetResult.Warnings` | `Varovani` (`kod_varov`) |

## Co v EET 2.0 není

V datové zprávě EET 2.0 nejsou údaje o DPH. Popis položek datové zprávy to říká přímo: údaji o evidované tržbě nejsou údaje týkající se daně z přidané hodnoty. Proto pro tyto části `Revenue` v EetKit nic není:

- `NotTaxable` (`zakl_nepodl_dph`);
- `StandardTaxRate`, `ReducedTaxRate`, `LowerReducedTaxRate` (`zakl_dan1` až `3`, `dan1` až `3`, `pouzit_zboz1` až `3`);
- `TravelServices` (`cest_sluz`).

Zmizel také režim tržby (`EvidenceMode`, atribut `rezim`) a podpisový a bezpečnostní kód poplatníka. Hodnoty `record.Signature` (PKP) a `record.SecurityCode` (BKP) se v EET 2.0 neposílají a EetKit je nepočítá.

## Rozdíly v chování

### Označení evidenční jednotky je nové číslo

Starý `id_provoz` nejde přepsat do `id_jednotky`. Evidenční jednotce přidělí správce daně dvou až devítimístné číslo poté, co ji poplatník oznámí přes DIS+, a poplatník ho najde v detailu evidenční jednotky v DIS+.

### EIČ místo DIČ

U poplatníků registrovaných k dani je EIČ stejné jako DIČ. Fyzická osoba bez DIČ má jiný identifikátor, podrobnosti jsou v popisu položek datové zprávy. EIČ musí odpovídat certifikátu. Když nesedí, EetKit to hlásí v `LocalWarnings`.

### Certifikát

Pokladní certifikát pro EET 2.0 vydává CA EET a žádá se přes DIS+. `EetClient.FromPkcs12` načte soubor `.p12` tak, jak ho CA EET vydá.

### Výchozí prostředí

`new EetClient(certificate)` v Mews posílal do produkce. EetKit ve výchozím nastavení posílá na playground, takže pro ostrý provoz uveďte `EetEnvironment.Production`.

### Timeout

Mews při překročení času vyhodil `TaskCanceledException`. EetKit kvůli síti ani času výjimku nevyhazuje: po 5 sekundách (nastavitelné v `EetClientOptions.Timeout`, nejméně 2 s) vrátí `TemporaryError` s `IsRetryable = true`. Když odeslání zrušíte vlastním `CancellationToken`, dostanete `OperationCanceledException`.

### Chyby

Místo `IsSuccess` a `IsError` čtěte `Status`:

- `Confirmed` znamená přijato, POK je v `Pok`;
- `VerifiedOnly` je výsledek ověřovacího režimu;
- `TemporaryError` je výpadek nebo chyba serveru a tržbu stačí poslat znovu;
- `Rejected` znamená, že server zprávu odmítl a je potřeba opravit data nebo certifikát.

### Validace

Neplatná data skončí výjimkou `EetValidationException` se seznamem všech chybných polí a neodešlou se:

```csharp
try
{
    await client.SendAsync(trzba with { PoradCis = "účtenka 1" });
}
catch (EetValidationException ex)
{
    foreach (var chyba in ex.Errors)
        Console.WriteLine($"{chyba.Field}: {chyba.Message}");
}
```

### Opakované odeslání

V Mews se pro nový pokus vytvářel nový `RevenueRecord` s `isFirstAttempt: false`. V EetKit pošlete stejnou `Trzba` znovu s jinými volbami. Každý pokus dostane nové `uuid_zpravy`:

```csharp
var znovu = await client.SendAsync(trzba, new SendOptions(PrvniZaslani: false));
```

### Zálohy

Zálohy se zadávají přímo v `Trzba`:

```csharp
var trzbaSeZalohou = trzba with { UrcenoCerpZuct = 100m, CerpZuct = 35m };
```

### Ověření odpovědi

Mews podpis odpovědi neověřoval, jeho README to uvádělo jako známý problém. EetKit přijme potvrzení jen s platným podpisem Generálního finančního ředitelství a jen k té zprávě, kterou odeslal.

### Certifikát jako pole bajtů

Mews bral obsah `.p12` jako `byte[]`. `EetClient.FromPkcs12` bere cestu k souboru. Když máte certifikát uložený jinde, třeba v databázi, nabízí balíček `EetKit.Extensions.DependencyInjection` možnost `EetOptions.CertificateBytes`. Pro vlastní úložiště můžete také zaregistrovat `IEetCertificateProvider`.

## Co dál

Než přepnete na produkci, vyzkoušejte odesílání na playgroundu s testovacími certifikáty (viz [README](https://github.com/Kazalax/eetkit/blob/main/README.md#playground)). Když narazíte na něco, co tu chybí, [založte issue](https://github.com/Kazalax/eetkit/issues).
