---
title: EetKit Pro: fronta tržeb a obnova certifikátů pro EET 2.0
description: Placené rozšíření knihovny EetKit pro dodavatele pokladních systémů v .NET. Tržba se neztratí při výpadku a pokladní certifikáty se obnovují samy. Roční licence za firmu, integrace na klíč.
---
# EetKit Pro

Placené rozšíření knihovny [EetKit](https://kazalax.github.io/eetkit/) pro firmy, které dodávají nebo provozují pokladní software v .NET. Knihovna zdarma umí tržbu odeslat. Pro řeší, co se v provozu pokazí: výpadek sítě, restart aplikace a certifikáty, které po roce vyprší.

## Pro koho

Pro dodavatele pokladen, hotelových, gastro a prodejních systémů, kteří mají desítky až stovky zákazníků. Každý zákazník má vlastní pokladní certifikát a každá jeho tržba musí do EET dojít, i když zrovna nejde internet.

Pokud evidujete tržby jen pro sebe na pár pokladnách, nejspíš vám stačí [EetKit zdarma](https://kazalax.github.io/eetkit/) nebo státní aplikace MOJE eet.

## Co Pro umí

### Fronta tržeb

Tržba se uloží do databáze vaší aplikace ve stejné transakci jako prodej. Pokud server finanční správy nebo internet nefunguje, fronta ji posílá znovu, dokud neprojde, a přežije i restart aplikace. Víc instancí aplikace nad jednou databází pošle každou tržbu právě jednou. Když tržba čeká na doručení déle než 24 hodin, zapíše se varování do logu, po 48 hodinách od uskutečnění chyba. Funguje s Entity Framework Core, testováno na SQLite a PostgreSQL.

### Obnova pokladních certifikátů

Certifikát platí rok. Pro ho obnoví sám přes rozhraní CA EET 21 dní před koncem platnosti, uloží ho bezpečně (do souboru se zálohou, nebo šifrovaně do databáze) a vymění za běhu, bez restartu. Obnova přežije výpadek v kterémkoli kroku. Ostrou zkoušku proti skutečnému serveru CA EET děláme s prvními zákazníky, do té doby je ověřená proti testovacímu serveru postavenému podle oficiální specifikace a ve výchozím stavu vypnutá. Bez ní Pro nový certifikát, který nahrajete ručně, vymění za běhu také.

### Víc poplatníků v jedné aplikaci

Jedna instance aplikace eviduje tržby za víc poplatníků, každou tržbu s certifikátem jejího poplatníka.

### Dohled

Kontroly stavu pro ASP.NET Core (blížící se konec platnosti certifikátu, tržby čekající ve frontě) a metriky a trace pro OpenTelemetry (odesláno, potvrzeno, odmítnuto, délka fronty, doba odeslání).

## Cena

| Co | Cena |
| --- | --- |
| Pro, roční licence za firmu | 24 000 Kč |
| Pro pro první tři firmy | 12 000 Kč za první rok |
| Integrace na klíč | 7 500 Kč za den |

Licence platí pro celou firmu, bez omezení počtu vývojářů a nasazení u vašich zákazníků. Obsahuje aktualizace a podporu e-mailem do dalšího pracovního dne.

Nižší cenu pro první tři firmy nabízíme výměnou za zpětnou vazbu, první ostrou obnovu certifikátu u vás a souhlas, že vás smíme uvést jako referenci.

Integrace na klíč znamená, že EetKit do vaší pokladny zapojíme za vás: odesílání, frontu, certifikáty a testy proti playgroundu finanční správy.

## Jak začít

Napište na [bedrichmaly79@gmail.com](mailto:bedrichmaly79@gmail.com), jaký software dodáváte a kolik máte zákazníků. Odpovím do dalšího pracovního dne.

## Časté otázky

### Proč platit, když EetKit je zdarma?

EetKit zdarma pošle jednu tržbu a řekne vám, jestli prošla. Co s tržbou, která neprošla, kde ji uložit, kdy ji poslat znovu a jak nepřijít o certifikáty stovek zákazníků, musíte napsat sami. Pro je hotové a otestované řešení právě téhle části.

### Na jakých verzích .NET Pro běží?

Na .NET 8 a novějším (testováno na .NET 8 a .NET 10). Starý .NET Framework Pro zatím nepodporuje, protože fronta stojí na Entity Framework Core 8, který na něm neběží. Jádro EetKit na .NET Framework 4.7.2 a novějším běží. Pokud vaše pokladna běží na .NET Frameworku, napište nám.

### Dostanu zdrojový kód?

Ano, platící firmy dostanou přístup ke zdrojovému kódu Pro pro čtení. Nejste tak závislí na tom, že projekt někdo udržuje.

### Kdo odpovídá za evidenci tržeb?

Poplatník. EetKit Pro je nástroj, který evidenci usnadňuje. Není oficiálním produktem Finanční správy ČR.
