# M1: certifikát, zpráva a podpis

> **Pro agenty:** povinný podskill superpowers:subagent-driven-development (doporučeno) nebo superpowers:executing-plans. Kroky používají checkboxy (`- [ ]`).

**Cíl:** Načíst testovací .p12 na Linuxu i Windows, sestavit zprávu EET v4 z dat tržby, podepsat ji podle WS-Security a dosáhnout bajtové shody `DigestValue` a `SignatureValue` se třemi vzorovými requesty.

**Architektura:** Jádro `EetKit` má čtyři oddělené části: certifikát (`EetCertificate`), formátování a validaci (`EetFormat`, `TrzbaValidator`), sestavení obálky (`SoapEnvelopeBuilder`) a podpis s ověřením (`WsSecuritySigner`, `WsSecurityVerifier`). Každá část jde testovat samostatně. Odesílání (`EetClient.SendAsync`) patří do M2.

**Technologie:** C#, `netstandard2.0; net8.0; net10.0`, `System.Security.Cryptography.Xml`, `Microsoft.Bcl.TimeProvider` (jen netstandard2.0), xUnit, GitHub Actions.

**Spec:** `docs/ZADANI.md` (kapitoly 2, 3, 4.1, 4.3, 4.5, 6, 7, 9) a oficiální dokumenty v `docs/spec/` (při rozporu platí ty).

## Globální omezení

- Cíle knihovny: `netstandard2.0; net8.0; net10.0`. Testy: `net8.0; net10.0`.
- `Nullable` zapnuté, `TreatWarningsAsErrors`, analyzátory (`AnalysisLevel` latest), deterministický build, SourceLink, XML dokumentace veřejného API.
- Závislosti jádra jen `System.Security.Cryptography.Xml`, `Microsoft.Bcl.TimeProvider` (netstandard2.0) a `BouncyCastle.Cryptography`, pouze pokud nativní načtení .p12 selže (Task 2).
- Veškeré formátování v `CultureInfo.InvariantCulture`.
- Heslo, klíč ani obsah .p12 se nikdy nelogují ani nepropisují do textu výjimek.
- Do podpisu žádný `Timestamp` ani WS-Addressing. Podepsané XML se neformátuje.
- Verze balíčků: poslední stabilní, vydaná aspoň před dvěma týdny.
- Commity: `type(scope): popis` anglicky, bez ticketu, bez řádku Co-Authored-By (stejně jako v Pantry).

## Rozhodnutí oproti zadání (čeká na schválení)

1. **Podpis se skládá ručně, ne přes `SignedXml.ComputeSignature`.** `SignatureValue` se počítá z kanonikalizovaného `SignedInfo`, a kanonikalizace zachovává prefixy. Vzor má prefixy `ds:` a `ec:`, `SignedXml` generuje prvky bez prefixu, takže by podpis nikdy neseděl. `SignedInfo` proto skládáme jako šablonu s přesnými prefixy. `SignedXml` použijeme na ověřování podpisu (s přepsaným `GetIdElement`), tam prefixy nevadí.
2. **Sestavení XML zachová posun času, který dostane.** Hodnota v UTC se zapíše jako `Z`, stejně jako ve vzorech. Dokumentace (popis rozhraní, kap. 3.1) povoluje `+01:00`/`+02:00` i `Z`. Převod na Europe/Prague dělá samostatná funkce `EetFormat.ToPragueTime` a v M2 ji zavolá `EetClient` před sestavením. Bajtový test tak jde vést z dat vzoru a veřejné API přitom posílá český čas.
3. **`overeni` se zapisuje vždy** (`overeni="false"` jako ve vzorech). Atribut je součástí otisku, bez něj by shoda nevyšla.
4. **Shoda EIČ pověřujícího s EIČ poplatníka je varování, ne chyba.** Popis rozhraní (kap. 2.2.4, bod 3) ji řadí mezi propustné kontroly: server vydá POK s varováním 7. Zadání ji zakazuje.
5. **Minimální datum tržby je varování.** Podle kap. 2.2.4, bod 4 jde o propustnou kontrolu.
6. **Limit 12 kB počítáme jako 12 000 bajtů.** Dokumentace bajty neupřesňuje. Bereme přísnější výklad, zprávy mají kolem 4 kB. Ověří se na playgroundu v M2.

## Na co dát pozor při revizi

