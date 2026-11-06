# M2: odesílání, odpověď a vydání 0.1.0

> **Pro agenty:** povinný podskill superpowers:executing-plans. Kroky používají checkboxy (`- [ ]`).

**Cíl:** `EetClient.SendAsync` pošle tržbu na server EET a vrátí srozumitelný výsledek. K tomu balíček pro DI, ukázková aplikace, testy proti playgroundu a balíčky `0.1.0` připravené k vydání na NuGet.

**Architektura:** `EetClient` spojí části z M1 do jedné cesty: validace, převod na český čas, sestavení, podpis, kontrola velikosti, HTTP POST, čtení odpovědi, ověření podpisu serveru, výsledek. Transport, čtení odpovědi a důvěra v certifikát serveru jsou samostatné interní části s vlastními testy. Testy jednotek nepotřebují síť (podstrčený `HttpMessageHandler`), testy proti playgroundu běží jen s `EET_PLAYGROUND=1`.

**Technologie:** jako M1, navíc `Microsoft.Bcl.TimeProvider` (netstandard2.0), v DI balíčku `Microsoft.Extensions.Http` a `Microsoft.Extensions.Options` (verze 10.0.13).

**Spec:** `docs/ZADANI.md` (kap. 3, 4.2, 4.4, 4.6, 6, 7, 8, 9), popis rozhraní v `docs/spec/`, plán M1 a jeho rozhodnutí.

## Ověřeno na playgroundu 6. 11. 2026

Pokusy se vzorovými requesty finanční správy, uložené odpovědi slouží jako testovací vzory:

- Odpověď přijde za 0,1 s. Potvrzení: `<eet:Potvrzeni pok="...-ff" test="true"/>`, prefix `eet:`, tělo `wsu:Id="Body-..."`.
- Potvrzení je podepsané bez prefixu `ds:` a s formátováním uvnitř `SignedInfo`. Certifikát: `CN=NEOSTRE - playground prostredi EET`, `O=Generální finanční ředitelství`, vydavatel `I.CA Public CA/RSA 06/2022`, kořen `I.CA Root CA/RSA 05/2022`.
- Kořen I.CA v systémovém úložišti Linuxu není. Otisky SHA-256 obou certifikátů I.CA sedí se stránkou ica.cz (HTTPS): kořen `d279c01a…7418eb`, vydavatel `df5baf6d…7a94de`.
- Chybová odpověď nemá hlavičku ani podpis: `<eet:Chyba kod="4" test="true">Neplatny podpis SOAP zpravy</eet:Chyba>`, v hlavičce `dat_odmit`.
- `SOAPAction` funguje s uvozovkami i bez nich.
- Limit velikosti je 12 288 bajtů: 12 288 B projde, 12 289 B vrátí chybu 7.
- Hlavička odpovědi `X-Global-Transaction-ID` (velikost písmen se liší, číst bez ohledu na ni).

## Globální omezení

Všechna omezení z plánu M1 platí dál. Navíc:
- Jádro smí přidat jen `Microsoft.Bcl.TimeProvider` (netstandard2.0). DI balíček smí `Microsoft.Extensions.Http` a `Microsoft.Extensions.Options`.
- Čtení XML odpovědi: `DtdProcessing.Prohibit`, `XmlResolver = null`.
- TLS: běžné ověření certifikátu serveru, nikdy ho nevypínat. Žádné IP adresy.
- Timeout výchozí 5 s, minimum 2 s.
- Nic se neloguje. Surové zprávy jen na výslovné zapnutí (v M2 žádné logování není).
- Commity s e-mailem `bedrichmaly79@gmail.com` (nastaveno v repu).

## Rozhodnutí

