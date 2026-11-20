---
title: Výpadek spojení v EET 2.0: co udělat s tržbou, která neprošla
description: Co říká dokumentace finanční správy o výpadku spojení v EET 2.0: mezní doba odezvy, lhůta 48 hodin, co se mění při opakovaném zaslání, které chyby má smysl opakovat a jak tržbu v pokladně neztratit.
date: 2026-11-20
author: Bedřich Malý
---
# Výpadek spojení v EET 2.0: co udělat s tržbou, která neprošla

Pokladna pošle tržbu, server finanční správy neodpoví a zákazník u pultu čeká. Co má pokladna dělat dál, popisují dva veřejné dokumenty na [eet.gov.cz](https://eet.gov.cz/cs/pro-vyvojare/dokumenty-ke-stazeni): Popis položek datové zprávy (verze 1.0 z 5. 10. 2026, kapitola 6) a Popis datového rozhraní (verze 1.2 z 25. 8. 2026). Prošel jsem je při psaní knihovny [EetKit](https://kazalax.github.io/eetkit/) a sepsal, co z nich plyne pro kód pokladny.

## Co říká dokumentace

První pokus o odeslání musí proběhnout nejpozději při uskutečnění tržby, tedy při přijetí peněz, u platby kartou při vydání příkazu k platbě. Pokladna pak čeká na odpověď nejvýš po mezní dobu odezvy, kterou si poplatník nastaví, ale ne kratší než 2 sekundy.

Když odpověď do té doby nepřijde, třeba kvůli výpadku internetu, musí poplatník zprávu poslat znovu bezodkladně poté, co příčina pomine, a nejpozději do 48 hodin od uskutečnění tržby. Povinnost zaslat tržbu po 48 hodinách nezaniká.

Postup je stejný ve dvou případech, které pokladna od sebe nerozezná: server zprávu vůbec nedostal, nebo ji dostal, vydal POK a odpověď se cestou ztratila. V obou případech se tržba posílá znovu.

## Co se při opakovaném zaslání mění

Mění se jen hlavička, data tržby zůstávají stejná jako při prvním pokusu.

| Položka | Opakované zaslání |
| --- | --- |
| `uuid_zpravy` | nové UUID, staré se nesmí použít znovu |
| `dat_odesl` | čas tohoto pokusu |
| `prvni_zaslani` | `false` (při prvním pokusu `true`) |
| `dat_trzby` a ostatní data | beze změny, včetně původního času tržby |

UUID identifikuje datovou zprávu, ne tržbu. Tržbu server pozná podle šesti položek: EIČ, evidenční jednotky, pokladního zařízení, pořadového čísla, času tržby a částky (kapitola 4 popisu rozhraní). Když přijde zpráva se stejnými hodnotami, server ji bere jako zaslání téže tržby. Opakované odeslání tedy nevytvoří druhou tržbu, i když server tu první už měl. Proto je důležité, aby se při opakování neměnil čas tržby ani pořadové číslo.

Opakované zaslání může vrátit jiný POK než první pokus. Pokladna, která první odpověď nedostala, ho stejně nezná, takže na účtenku jde ten, který skutečně přišel.

## Které chyby má smysl opakovat

Tabulka chybových kódů v popisu rozhraní (kapitola 3.5.4) rozlišuje výpadky a chyby zprávy:

- Kód -1 je dočasná technická chyba a server sám píše „odešlete prosím datovou zprávu později“. Opakovat.
- Kódy 2 až 7 jsou chyby zprávy: kódování, XML schéma, neplatný podpis, chybná struktura EIČ, příliš velká zpráva. Opakování stejné zprávy nepomůže, je potřeba opravit data nebo certifikát.
- Kód 8 zní „datová zpráva nebyla zpracována kvůli technické chybě nebo chybě dat“. Dokumentace z něj nedělá jasně ani jedno. EetKit ho bere jako dočasnou chybu, protože vzdát se tržby, která by napodruhé prošla, je horší než ji zbytečně zkusit znovu.
- Žádná odpověď (timeout, spadlé spojení, chyba HTTP) je typický výpadek. Opakovat.

Server kontroluje i čas tržby. Pokud je čas tržby o víc než 2 roky starší než okamžik přijetí zprávy, server ho označí jako chybný. Jde ale o takzvanou propustnou kontrolu (kapitola 2.2.4): POK se vydá i tak a chyba se jen uloží. Tržba odeslaná po týdnu tedy projde.

## Kam tržbu mezitím uložit

Kde má tržba čekat na další pokus, dokumentace neřeší. Pokladna, která drží neodeslané tržby jen v paměti, o ně přijde při prvním restartu nebo pádu aplikace. Spolehlivý postup vypadá zhruba takhle:

1. Uložit tržbu do databáze ve stejné transakci jako prodej. Buď se uloží obojí, nebo nic. Když se tržba ukládá až po prodeji, stačí pád aplikace mezi dvěma zápisy.
2. Hned zkusit první odeslání, aby byl POK na účtence. Dokumentace výslovně nepřipouští, aby centrální server zprávy z více pokladen bezdůvodně hromadil a posílal dávkově. Fronta proto slouží jen pro opakované pokusy, první pokus jde vždy hned.
3. Před odesláním si zapsat, že zpráva odchází na síť. Podle toho se pozná, jestli má další pokus `prvni_zaslani` nastavené na `true`, nebo `false`. Pokus, který spadl dřív, než zpráva odešla (třeba nešel načíst certifikát), server nikdy neviděl.
4. Opakovat s rostoucí prodlevou. Pokladna, která při výpadku zkouší posílat každou sekundu, zatíží síť i server ve chvíli, kdy oba mají potíže. EetKit Pro používá prodlevy 2 s, 5 s, 15 s, 1 min, 5 min, 15 min a pak každých 30 minut, dokud tržba neprojde.
5. Pokud aplikace běží ve více instancích nad jednou databází, zajistit, aby stejnou tržbu neposílaly dvě najednou. Stačí zámek na řádku s časovým limitem, který vyprší, když instance uprostřed spadne.
6. Hlídat lhůtu. Po 24 hodinách ve frontě zapsat varování a po 48 hodinách chybu do logu nebo dohledu, jinak se o tržbě, která neprošla, nikdo nedozví.

Výpadek nemusí být jen na straně poplatníka. Podle provozních informací produkčního prostředí (verze 1.1 z 10. 7. 2026) má servisní okno pro údržbu zpravidla každý čtvrtek mezi 20:00 a 6:00. Pokladny, které v noci prodávají, by s tím měly počítat.

## Jak to vypadá v kódu

V knihovně EetKit (zdarma, licence MIT) vrací odeslání při výpadku výsledek `TemporaryError` s příznakem `IsRetryable` a nevyhazuje výjimku. Každé volání `SendAsync` je nová datová zpráva s novým `uuid_zpravy` a aktuálním `dat_odesl`, takže pro opakování stačí poslat stejnou tržbu znovu:

```csharp
var result = await client.SendAsync(trzba);

if (result.Status == EetStatus.Confirmed)
{
    // POK na účtenku
}
else if (result.IsRetryable)
{
    // tržbu uložit a později poslat znovu:
    // await client.SendAsync(trzba, new SendOptions(PrvniZaslani: false));
}
else
{
    // Rejected: opravit data nebo certifikát, opakování nepomůže
}
```

Ukládání do databáze, plánování pokusů a hlídání lhůt už je na aplikaci. Pokud ho nechcete psát sami, řeší ho placené rozšíření [EetKit Pro](https://kazalax.github.io/eetkit/pro/) pro Entity Framework Core. Pokud píšete v jiném jazyce, berte tenhle článek jako seznam věcí, které vaše pokladna musí umět, a oficiální dokumentaci si stáhněte z [eet.gov.cz](https://eet.gov.cz/cs/pro-vyvojare/dokumenty-ke-stazeni).
