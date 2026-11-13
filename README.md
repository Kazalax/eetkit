# EetKit: EET 2.0 pro .NET

[![CI](https://github.com/Kazalax/eetkit/actions/workflows/ci.yml/badge.svg)](https://github.com/Kazalax/eetkit/actions/workflows/ci.yml)
[![Ověřeno proti playgroundu](https://github.com/Kazalax/eetkit/actions/workflows/playground.yml/badge.svg)](https://github.com/Kazalax/eetkit/actions/workflows/playground.yml)

Knihovna pro odesílání tržeb do elektronické evidence tržeb EET 2.0 (rozhraní v4.1), která startuje 1. 1. 2027. Zkontroluje data, sestaví a podepíše zprávu, pošle ji finanční správě, ověří podpis odpovědi a vrátí výsledek s POK.

[English version](https://github.com/Kazalax/eetkit/blob/main/README.en.md)

> Není oficiálním produktem Finanční správy ČR. Za splnění evidenční povinnosti odpovídá poplatník.

## Instalace

```bash
dotnet add package EetKit
```

Testováno na .NET 8 a .NET 10 (Linux i Windows). Cíl `netstandard2.0` je určený pro .NET Framework, tam zatím testy neběží. Pro ASP.NET Core a další aplikace s DI je tu `EetKit.Extensions.DependencyInjection`.

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

## Výsledek odeslání

| `Status` | Co to znamená | Co dál |
| --- | --- | --- |
| `Confirmed` | Server tržbu přijal | POK z `Pok` patří na účtenku |
| `VerifiedOnly` | Ověřovací režim (`Overeni = true`), kód 0 | Zpráva je v pořádku, POK se nevydává |
| `TemporaryError` | Timeout, výpadek sítě, chyba serveru (kódy -1 a 8), jakákoli chyba HTTP, odpověď bez platného podpisu | Poslat znovu s `PrvniZaslani = false`. Duplicitu server pozná sám |
| `Rejected` | Server zprávu odmítl (kódy 2 až 7 a neznámé kladné kódy) | Opravit data nebo certifikát, opakování nepomůže |

Neplatná data se nikdy neodešlou: `SendAsync` vyhodí `EetValidationException` se seznamem polí. Místní varování (například neobvyklý tvar `id_jednotky`, prošlý certifikát nebo EIČ, které nesedí s certifikátem) jsou v `LocalWarnings`, varování serveru ve `Warnings`.

## Playground

Testovací prostředí finanční správy funguje stejně jako ostré, jen se nic neeviduje. Testovací certifikáty pro tři EIČ (`CZ00000019`, `CZ683555118`, `CZ8551015704`) i heslo k nim jsou v archivu [CAEET_Playground_2026_v1.zip](https://github.com/Kazalax/eetkit/blob/main/docs/spec/CAEET_Playground_2026_v1.zip). POK z playgroundu končí `-ff` a `IsTest` je `true`.

Ukázková aplikace pošle tržbu na playground jedním tlačítkem:

```bash
dotnet run --project samples/EetKit.Sample.AspNetCore
```

Pak otevřete http://localhost:5080.

## ASP.NET Core

```csharp
builder.Services.AddEet(o =>
{
    o.Environment = EetEnvironment.Production;
    o.CertificatePath = builder.Configuration["Eet:CertificatePath"];
    o.CertificatePassword = builder.Configuration["Eet:CertificatePassword"];
});
```

`EetClient` je jedna sdílená instance, nechte si ji předat do konstruktoru. Při startu aplikace se zkontroluje nastavení a certifikát se zkusí otevřít, takže špatná cesta nebo heslo zastaví start. Vlastní zdroj certifikátu (například trezor klíčů) zaregistrujte jako `IEetCertificateProvider`.

## Co knihovna hlídá

- Podpis přesně podle vzorových zpráv finanční správy (shoda na bajt je ověřená testem).
- Starý formát .p12 z DIS+ se načte na Windows i Linuxu bez zásahu do OpenSSL.
- Potvrzení s POK se přijme jen s platným podpisem certifikátu Generálního finančního ředitelství od I.CA, ze správného prostředí a k odeslané zprávě. Chybové odpovědi server nepodepisuje.
- Zpráva nad 12 288 bajtů se neodešle (limit ověřený na playgroundu).
- Heslo, klíč ani obsah certifikátu se nikam nelogují.

## Integrace na klíč

Potřebujete EET zapojit do vlastní pokladny, frontu tržeb pro výpadky sítě nebo automatickou obnovu certifikátů? Kontakt bude doplněn.

## Licence

[MIT](https://github.com/Kazalax/eetkit/blob/main/LICENSE)