1. **Limit zprávy 12 288 bajtů** místo 12 000 (rozhodnutí 6 z M1 opraveno podle playgroundu).
2. **`SOAPAction` s uvozovkami** (`"http://fs.gov.cz/eet/OdeslaniTrzby"`), jak ho zapisuje SOAP 1.1. Server bere oba tvary.
3. **Certifikáty I.CA (kořen a vydavatel) jsou přibalené v knihovně** a důvěra v podpis odpovědi se ověřuje jen vůči nim. Řetězec musí vést k přibalenému kořenu, subjekt musí mít `O=Generální finanční ředitelství`. Platí pro playground i produkci.
4. **Revokace (CRL, OCSP) se nekontroluje.** Kontrola by přidala síťová volání do každého odeslání a výpadek CRL serveru by zastavil evidenci.
5. **Potvrzení s neověřitelným podpisem je `TemporaryError`**, ne `Confirmed`. Pokladna to zkusí znovu a server tržbu podle unikátnosti pozná.
6. **Chyba 8 je v jádru `TemporaryError`.** Jádro si nepamatuje pokusy, omezený počet pokusů řeší fronta v M3.
7. **Neznámé kódy:** záporné → `TemporaryError`, kladné → `Rejected`. **HTTP:** 408, 429 a 5xx → `TemporaryError`, ostatní kromě 200 → `Rejected`. SOAP Fault → `TemporaryError`.
8. **`EetResult.LocalWarnings`** (nad rámec zadání): místní varování validace a certifikátu, aby nezmizela.
9. **`IEetCertificateProvider` je v jádru** a `EetClient` se ptá na certifikát při každém odeslání. Výměna certifikátu za běhu v M4 pak nepotřebuje nový klient.
10. **`uuid_zpravy` v odpovědi se musí shodovat s odeslaným**, pokud v odpovědi je. Jinak `TemporaryError`.
11. **`Trzba` dostane konstruktor** s `[SetsRequiredMembers]` (rozhodnutí 8 z M1).

## Na co dát pozor při revizi

- Zrušení volajícím (`CancellationToken`) se nesmí plést s timeoutem: zrušení vyhodí `OperationCanceledException`, timeout vrátí `TemporaryError`.
- Odpověď s DTD nebo externí entitou (XXE) se nesmí načíst.
- Podvržené potvrzení podepsané certifikátem mimo I.CA nebo s jiným `O` nesmí být `Confirmed`.
- Opakované odeslání téže tržby má pokaždé nové `uuid_zpravy` a čerstvé `dat_odesl`.
- `HttpClient` z DI se nesmí uvolnit spolu s `EetClient` (patří továrně).

---

### Task 1: Veřejné typy výsledku a drobnosti z M1

**Files:** `src/EetKit/EetStatus.cs`, `EetResult.cs`, `EetWarning.cs`, `IEetCertificateProvider.cs`, úprava `Trzba.cs`, `Internal/EnvelopeSerializer.cs`. Testy: `EetResultTests.cs`, úpravy `TrzbaTests`, `WsSecuritySignerTests`.

**Interfaces:**
- `public enum EetStatus { Confirmed, VerifiedOnly, TemporaryError, Rejected }`
- `public sealed record EetWarning(int Code, string Text)`
- `public sealed class EetResult` s vlastnostmi ze zadání (kap. 3) a `IReadOnlyList<string> LocalWarnings`; `IsRetryable => Status == TemporaryError`. Konstruktor internal.
- `public interface IEetCertificateProvider { EetCertificate GetCertificate(); }`
- `Trzba`: veřejný konstruktor `(string eicPopl, int idJednotky, string idPokl, string poradCis, DateTimeOffset datTrzby, decimal celkTrzba)` s `[SetsRequiredMembers]` a bezparametrový konstruktor.
- `EnvelopeSerializer.MaxMessageBytes = 12_288`.

- [ ] Testy: `Trzba_Constructor_SetsRequiredMembers`; `Serialize_AtLimit_Passes` (přesně 12 288 B), `Serialize_OverLimit_Throws` (12 289 B); `EetResult_IsRetryable_OnlyForTemporaryError`.
- [ ] Testy selžou, implementace, testy zelené, commit `feat(api): add result types, certificate provider and Trzba constructor`.

### Task 2: Důvěra v certifikát serveru

**Files:** `src/EetKit/Internal/ServerCertificateTrust.cs`, `src/EetKit/Certificates/ica-root-rsa-2022.cer`, `ica-public-rsa-2022.cer` (embedded resources), úprava `WsSecurityVerifier.cs`. Testy: `ServerCertificateTrustTests.cs`, fixture `tests/EetKit.Tests/Fixtures/playground-potvrzeni.xml`, `playground-chyba-4.xml`.

