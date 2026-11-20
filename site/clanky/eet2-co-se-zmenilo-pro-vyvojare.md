---
title: EET 2.0 pro vývojáře: co se změnilo proti starému EET
description: Co se pro vývojáře pokladen změnilo mezi původním EET (rozhraní v3) a EET 2.0 (rozhraní v4.1): data, identifikátory, POK, podpis WS-Security, certifikáty a ověřování odpovědí.
date: 2026-11-16
author: Bedřich Malý
---
# EET 2.0 pro vývojáře: co se změnilo proti starému EET

Povinnost evidovat tržby platí znovu od 1. ledna 2027 a leden má podle finanční správy sloužit jako pilotní provoz. EET 2.0 používá datové rozhraní ve verzi 4.1, které se starým rozhraním v3 není kompatibilní, takže kód napsaný pro původní EET bez úprav fungovat nebude. Změnila se data, která se posílají, i kódy, které se tisknou na účtenku. Při psaní open-source knihovny [EetKit](https://github.com/Kazalax/eetkit) pro .NET jsem prošel oficiální dokumentaci finanční správy a vyzkoušel ji na jejím testovacím prostředí (playgroundu).

## Méně dat ve zprávě

V hlavičce datové zprávy EET 2.0 zůstalo `uuid_zpravy`, `dat_odesl`, `prvni_zaslani` a `overeni`. V datech je EIČ poplatníka, případně EIČ pověřujícího poplatníka a příznak pověření více poplatníky, dále označení evidenční jednotky, označení pokladního zařízení, pořadové číslo tržby, datum a čas tržby, celková částka a dvě částky pro zálohy (`urceno_cerp_zuct` a `cerp_zuct`).

Zmizel celý rozpis DPH. Popis položek datové zprávy to říká přímo: údaji o evidované tržbě nejsou údaje týkající se daně z přidané hodnoty. Pryč jsou tedy `zakl_dan1` až `3`, `dan1` až `3`, `zakl_nepodl_dph`, `pouzit_zboz1` až `3` i `cest_sluz`. Zmizel také atribut `rezim`, který rozlišoval běžný a zjednodušený režim.

## Nové identifikátory

Místo DIČ se posílá EIČ, evidenční identifikační číslo. U poplatníků registrovaných k nějaké dani je stejné jako DIČ, takže se pro většinu firem nic nemění. Fyzická osoba bez DIČ má jiný identifikátor a dokumentace popisuje, jak vzniká.

Větší změnou je označení evidenční jednotky (`id_jednotky`), které nahradilo označení provozovny (`id_provoz`). Staré číslo nejde jen přepsat do nového pole. Evidenční jednotce přidělí správce daně dvou až devítimístné číslo poté, co ji poplatník oznámí přes daňovou informační schránku DIS+, a právě tohle číslo musí pokladna posílat.

## POK místo FIK, bez BKP a PKP

Server po přijetí tržby vrací POK. Ve starém EET to byl FIK. Podpisový kód PKP a bezpečnostní kód BKP, které musela pokladna počítat sama, v datové zprávě EET 2.0 nejsou. Na testovacím prostředí končí POK znaky `-ff`, takže testovací účtenku nejde splést s ostrou.

## Podpis zprávy: WS-Security místo vlastního kódu

Zpráva se podepisuje standardem WS-Security: XML podpis s kanonizací exc-c14n, otiskem SHA-256 a podpisem RSA-SHA256, certifikát poplatníka jde ve zprávě jako `BinarySecurityToken`. Finanční správa dává k dispozici tři vzorové podepsané zprávy. Vyplatí se dosáhnout s nimi shody bajt po bajtu, protože pak víte, že podpis skládáte stejně jako server.

Třída `SignedXml` v .NET ale generuje prvky podpisu bez jmenného prefixu, kdežto vzory používají prefixy `ds:` a `ec:`. Podepsaná část `SignedInfo` pak nesedí a server podpis odmítne. `SignedInfo` jsem proto skládal ručně a `SignedXml` používám jen k ověřování.

## Certifikát ve starém formátu

Pokladní certifikát vydává certifikační autorita CA EET a žádá se o něj přes DIS+. Soubor `.p12` používá starší šifrování (RC2-40 a 3DES). OpenSSL 3 ho bez přepínače `-legacy` neotevře, takže knihovny, které na OpenSSL stojí, s ním můžou mít potíže. .NET 8 a 10 ho načtou bez problému na Windows i Linuxu.

## Odpovědi je potřeba ověřovat

Odpověď s POK je podepsaná certifikátem Generálního finančního ředitelství, který vydala I.CA. Kořenový certifikát I.CA ale v systémovém úložišti Linuxu, na kterém jsem to zkoušel (Ubuntu), není, takže ověření „podle systému“ selže. Knihovna si proto musí příslušné certifikáty I.CA nést s sebou. Chybové odpovědi server nepodepisuje.

Ověřovat se vyplatí i to, že odpověď patří k odeslané zprávě (stejné `uuid_zpravy`) a že přišla ze správného prostředí. Testovací odpověď nese příznak `test`.

## Výpadky a opakované odeslání

Když server neodpoví, tržba se pošle znovu s `prvni_zaslani="false"` a s novým `uuid_zpravy`. UUID identifikuje datovou zprávu, ne tržbu. Ověřovací režim (`overeni="true"`) zprávu jen zkontroluje a POK nevydá, což se hodí při instalaci pokladny.

## Co jsem zjistil na playgroundu

Pár věcí jsem v dokumentaci nenašel, tak jsem je vyzkoušel na testovacím prostředí:

- Maximální velikost zprávy je 12 288 bajtů. Zpráva o bajt delší skončí chybou 7.
- Hlavičku `SOAPAction` server přijme s uvozovkami i bez nich.
- Zpráva změněná po podpisu skončí chybou 4.

## Staré knihovny

Nejstahovanější .NET knihovna pro staré EET na NuGetu, Mews.Fiscalizations.Czechia, vyšla naposledy v říjnu 2022 a v lednu 2023 ji Mews ze svého repozitáře odstranil. Pro EET 2.0 tedy nepomůže. Pokud na ní vaše aplikace stojí, sepsal jsem [návod na přechod](https://github.com/Kazalax/eetkit/blob/main/docs/prechod-z-mews-eet.md) s tabulkou, co čemu odpovídá.

## Knihovna pro .NET

Pokud píšete v .NET, můžete použít EetKit. Je pod licencí MIT, na NuGetu jako `EetKit` a každou noc posílá testovací tržby na playground finanční správy. Pokud píšete v jiném jazyce, berte tenhle článek jako seznam věcí, na které si dát pozor. Oficiální dokumentace je ke stažení na [eet.gov.cz](https://eet.gov.cz/cs/pro-vyvojare/dokumenty-ke-stazeni).
