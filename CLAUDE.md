# EetKit

Open-core .NET knihovna pro odesílání tržeb do EET 2.0 (rozhraní v4.1). Free jádro (MIT) v tomto repu, placené Pro balíčky později v privátním repu `eetkit-pro`.

## Zdroje pravdy

- Oficiální dokumenty v `docs/spec/` (soupis v `docs/spec/README.md`). Při rozporu se zadáním platí ony a rozpor se hlásí uživateli.
- Zadání: `docs/ZADANI.md`. Plány milníků: `docs/plans/`.
- Vzorové requesty `docs/spec/*.eet.v4.req.xml` jsou autoritou pro podobu podepsané zprávy. Certifikáty v nich jsou bajtově stejné jako v testovacích .p12 z `CAEET_Playground_2026_v1.zip` (ověřeno 6. 11. 2026).

## Ověřené technické fakty

- Testovací .p12: certifikáty RC2-40, klíč 3DES. OpenSSL 3 je bez `-legacy` neotevře. Heslo je v archivu (`password_pokladni_cert_playground.txt`), nikam ho neopisovat.
- Otisk těla: exc-c14n elementu `soapenv:Body` s deklaracemi `soapenv`, `v4`, `wsu`, SHA-256. Ručním výpočtem sedí se všemi třemi vzory.
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
- Testy: `dotnet test -c Release`
- Playground testy: `EET_PLAYGROUND=1 dotnet test tests/EetKit.PlaygroundTests` (od M2)