**Interfaces:**
- `internal static bool ServerCertificateTrust.IsTrusted(X509Certificate2 certificate, DateTimeOffset now)`: řetězec přes přibalený vydavatel k přibalenému kořenu (net5+ `CustomRootTrust`, netstandard2.0 `AllowUnknownCertificateAuthority` a kontrola otisku kořene), bez revokace, čas `now`; subjekt obsahuje `O=Generální finanční ředitelství`.
- `WsSecurityVerifier.Verify(XmlDocument envelope, Func<X509Certificate2, bool>? isTrusted = null)`: s funkcí důvěry vrátí `true` jen pro důvěryhodný certifikát.

- [ ] Testy: uložené potvrzení z playgroundu se ověří a je důvěryhodné (čas 2026-11-06); vzorový request (podepsaný EET CA playground) důvěryhodný není; certifikát po `NotAfter` není; potvrzení se změněným POK neprojde; otisky přibalených certifikátů se rovnají hodnotám z ica.cz.
- [ ] Testy selžou, implementace, testy zelené, commit `feat(signing): trust server signatures only from I.CA and GFŘ`.

### Task 3: Čtení odpovědi

**Files:** `src/EetKit/Internal/ResponseParser.cs`. Testy: `ResponseParserTests.cs`.

**Interfaces:**
- `internal static EetResult ResponseParser.Parse(byte[] body, ResponseContext context)`
- `internal sealed record ResponseContext(Guid SentUuid, string? GlobalTransactionId, IReadOnlyList<string> LocalWarnings, DateTimeOffset Now)`
- Bezpečné čtení XML. `Potvrzeni` vyžaduje ověřený a důvěryhodný podpis (Task 2), jinak `TemporaryError`.

- [ ] Testy: potvrzení z playgroundu → `Confirmed`, POK, `IsTest`, `DatPrij`; chyba 4 → `Rejected`, `ErrorCode 4`, text, `DatOdmit`; kódy -1, 0, 2–8, -5, 1, 9 podle rozhodnutí 6 a 7; varování (`kod_varov`, text) se přenesou; neshoda `uuid_zpravy` → `TemporaryError`; SOAP Fault → `TemporaryError`; nesmyslný obsah → `TemporaryError`; odpověď s `<!DOCTYPE` a externí entitou se nenačte (`TemporaryError`, žádný přístup k souboru).
- [ ] Testy selžou, implementace, testy zelené, commit `feat(response): parse and classify server responses`.

### Task 4: EetClient a transport

**Files:** `src/EetKit/EetClient.cs`, `EetClientOptions.cs`, `Internal/FixedCertificateProvider.cs`, `Internal/EetEndpoints.cs`. Testy: `EetClientTests.cs`, `Fakes/StubHttpHandler.cs`, `Fakes/FixedTimeProvider.cs`.

**Interfaces:**
- `public sealed class EetClientOptions { EetEnvironment Environment; TimeSpan Timeout = 5 s }`, timeout pod 2 s → `ArgumentOutOfRangeException`.
- `public EetClient(IEetCertificateProvider certificates, EetClientOptions options, HttpClient httpClient, TimeProvider? timeProvider = null)`; `HttpClient` neuvolňuje.
- `public static EetClient FromPkcs12(string path, string password, EetEnvironment environment)`: vlastní `HttpClient` (net8+ `SocketsHttpHandler` s `PooledConnectionLifetime` 2 min), klient je pak `IDisposable` a uvolní ho.
- `public Task<EetResult> SendAsync(Trzba trzba, SendOptions? options = null, CancellationToken cancellationToken = default)`
- `internal Task<EetResult> SendRawAsync(byte[] envelope, Guid uuid, IReadOnlyList<string> localWarnings, CancellationToken ct)` (pro playground testy).

- [ ] Testy (bez sítě): úspěch vrátí `Confirmed`; hlavičky `SOAPAction` a `Content-Type: text/xml; charset=utf-8`; tělo je UTF-8 bez BOM a podpis v něm sedí (`WsSecurityVerifier.Verify`); `dat_odesl` a `dat_trzby` v českém čase podle `FixedTimeProvider`; dvě odeslání mají různé `uuid_zpravy`; `PrvniZaslani=false` a `Overeni=true` se propíšou; neplatná data vyhodí `EetValidationException` a nic se neodešle; timeout → `TemporaryError`; `HttpRequestException` → `TemporaryError`; HTTP 500 a 429 → `TemporaryError`, 404 → `Rejected`; zrušení volajícím → `OperationCanceledException`; `X-Global-Transaction-Id` se uloží; místní varování (např. `id_jednotky` 305) jsou v `LocalWarnings`; timeout 1 s v options vyhodí výjimku.
- [ ] Testy selžou, implementace, testy zelené, commit `feat(client): send sales to EET and return results`.

