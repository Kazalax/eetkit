# Zadání pro Claude Code: EetKit, EET 2.0 pro .NET

Postav open-core .NET knihovnu pro odesílání tržeb do EET 2.0 (rozhraní v4.1). Zdrojem pravdy je oficiální dokumentace finanční správy. Když se s tímto zadáním rozchází, platí dokumentace a dej mi vědět.

**Kontext:** evidence startuje 1. 1. 2027. Free jádro (MIT) slouží jako marketing, placené Pro balíčky řeší provoz: frontu, lhůtu 48 hodin a obnovu certifikátů. Kód nečtu řádek po řádku, ověřuju ho testy. Ke každé části proto napiš testy, spusť je a ukaž výsledek. Commituj po malých krocích.

## 0. První krok: stáhni specifikaci

Stáhni tyto soubory do `docs/spec/` a commitni je. Zkontroluj, jestli na [stránce s dokumenty](https://eet.gov.cz/cs/pro-vyvojare/dokumenty-ke-stazeni) nejsou novější verze.

- [Popis rozhraní v1.2](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/EET_popis_rozhrani_v1.2.pdf) a [anglická verze v1.2a](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/EET_popis_rozhrani_v1.2a_EN.pdf)
- [Popis položek datové zprávy](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/EET_popis_polozek_datove_zpravy.pdf)
- [XSD](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/EETXMLSchema.xsd) a [WSDL](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/EETServiceSOAP.wsdl)
- Vzorové requesty: [CZ00000019](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/CZ00000019.eet.v4.req.xml), [CZ683555118](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/CZ683555118.eet.v4.req.xml), [CZ8551015704](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/CZ8551015704.eet.v4.req.xml)
- Provozní informace pro [playground](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/EET_pristupove_provozni_informace_playgr-3.pdf) a [produkci](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/EET_pristupove_provozni_informace_produk-2.pdf)
- Testovací certifikáty [CAEET\_Playground\_2026\_v1.zip](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/CAEET_Playground_2026_v1.zip) a certifikáty CA pro [playground root](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/ca_eet-root_cert-playground.crt), [playground sub](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/ca_eet-sub_cert-playground.crt), [produkce root](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/ca_eet-root_prod.pem) a [produkce sub](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/ca_eet-sub_prod.pem)
- [CA EET – postupy žádosti o certifikát v2](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/CAEET_postupy_zadost_certifikat_v2.pdf) a [CAEET API (OpenAPI, JWT)](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/caeetapi_jwt.zip)
- [Prezentace ze semináře pro vývojáře](https://eet.gov.cz/assets/cs/cmsmedia/pro-vyvojare/EET2_Prezentace_Seminar_pro_vyvojare_23-.pdf)

Heslo k testovacím .p12 hledej v dokumentaci k playgroundu. Když ho nenajdeš, zeptej se mě.

## 1. Rozsah

**Stavíme:** jádro `EetKit` a `EetKit.Extensions.DependencyInjection` (MIT, veřejné repo). Později v samostatném privátním repu Pro balíčky outbox, certifikáty a diagnostiku.

**Nestavíme:** UI, pokladní aplikaci, hostovanou službu ani podporu starého EET (rozhraní v3).

## 2. Struktura repozitářů

```
eetkit/                                   veřejné, MIT
  src/EetKit/                             netstandard2.0; net8.0; net10.0
  src/EetKit.Extensions.DependencyInjection/
  tests/EetKit.Tests/                     xUnit, unit testy
  tests/EetKit.PlaygroundTests/           integrační testy, jen s EET_PLAYGROUND=1
  samples/EetKit.Sample.AspNetCore/
  docs/spec/                              oficiální dokumenty
  README.md (česky), README.en.md
  .github/workflows/ci.yml

eetkit-pro/                               privátní, později
  src/EetKit.Pro.Outbox.EntityFrameworkCore/    net8.0; net10.0
  src/EetKit.Pro.Certificates/
  src/EetKit.Pro.Diagnostics/
  tests/
```

`netstandard2.0` je kvůli starým pokladnám na .NET Frameworku (WinForms). Je to reálná část trhu.

## 3. Veřejné API jádra

```csharp
public enum EetEnvironment { Playground, Production }

public sealed record Trzba
{
    public required string EicPopl { get; init; }
    public string? EicPoverujiciho { get; init; }
    public bool? PovereniViceePopl { get; init; }
    public required int IdJednotky { get; init; }
    public required string IdPokl { get; init; }
    public required string PoradCis { get; init; }
    public required DateTimeOffset DatTrzby { get; init; }
    public required decimal CelkTrzba { get; init; }
    public decimal? UrcenoCerpZuct { get; init; }
    public decimal? CerpZuct { get; init; }
}

public sealed record SendOptions(bool PrvniZaslani = true, bool Overeni = false);

public enum EetStatus { Confirmed, VerifiedOnly, TemporaryError, Rejected }

public sealed class EetResult
{
    public EetStatus Status { get; }
    public string? Pok { get; }
    public int? ErrorCode { get; }
    public string? ErrorText { get; }
    public IReadOnlyList<EetWarning> Warnings { get; }
    public Guid MessageUuid { get; }
    public DateTimeOffset? DatPrij { get; }
    public DateTimeOffset? DatOdmit { get; }
    public bool IsTest { get; }
    public string? GlobalTransactionId { get; }   // hlavička X-Global-Transaction-Id
    public bool IsRetryable => Status == EetStatus.TemporaryError;
}

public sealed class EetClient
{
    public static EetClient FromPkcs12(string path, string password, EetEnvironment env);
    public Task<EetResult> SendAsync(Trzba trzba, SendOptions? options = null, CancellationToken ct = default);
}
```

Neplatná data nikdy neodesílej: validace před odesláním vyhodí `EetValidationException` se seznamem polí. Čas ber přes `TimeProvider` (pro netstandard2.0 balíček `Microsoft.Bcl.TimeProvider`) a `HttpClient` musí jít podstrčit.

## 4. Požadavky na jádro

### 4.1 Validace před odesláním

Všechna pole se v XML posílají jako **atributy** elementů `Hlavicka` a `Data`, ne jako podelementy (viz XSD).

| Pole | Pravidlo |
| --- | --- |
| `uuid_zpravy` | Nové UUID v4 pro **každé** odeslání, i opakované, malými písmeny, 36 znaků |
| `eic_popl`, `eic_poverujiciho` | `^CZ[0-9]{8,10}$`; pověřující se nesmí rovnat poplatníkovi |
| `id_jednotky` | Celé číslo 1–999999999. Lokální varování, když nemá aspoň 2 číslice nebo poslední číslice není 1–4 |
| `id_pokl` | `^[0-9a-zA-Z\.,:;/#\-_ ]{1,20}$` |
| `porad_cis` | Stejná znaková sada, 1–25 znaků |
| `dat_trzby`, `dat_odesl` | `yyyy-MM-ddTHH:mm:sszzz`, bez zlomků sekund, se zónou; převádět na Europe/Prague |
| Částky | Invariantní kultura, přesně 2 desetinná místa, absolutně pod 100 000 000, nikdy `-0.00` (normalizovat na `0.00`), bez úvodních nul |
| Booleany | `true` / `false`; volitelné atributy s hodnotou null vynechat, prázdné hodnoty jsou neplatné |
| Znaky | Jen ASCII 9, 10, 13 a 32–126 |
| Velikost | Celá podepsaná SOAP zpráva max 12 kB, kontrolovat po podpisu |
| Datum tržby | Lokální varování mimo okno −2 roky až +2 hodiny. Minimum: playground 2026-07-01, produkce 2026-11-01, od 2027-01-01 produkce 2027-01-01 |

### 4.2 SOAP a transport

- SOAP 1.1, document/literal. Operace `OdeslaniTrzby`, `SOAPAction` = `http://fs.gov.cz/eet/OdeslaniTrzby` (zda v uvozovkách, ověř na playgroundu). Element `v4:Trzba` v jmenném prostoru `http://fs.gov.cz/eet/schema/v4`.
- Produkce `https://trzbyeet.gov.cz/eet/services/EETServiceSOAP/v4`, playground `https://pg.trzbyeet.gov.cz/eet/services/EETServiceSOAP/v4`.
- HTTP/1.1 POST, `Content-Type: text/xml; charset=utf-8`, UTF-8 bez BOM. TLS 1.2+, běžné ověření certifikátu serveru, žádné obcházení.
- Připojovat se přes DNS jméno a respektovat TTL: `SocketsHttpHandler.PooledConnectionLifetime` = 2 min (na net8+).
- Timeout nastavitelný, výchozí 5 s, minimum 2 s (seminář uvádí mezní dobu odezvy nejméně 2 s). Timeout i síťová chyba znamenají `TemporaryError`: doručení je nejisté a tržba se posílá znovu s `prvni_zaslani=false`. Duplicitu server pozná sám.

### 4.3 Podpis

Řiď se **vzorovými requesty** finanční správy, jsou autoritou pro přesnou podobu hlavičky.

- Hlavička `wsse:Security` (WS-Security 1.1) s `wsse:BinarySecurityToken` (X509v3, Base64Binary, `wsu:Id`).
- `ds:Signature`: kanonikalizace exc-c14n s `InclusiveNamespaces` podle vzoru (ve vzoru prefixy `soapenv v4`), `rsa-sha256`, jedna `Reference` na `#<wsu:Id těla>` s transformací exc-c14n a digestem SHA-256.
- `KeyInfo` → `wsse:SecurityTokenReference` → `wsse:Reference` na token.
- Podepisuje se **jen** `soapenv:Body`. Žádný `Timestamp` ani WS-Addressing, server je může brát jako útok.
- .NET `SignedXml` sám nenajde `wsu:Id`: přepiš `GetIdElement`. Nastav `PreserveWhitespace = true` a po podpisu XML neformátuj.
- **Akceptační test:** pro data ze tří vzorových requestů a odpovídající testovací .p12 musí vyjít `DigestValue` i `SignatureValue` bajt po bajtu stejně jako ve vzoru (RSA PKCS#1 v1.5 je deterministické).

### 4.4 Odpověď

- Parsuj `Odpoved`: `Hlavicka` (`uuid_zpravy`, `dat_prij` nebo `dat_odmit`), potom `Potvrzeni` (`pok`, `test`) nebo `Chyba` (`kod`, `test`, text) a 0–10× `Varovani` (`kod_varov`, text).
- U `Potvrzeni` ověř podpis těla certifikátem z odpovědi. Řetězec musí vést k I.CA (vydavatel „I.CA Public CA/RSA 06/2022“) a subjekt musí mít O = Generální finanční ředitelství. `Chyba` podepsaná není, podpis nevyžaduj.
- Mapování: `-1` → `TemporaryError`; `0` (jen při `overeni=true`) → `VerifiedOnly`; `2`–`7` → `Rejected`; `8` → `TemporaryError` s omezeným počtem pokusů, pak `Rejected` (otevřená otázka). SOAP Fault a HTTP 5xx → `TemporaryError`.
- Ulož `X-Global-Transaction-Id` z HTTP odpovědi.

### 4.5 Certifikáty

- .p12 z DIS+ je ve starém formátu PKCS#12 (klíč 3DES, certifikáty možná RC2-40) a OpenSSL 3 ho bez legacy provideru neotevře. **Musí fungovat na Windows i Linuxu**, ověř v CI jako první věc.
- Na .NET 9+ použij `X509CertificateLoader.LoadPkcs12`, na starších cílech konstruktor `X509Certificate2`. Když nativní načtení selže, záloha je BouncyCastle (`BouncyCastle.Cryptography`): rozbalit a znovu importovat v paměti.
- Hlídej platnost (`NotAfter`) a shodu EIČ v CN s `eic_popl` (jinak varování).
- Heslo, klíč ani obsah .p12 nikdy nelogovat.

### 4.6 Registrace do DI (Free)

- `services.AddEet(o => …)`: prostředí, certifikát (cesta nebo bajty a heslo, případně vlastní `IEetCertificateProvider`), timeout. Typovaný `HttpClient` přes `IHttpClientFactory`.
- Validace options při startu aplikace.

## 5. Pro balíčky (privátní repo, až po vydání jádra)

### 5.1 Outbox v EF Core

- Entita `EetSale`: `Id`, šest polí tržby plus volitelné částky, `Status` (Pending, Sending, Confirmed, Rejected), `AttemptCount`, `NextAttemptAt`, `LeaseUntil`, `FirstSentAt`, `LastAttemptAt`, `LastErrorCode`, `LastErrorText`, `LastMessageUuid`, `Pok`, `Warnings` (JSON), `ConfirmedAt`, `DeadlineAt` = `DatTrzby` + 48 h, `GlobalTransactionId`, concurrency token.
- Unikátní index (`EicPopl`, `IdJednotky`, `IdPokl`, `PoradCis`).
- `modelBuilder.ApplyEetOutbox()` a `IEetOutbox.EnqueueAsync(trzba)`, který jen přidá entitu do `DbContext` aplikace. Uložení dělá aplikace ve stejné transakci jako objednávku.
- `SendOrEnqueueAsync(trzba, timeout)`: jeden okamžitý pokus kvůli POK na účtence; při neúspěchu zůstane tržba ve frontě.
- `OutboxProcessor : BackgroundService`: polling po 2 s plus okamžité probuzení po `SaveChanges`. Řádky si bere přes lease (`LeaseUntil`) a optimistickou souběžnost, aby fungoval i ve více instancích.
- První pokus `prvni_zaslani=true`, každý další `false`, vždy nové `uuid_zpravy` a aktuální `dat_odesl`.
- Backoff: 2 s, 5 s, 15 s, 1 min, 5 min, 15 min, pak každých 30 min. U dočasných chyb se **nikdy nevzdávat**, povinnost doručit trvá i po 48 hodinách. `Rejected` čeká na opravu dat.
- Upozornění: varování v logu, když tržba čeká přes 24 h, chyba po `DeadlineAt`.
- Testy na SQLite a PostgreSQL (Testcontainers), SQL Server volitelně.

### 5.2 Obnova certifikátů přes CAEET API

Implementuj podle OpenAPI z `caeetapi_jwt.zip`.

- JWT RS256 podepsaný **obnovovaným** certifikátem. Hlavička `x5t#S256` = base64url(SHA-256(DER certifikátu)), bez `x5c`. Payload `iat` a `exp`, rozdíl nejvýš 5 minut. Posílá se jako `Authorization: Bearer`.
- Tok: `POST /request/renew` → `GET /request/{reqId}/status` (INPROCESS, ISSUED, DELIVERING, FINISHED, REJECTED; respektuj `pollAfterSeconds` a `Retry-After`) → `POST /request/{reqId}/claim-download` (base64 PKCS#12 a heslo; necachovat, nelogovat) → uložit → `POST /request/{reqId}/ack-download`. `GET /request/not-finished` slouží k dokončení přerušených žádostí.
- Plánovač obnoví certifikát 21 dní před `NotAfter` (nastavitelné; CA EET doporučuje 2–3 týdny).
- `IEetCertificateStore`: soubor se zálohou, Azure Key Vault, šifrovaně v DB přes Data Protection. Nový certifikát se nasadí za běhu, bez restartu.
- Úložiště podle EIČ, aby jedna instance zvládla víc poplatníků (Business).

### 5.3 Diagnostika

- Health check `eet_certificate`: Degraded pod 21 dní do konce platnosti, Unhealthy pod 3 dny nebo po vypršení.
- Health check `eet_outbox`: Degraded, když nejstarší čekající tržba čeká přes 1 h, Unhealthy přes 24 h.
- OpenTelemetry: `ActivitySource` a `Meter` jménem `EetKit`. Metriky odesláno, potvrzeno, odmítnuto, dočasná chyba, latence a délka fronty; tag `X-Global-Transaction-Id`.

## 6. Testy

- **Unit:** každé pravidlo validace (včetně `-0.00`, `0.1` → `0.10`, `1234567.8` → `1234567.80`), formát času a zóny, vygenerované XML validované proti XSD (`XmlSchemaSet`), podpis a jeho ověření, bajtová shoda se třemi vzory, parsování odpovědí pro všechny kódy, třídění chyb.
- **Playground** (jen s `EET_PLAYGROUND=1`): odeslání pro všechna tři testovací EIČ, POK končí `-ff` a `test=true`; ověřovací režim vrátí `0`; rozbitý podpis vrátí `4`; zpráva přes 12 kB vrátí `7`.
- **CI:** GitHub Actions, matice `ubuntu-latest` × `windows-latest` × .NET 8 a 10. Unit testy při každém pushi, playground každou noc. Odznak „ověřeno proti playgroundu“ do README.
- Načtení legacy .p12 na Linuxu má vlastní test, je to největší technické riziko.

## 7. Pravidla kvality

- Nullable zapnuté, varování jako chyby, analyzátory, deterministický build, SourceLink, XML dokumentace veřejného API.
- Formátování vždy v invariantní kultuře.
- Jádro bez závislostí navíc kromě `System.Security.Cryptography.Xml`, `Microsoft.Bcl.TimeProvider` (netstandard2.0) a případně BouncyCastle pro legacy .p12.
- Logování surových requestů a odpovědí jen na výslovné zapnutí. Heslo, klíč ani .p12 nikdy.
- SemVer, `CHANGELOG.md`, licenční hlavička MIT.
- V README výslovně: „Není oficiálním produktem Finanční správy ČR. Za splnění evidenční povinnosti odpovídá poplatník.“

## 8. NuGet a dohledatelnost

- `PackageId`: `EetKit` (nejdřív ověř, že je volný). Title „EET 2.0 pro .NET – evidence tržeb“.
- Popis česky i anglicky s klíčovými slovy. Tagy: `eet eet2 eet-2.0 evidence-trzeb trzby pokladna financni-sprava czech fiscal dotnet csharp`. Stejná témata dej repozitáři.
- README česky jako první: příklad na 10 řádků, návod na playground, odkaz na nabídku integrace na klíč (kontakt doplním).
- Stránka na GitHub Pages až po vydání 0.1.

## 9. Milníky a akceptační kritéria

1. **M1, do 15. 11.:** testovací .p12 se načte na Windows i Linuxu, zpráva se sestaví a podepíše, bajtová shoda se všemi třemi vzory, XML projde XSD.
2. **M2, do 22. 11.:** SOAP transport, ověření odpovědi a třídění chyb. Playground testy zelené pro všechna tři EIČ, DI balíček, ukázková ASP.NET Core aplikace. Vydat `0.1.0` na NuGet.
3. **M3, do 29. 11.:** outbox, processor a hlídání 48 hodin v privátním repu `eetkit-pro`, testy na SQLite a PostgreSQL.
4. **M4, do 6. 12.:** obnova certifikátů přes CAEET API, úložiště, health checks a OpenTelemetry. Pro `1.0.0`.

## 10. Otevřené otázky: zeptej se, nehádej

- [ ] Je chyba `8` dočasná, nebo trvalá? Dotaz na epodpora@fs.gov.cz pošlu já.
- [ ] Má být `SOAPAction` v uvozovkách? Ověř na playgroundu.
- [ ] Jak se získá první přístup ke CAEET API? Dokumentace popisuje jen obnovu stávajícím certifikátem.
- [ ] Heslo k testovacím .p12, pokud není v dokumentaci.
- [ ] Je název `EetKit` na NuGetu volný?

## 11. Co nedělat

- Žádné UI, hostovaná služba ani podpora starého EET (v3).
- Žádné obcházení ověření TLS a žádné natvrdo zadané IP adresy.
- Do podpisu nepřidávat `Timestamp` ani WS-Addressing a podepsané XML neformátovat.
- Nelogovat hesla, klíče ani obsah .p12.
- Neodesílat data, která neprošla validací.

Mimo rozsah Claude Code: prodej přes Polar.sh, oslovování firem a dotazy na finanční správu řeším sám.
