# EetKit

Open-core .NET knihovna pro odesílání tržeb do EET 2.0 (rozhraní v4.1). Free jádro (MIT) v tomto repu, placené Pro balíčky později v privátním repu `eetkit-pro`.

## Zdroje pravdy

- Oficiální dokumenty v `docs/spec/` (soupis v `docs/spec/README.md`). Při rozporu se zadáním platí ony a rozpor se hlásí uživateli.
- Repo je veřejné. Dokumenty označené TLP:AMBER (provozní informace playground a anglická produkční) patří jen do ignorované `docs/spec/tlp-amber/`, nikdy do commitu. Před přidáním nového dokumentu zkontrolovat značku TLP.
- Zadání: `docs/ZADANI.md`. Plány milníků: `docs/plans/`.
- Vzorové requesty `docs/spec/*.eet.v4.req.xml` jsou autoritou pro podobu podepsané zprávy. Certifikáty v nich jsou bajtově stejné jako v testovacích .p12 z `CAEET_Playground_2026_v1.zip` (ověřeno 6. 11. 2026).

## Ověřené technické fakty

- Testovací .p12: certifikáty RC2-40, klíč 3DES. OpenSSL 3 je bez `-legacy` neotevře. Heslo je v archivu (`password_pokladni_cert_playground.txt`), nikam ho neopisovat.
- .NET 8 a 10 tyto .p12 načtou nativně na Linuxu i Windows (testy lokálně a v CI), BouncyCastle není potřeba. .NET Framework 4.8 (cíl netstandard2.0) testovaný v CI na Windows, včetně playgroundu (16. 11. 2026). Lokálně jde net48 jen přeložit: `dotnet build -c Release -p:EetNetFramework=true`.
- Odpovědi serveru podepisuje certifikát GFŘ od I.CA Public CA/RSA 06/2022 (kořen I.CA Root CA/RSA 05/2022). Oba certifikáty I.CA jsou přibalené v `src/EetKit/Certificates`, kořen v systémovém úložišti Linuxu není. Důvěru řeší `ServerCertificateTrust`, nikdy nic nestahuje.
- Otisk těla: exc-c14n elementu `soapenv:Body` s deklaracemi `soapenv`, `v4`, `wsu`, SHA-256. Ručním výpočtem sedí se všemi třemi vzory.
- Limit zprávy je 12 288 bajtů (playground 6. 11. 2026: 12 288 B projde, 12 289 B chyba 7). `SOAPAction` server bere s uvozovkami i bez.
- Vzory zapisují časy v UTC (`Z`). Dokumentace povoluje `+01:00`/`+02:00` i `Z`.
- Shoda EIČ pověřujícího s poplatníkem a minimální datum tržby jsou propustné kontroly (popis rozhraní, kap. 2.2.4): server vydá POK s varováním.

## Rozhodnutí

Rozhodnutí oproti zadání jsou v plánu M1, sekce „Rozhodnutí oproti zadání“. Hlavní: `SignedInfo` se skládá ručně se stejnými prefixy jako vzor (`ds:`, `ec:`), protože `SignedXml` generuje prvky bez prefixu a `SignatureValue` by neseděl. `SignedXml` slouží jen k ověřování.

## Konvence

- Veřejné API používá česká jména podle zadání (`Trzba`, `EicPopl`), interní kód anglická.
- Dokumentace a README česky (README.en.md anglicky), commity anglicky.
- Commity: `type(scope): popis`, bez ticketu, bez řádku Co-Authored-By.
- Nikdy nelogovat heslo, klíč ani obsah .p12.

## Příkazy

- Build: `dotnet build -c Release`
- Testy: `dotnet test -c Release` (xUnit v3 na Microsoft.Testing.Platform, zapnuto v `global.json`; filtr např. `--filter-class EetKit.Tests.SampleSignatureTests`)
- Akceptační test podpisu: `SampleSignatureTests.Sign_MatchesSample` (shoda se třemi vzory finanční správy)
- Vydání na NuGet: zvednout `<Version>` v `Directory.Build.props`, datum v `CHANGELOG.md`, pak `git tag -a vX.Y.Z` a push tagu. Workflow `release.yml` otestuje, zabalí a pošle přes Trusted Publishing (bez API klíče). Tag pushovat jen po výslovném souhlasu uživatele.
- Web https://kazalax.github.io/eetkit/ se generuje z `README.md` (`python3 site/build.py _site`, workflow `pages.yml`). Co má být na webu, patří do README.
- Playground testy: `EET_PLAYGROUND=1 dotnet test --project tests/EetKit.PlaygroundTests -c Release -f net10.0` (skutečný server FS; v CI každou noc, workflow `playground.yml`)