### Task 5: Testy proti playgroundu

**Files:** `tests/EetKit.PlaygroundTests/` (projekt, `PlaygroundTests.cs`), `.github/workflows/playground.yml`.

- [ ] Bez `EET_PLAYGROUND=1` se testy přeskočí (`Assert.Skip`).
- [ ] Testy: tři EIČ → `Confirmed`, POK končí `-ff`, `IsTest`; `Overeni=true` → `VerifiedOnly`, kód 0; zpráva se změněným tělem po podpisu (`SendRawAsync`) → `Rejected`, kód 4; zpráva 12 289 B (komentář v hlavičce, `SendRawAsync`) → `Rejected`, kód 7.
- [ ] Spustit lokálně s `EET_PLAYGROUND=1` a ukázat výstup.
- [ ] Workflow `playground.yml`: každou noc a ručně, Ubuntu, .NET 10. CI akce zvednout na `actions/checkout@v7` a `actions/setup-dotnet@v6`.
- [ ] Commit `test(playground): verify sending against the EET playground`.

### Task 6: DI balíček a ukázková aplikace

**Files:** `src/EetKit.Extensions.DependencyInjection/` (`EetServiceCollectionExtensions.cs`, `EetOptions.cs`, `EetOptionsValidator.cs`), `samples/EetKit.Sample.AspNetCore/`, testy `tests/EetKit.Tests/DependencyInjectionTests.cs`.

**Interfaces:**
- `services.AddEet(Action<EetOptions> configure)`: `EetOptions { Environment, CertificatePath, CertificateBytes, CertificatePassword, Timeout }`; vlastní `IEetCertificateProvider` má přednost, když je registrovaný.
- `EetClient` jako typovaný `HttpClient` přes `IHttpClientFactory`, options s `ValidateOnStart`.
- Ukázka: minimal API, stránka s formulářem (částka, číslo účtenky) a tlačítkem „Odeslat na playground“, výsledek s POK nebo chybou. Jen playground a testovací certifikát z `docs/spec`.

- [ ] Testy: `AddEet` vrátí funkční `EetClient`; chybějící certifikát nebo heslo selže při startu; timeout 1 s selže při startu; vlastní poskytovatel certifikátu se použije.
- [ ] Ukázku spustit a odeslat jednu tržbu na playground (výstup do zprávy).
- [ ] Commit `feat(di): add AddEet registration and sample app`.

### Task 7: README, balíčky a CHANGELOG

**Files:** `README.md`, `README.en.md`, `Directory.Build.props` (metadata balíčků), `src/*/*.csproj`, `CHANGELOG.md`.

- [ ] README česky: co knihovna dělá, příklad na 10 řádků, návod na playground, odznaky CI a playgroundu, věta „Není oficiálním produktem Finanční správy ČR. Za splnění evidenční povinnosti odpovídá poplatník.“, místo pro kontakt (doplní uživatel). README.en.md zkráceně anglicky.
- [ ] Metadata: `PackageId` `EetKit` a `EetKit.Extensions.DependencyInjection`, Title „EET 2.0 pro .NET – evidence tržeb“, popis česky i anglicky, tagy ze zadání (kap. 8), `RepositoryUrl` https://github.com/Kazalax/eetkit, README v balíčku, `Version` 0.1.0, symboly `snupkg`.
- [ ] `dotnet pack -c Release` vytvoří 2 balíčky a 2 balíčky symbolů. Obsah zkontrolovat (`unzip -l`).
- [ ] CHANGELOG: sekce 0.1.0.
- [ ] Témata repozitáře na GitHubu podle tagů.
- [ ] Commit `docs: add README and package metadata for 0.1.0`. Push, CI zelené.
- [ ] Vydání na NuGet dělá uživatel: připravit přesný příkaz `dotnet nuget push`.