- Zóna `Europe/Prague` na Windows s .NET Framework neexistuje (zná jen `Central Europe Standard Time`). Převod nesmí spadnout. Test v Task 3.
- Částka s více než dvěma desetinnými místy (`12.345`) musí skončit chybou validace, ne tichým zaokrouhlením. Test v Task 4.
- Čas s milisekundami se zapíše bez zlomků sekund. Test v Task 3.
- Špatné heslo nebo poškozený .p12 vyhodí `EetCertificateException` a heslo v textu výjimky není. Test v Task 2.
- Text s diakritikou v `id_pokl` (`Pokladna č.1`) skončí chybou, která jmenuje pole. Test v Task 4.

---

### Task 1: Kostra řešení a pravidla buildu

**Files:**
- Create: `EetKit.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, `LICENSE` (MIT), `CHANGELOG.md`
- Create: `src/EetKit/EetKit.csproj`, `src/EetKit/Polyfills/*.cs` (internal `IsExternalInit`, `RequiredMemberAttribute`, `CompilerFeatureRequiredAttribute`, `SetsRequiredMembersAttribute`, jen pro netstandard2.0)
- Create: `tests/EetKit.Tests/EetKit.Tests.csproj`, `tests/EetKit.Tests/SpecFiles.cs`, `tests/EetKit.Tests/SpecFilesTests.cs`
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Produces: `SpecFiles` (test helper): `static string Path(string fileName)` vrací cestu k souboru z `docs/spec` zkopírovanému do výstupu testů; `static (byte[] P12, string Password) TestCertificate(string eic)` načte .p12 a heslo z `CAEET_Playground_2026_v1.zip` v paměti (`System.IO.Compression`).
- Produces: `InternalsVisibleTo("EetKit.Tests")` v `EetKit.csproj`.

- [ ] **Step 1: Test** `SpecFilesTests.TestCertificate_ReturnsP12AndPassword`: pro `"CZ00000019"` vrátí neprázdné bajty a neprázdné heslo; `File.Exists(SpecFiles.Path("EETXMLSchema.xsd"))` je true.
- [ ] **Step 2:** `dotnet test` selže (chybí projekty).
- [ ] **Step 3:** Založit řešení, projekty a společná pravidla podle Globálních omezení. Testovací projekt kopíruje `docs/spec/*.zip`, `*.xsd`, `*.xml` do výstupu (`<Content Include=... Link=... CopyToOutputDirectory=...>`). CI: matice `ubuntu-latest` × `windows-latest`, instalace SDK 8 a 10, `dotnet build -c Release` a `dotnet test -c Release` při každém pushi.
- [ ] **Step 4:** `dotnet build -c Release` bez varování, `dotnet test` zelený pro net8.0 i net10.0.
- [ ] **Step 5: Commit** `build: add solution skeleton, build rules and CI`

### Task 2: Načtení certifikátu (největší riziko, dělá se první)

**Files:**
- Create: `src/EetKit/EetCertificate.cs`, `src/EetKit/EetCertificateException.cs`
- Test: `tests/EetKit.Tests/EetCertificateTests.cs`

**Interfaces:**
- Consumes: `SpecFiles.TestCertificate`
- Produces:
  - `public sealed class EetCertificate : IDisposable`
  - `public static EetCertificate FromPkcs12(byte[] data, string password)`, `public static EetCertificate FromPkcs12File(string path, string password)`
  - `public string Eic { get; }` (z CN subjektu), `public DateTimeOffset NotAfter { get; }`
  - `public IReadOnlyList<string> GetWarnings(string eicPopl, DateTimeOffset now)` (neshoda EIČ, vypršení)
  - `internal X509Certificate2 Certificate { get; }`, `internal RSA PrivateKey { get; }`
  - `public sealed class EetCertificateException : Exception`

- [ ] **Step 1: Testy**
  - `Loads_AllThreePlaygroundCertificates` (Theory pro `CZ00000019`, `CZ683555118`, `CZ8551015704`): `Eic` se rovná EIČ, privátní klíč je k dispozici.
  - `NotAfter_MatchesCertificate`: pro `CZ00000019` je `NotAfter == 2027-05-14T09:40:23Z`.
  - `WrongPassword_ThrowsWithoutPasswordInMessage`: heslo `"spatne-heslo-123"` vyhodí `EetCertificateException` a `Message` ani `ToString()` heslo neobsahují.
  - `CorruptedData_Throws`: náhodné bajty vyhodí `EetCertificateException`.
  - `GetWarnings_EicMismatch` a `GetWarnings_Expired` (čas `2027-05-15T00:00:00Z`): vrátí právě jedno varování.
- [ ] **Step 2:** Spustit testy na Linuxu (lokálně, OpenSSL 3.0.13) a zapsat výsledek. Soubory jsou ověřené: certifikáty RC2-40, klíč 3DES, OpenSSL bez `-legacy` je neotevře.
- [ ] **Step 3:** Implementace: net10.0 přes `X509CertificateLoader.LoadPkcs12`, net8.0 a netstandard2.0 přes konstruktor `X509Certificate2`. Klíč se nezapisuje do systémového úložiště, pokud to platforma dovolí. Když nativní načtení na Linuxu selže, záloha je `BouncyCastle.Cryptography`: rozbalit .p12 v paměti a znovu ho zabalit moderními algoritmy (AES, SHA-256) před nativním importem. Balíček přidat jen v tom případě, zadání ho pro tento účel povoluje.
- [ ] **Step 4:** Testy zelené na net8.0 i net10.0 na Linuxu. Windows ověří CI (Task 7).
- [ ] **Step 5: Commit** `feat(certificates): load legacy PKCS#12 certificates`

### Task 3: Formátování hodnot a čas

**Files:**
- Create: `src/EetKit/Internal/EetFormat.cs`
- Test: `tests/EetKit.Tests/EetFormatTests.cs`

**Interfaces:**
- Produces (internal static `EetFormat`):
  - `string Amount(decimal value)`: přesně 2 desetinná místa, invariantní kultura, `-0.00` normalizuje na `0.00`. Hodnotu s víc než 2 místy nepřijímá (to hlídá validace).
  - `string DateTime(DateTimeOffset value)`: `yyyy-MM-ddTHH:mm:ss` + `Z` při posunu 0, jinak `±HH:mm`. Zlomky sekund odřízne.
  - `DateTimeOffset ToPragueTime(DateTimeOffset value)`: zóna `Europe/Prague`, když ji systém nezná, tak `Central Europe Standard Time`.
  - `string Bool(bool value)`: `true` / `false`.
  - `string Uuid(Guid value)`: malá písmena, 36 znaků.

- [ ] **Step 1: Testy**
  - `Amount`: `0.1m` → `"0.10"`, `1234567.8m` → `"1234567.80"`, `-0.00m` → `"0.00"`, `188580m` → `"188580.00"`, `-12.5m` → `"-12.50"`; výsledek je stejný pod kulturou `cs-CZ`.
  - `DateTime`: `2026-07-01T09:02:18+00:00` → `"2026-07-01T09:02:18Z"`, `2027-01-09T04:25:28+01:00` → `"2027-01-09T04:25:28+01:00"`, hodnota s 500 ms se zapíše bez zlomku.
  - `ToPragueTime`: `2027-01-09T03:25:28Z` → posun `+01:00`, `2027-06-09T03:25:28Z` → posun `+02:00`.
  - `Uuid`: výstup odpovídá `^[0-9a-f-]{36}$`.
- [ ] **Step 2:** Testy selžou.
- [ ] **Step 3:** Implementace podle Interfaces.
- [ ] **Step 4:** Testy zelené.
- [ ] **Step 5: Commit** `feat(format): add invariant formatting for amounts, times and booleans`

### Task 4: Veřejné typy a validace

**Files:**
- Create: `src/EetKit/EetEnvironment.cs`, `src/EetKit/Trzba.cs`, `src/EetKit/SendOptions.cs` (tvary přesně podle zadání, kap. 3)
- Create: `src/EetKit/EetValidationError.cs`, `src/EetKit/EetValidationException.cs`, `src/EetKit/Internal/TrzbaValidator.cs`
- Test: `tests/EetKit.Tests/TrzbaValidatorTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record EetValidationError(string Field, string Message)`
  - `public sealed class EetValidationException : Exception` s `IReadOnlyList<EetValidationError> Errors`
  - internal `TrzbaValidator.Validate(Trzba trzba, EetEnvironment env, DateTimeOffset now) → TrzbaValidationResult` s `IReadOnlyList<EetValidationError> Errors` a `IReadOnlyList<string> Warnings`
  - `Field` používá XML jména (`eic_popl`, `id_pokl`, `celk_trzba` ...)

- [ ] **Step 1: Testy** (každé pravidlo z tabulky v zadání, kap. 4.1, jeden test nebo jeden řádek Theory):
  - Chyby: `eic_popl` `"CZ1234567"` (7 číslic) a `"SK12345678"`; `id_jednotky` `0` a `1000000000`; `id_pokl` `""`, 21 znaků, `"Pokladna č.1"`; `porad_cis` 26 znaků; částka `100000000m` a `-100000000m`; částka `12.345m`; znak mimo ASCII 9, 10, 13, 32–126 v libovolném textu.
  - Bez chyby: data prvního vzoru (`CZ00000019`, `303`, `/5604/MA65`, `00/2224/SO57`, `188580.00`, `25.00`, `302.00`).
  - Varování: `id_jednotky` `5` (jedna číslice) a `305` (končí 5); `eic_poverujiciho == eic_popl`; datum tržby o 3 hodiny v budoucnosti; datum starší než 2 roky; playground s datem `2026-06-30T23:59:59+02:00`; produkce s `now` před 1. 1. 2027 a datem před `2026-11-01`; produkce s `now` od 1. 1. 2027 a datem před `2027-01-01`.
  - Všechny chyby se vrátí najednou, ne jen první.
- [ ] **Step 2:** Testy selžou.
- [ ] **Step 3:** Implementace. Regulární výrazy převzít z XSD (`docs/spec/EETXMLSchema.xsd`), aby validace a schéma nemohly ukazovat jinam.
- [ ] **Step 4:** Testy zelené.
- [ ] **Step 5: Commit** `feat(validation): validate sale data before sending`

### Task 5: Sestavení SOAP obálky

**Files:**
- Create: `src/EetKit/Internal/MessageHeader.cs`, `src/EetKit/Internal/SoapEnvelopeIds.cs`, `src/EetKit/Internal/SoapEnvelopeBuilder.cs`, `src/EetKit/Internal/EetNamespaces.cs`
- Test: `tests/EetKit.Tests/SoapEnvelopeBuilderTests.cs`, `tests/EetKit.Tests/SampleRequest.cs`

**Interfaces:**
- Consumes: `EetFormat`, `Trzba`
- Produces:
  - `internal sealed record MessageHeader(Guid UuidZpravy, DateTimeOffset DatOdesl, bool PrvniZaslani, bool Overeni)`
  - `internal sealed record SoapEnvelopeIds(string Token, string Signature, string KeyInfo, string TokenReference, string Body)` a `static SoapEnvelopeIds CreateRandom()` (formát jako ve vzoru: `X509-`, `SIG-`, `KI-`, `STR-`, `id-` + 33 hex znaků velkými písmeny)
  - `internal static XmlDocument SoapEnvelopeBuilder.Build(Trzba trzba, MessageHeader header, SoapEnvelopeIds ids)`: obálka bez podpisu, `PreserveWhitespace = true`; `soapenv` a `v4` deklarované na `Envelope`, `wsu` deklarované na `Body`; prázdný `soapenv:Header`
  - `SampleRequest` (test helper): `static SampleRequest Load(string eic)` načte vzor a vrátí `Trzba`, `MessageHeader`, `SoapEnvelopeIds`, `DigestValue`, `SignatureValue`, `TokenBase64` (časy jako `DateTimeOffset` s posunem 0)

- [ ] **Step 1: Testy**
  - `Build_ValidatesAgainstXsd`: element `v4:Trzba` projde `XmlSchemaSet` s `EETXMLSchema.xsd` pro data všech tří vzorů.
  - `Build_SampleData_AttributesEqualSample`: pro každý vzor jsou atributy `Hlavicka` a `Data` stejné jako ve vzoru (jméno i hodnota, pořadí se nekontroluje).
  - `Build_OmitsNullOptionalAttributes`: bez `UrcenoCerpZuct`, `CerpZuct`, `EicPoverujiciho` a `PovereniViceePopl` atributy chybí.
  - `Build_AlwaysWritesOvereni`: `overeni="false"` je přítomný.
  - `Build_BodyHasWsuId`: `Body` má `wsu:Id` rovné `ids.Body`.
- [ ] **Step 2:** Testy selžou.
- [ ] **Step 3:** Implementace podle Interfaces.
- [ ] **Step 4:** Testy zelené.
- [ ] **Step 5: Commit** `feat(xml): build SOAP envelope for sale message`

### Task 6: Podpis WS-Security, ověření a bajtová shoda

**Files:**
- Create: `src/EetKit/Internal/WsSecuritySigner.cs`, `src/EetKit/Internal/WsSecurityVerifier.cs`, `src/EetKit/Internal/EnvelopeSerializer.cs`
- Test: `tests/EetKit.Tests/WsSecuritySignerTests.cs`, `tests/EetKit.Tests/SampleSignatureTests.cs`

**Interfaces:**
- Consumes: `SoapEnvelopeBuilder`, `SoapEnvelopeIds`, `EetCertificate`, `SampleRequest`
- Produces:
  - `internal static void WsSecuritySigner.Sign(XmlDocument envelope, EetCertificate certificate, SoapEnvelopeIds ids)`: doplní `wsse:Security` do `soapenv:Header`
  - `internal static bool WsSecurityVerifier.Verify(XmlDocument envelope)`: ověří podpis těla certifikátem z `BinarySecurityToken` (`SignedXml` s přepsaným `GetIdElement` pro `wsu:Id`). V M2 se použije i pro odpovědi.
  - `internal static byte[] EnvelopeSerializer.ToUtf8(XmlDocument envelope)`: UTF-8 bez BOM, bez XML deklarace, bez formátování
  - `internal const int MaxMessageBytes = 12000`

- [ ] **Step 1: Testy**
  - `Sign_MatchesSample` (Theory pro tři EIČ): `SampleRequest.Load` → `Build` → `Sign` testovacím .p12 daného EIČ; `DigestValue`, `SignatureValue` i obsah `BinarySecurityToken` se rovnají vzoru znak po znaku.
  - `Verify_SampleRequest_True`: nezměněný vzor projde.
  - `Verify_TamperedAmount_False`: změna `celk_trzba` ve vzoru po podpisu vrátí false.
  - `Sign_NoTimestampNoAddressing`: podepsaná zpráva neobsahuje `wsu:Timestamp` ani jmenný prostor WS-Addressing.
  - `Serialize_NoBomNoDeclaration`: první bajt výstupu je `<`.
  - `Serialize_SampleSize_UnderLimit`: podepsaná zpráva prvního vzoru má méně než `MaxMessageBytes`.
- [ ] **Step 2:** Testy selžou.
- [ ] **Step 3:** Implementace podle Rozhodnutí 1. Pořadí: (a) otisk těla: exc-c14n s `InclusiveNamespaces PrefixList="v4"` přes `XmlDsigExcC14NTransform`, SHA-256, Base64; (b) složit `ds:Signature` se `ds:SignedInfo` přesně podle vzoru (prefixy `ds`, `ec`, `wsse`, `wsu`, pořadí prvků i atributů); (c) kanonikalizovat `ds:SignedInfo` (exc-c14n, `PrefixList="soapenv v4"`) v kontextu dokumentu; (d) podepsat RSA PKCS#1 v1.5 + SHA-256. Kanonikalizace podstromu musí brát v úvahu jmenné prostory předků: vstupem transformace je množina uzlů podstromu, ne `OuterXml` znovu načtené do nového dokumentu.
- [ ] **Step 4:** Testy zelené na net8.0 i net10.0.
- [ ] **Step 5: Commit** `feat(signing): sign sale message with WS-Security and verify signatures`

### Task 7: Ověření na Windows v CI

Vyžaduje tvůj souhlas: vytvořit repozitář na GitHubu (veřejný podle zadání) a pushnout do něj. Bez toho Windows ověřit nejde, lokálně je jen Linux.

- [ ] **Step 1:** Po souhlasu založit repozitář (`gh repo create`), pushnout `main`.
- [ ] **Step 2:** Počkat na běh CI a přečíst výsledek všech čtyř kombinací (Ubuntu a Windows × .NET 8 a 10).
- [ ] **Step 3:** Při pádu na Windows opravit příčinu v Task 2 nebo Task 6, ne v CI.
- [ ] **Step 4:** M1 je hotový, když CI prochází na všech čtyřech kombinacích a testy `Sign_MatchesSample` a `Build_ValidatesAgainstXsd` jsou v něm zelené.
