---
title: Licenční podmínky EetKit Pro
description: Licenční podmínky placeného rozšíření EetKit Pro: rozsah licence pro celou firmu, šíření v softwaru pro zákazníky, zdrojový kód, podpora, odpovědnost a ukončení.
---
# Licenční podmínky EetKit Pro

Platí od 21. 11. 2026.

## 1. Kdo a co

1.1 Poskytovatelem licence je Bedřich Malý, IČO 22573411, se sídlem náměstí 17. listopadu 1303, 535 01 Přelouč, e-mail bedrichmaly89@gmail.com (dále „poskytovatel“).

1.2 Nabyvatelem je podnikatel nebo jiná právnická osoba, která si licenci objedná pro svou podnikatelskou činnost (dále „nabyvatel“). Spotřebitelům se licence neposkytuje.

1.3 Software jsou balíčky EetKit Pro (`EetKit.Pro.Core`, `EetKit.Pro.Outbox.EntityFrameworkCore`, `EetKit.Pro.Certificates`, `EetKit.Pro.Certificates.EntityFrameworkCore`, `EetKit.Pro.Diagnostics`), jejich dokumentace a zdrojový kód (dále „Pro“). Volně šiřitelná knihovna EetKit se řídí svou licencí MIT, ne těmito podmínkami.

1.4 Licenční smlouva vzniká zaplacením objednávky. Cenu, délku placeného období a způsob platby určuje objednávka. Tyto podmínky jsou součástí smlouvy.

## 2. Rozsah licence

2.1 Poskytovatel uděluje nabyvateli nevýhradní licenci k užití Pro po dobu placeného období, pro celou firmu nabyvatele, bez omezení počtu vývojářů, aplikací a instalací a bez územního omezení.

2.2 Nabyvatel smí Pro šířit jako součást vlastního softwaru svým zákazníkům a nechat ho u nich běžet. Zákazníci nabyvatele ho smí užívat jen jako součást tohoto softwaru. Tím nabyvatel poskytuje podlicenci v rozsahu tohoto bodu a k jinému podlicencování oprávněn není.

2.3 Nabyvatel nesmí Pro šířit samostatně, jako knihovnu nebo vývojářský nástroj pro jiné vývojáře, ani zpřístupnit zdrojový kód Pro komukoli mimo svou firmu a své smluvní vývojáře.

2.4 Po dobu placeného období dostane nabyvatel přístup k repozitáři se zdrojovým kódem Pro. Do repozitáře poskytovatele nemůže zapisovat, kód si ale smí stáhnout, pro vlastní potřebu upravovat a opravovat a upravenou verzi užívat a šířit ve stejném rozsahu jako Pro podle bodu 2.2.

2.5 Podpora podle bodu 4.2 se týká jen verzí Pro, jak je vydal poskytovatel. Chybu v upravené verzi poskytovatel řeší, jen když se projeví i v neupravené.

2.6 Nabyvatel není povinen licenci využít.

2.7 Licenci smí nabyvatel postoupit jinému jen s písemným souhlasem poskytovatele.

2.8 Poskytovatel prohlašuje, že je autorem Pro a že licenci smí poskytnout. Pro používá knihovny třetích stran (EetKit a balíčky Microsoft .NET) pod jejich vlastními open-source licencemi, které platí vedle těchto podmínek.

## 3. Po skončení placeného období

3.1 Verze Pro vydané během placeného období smí nabyvatel užívat a šířit podle článku 2 i po jeho skončení, bez časového omezení. Pokladny u zákazníků nabyvatele tak nepřestanou fungovat.

3.2 Po skončení placeného období nabyvatel nemá nárok na nové verze, podporu ani další přístup ke zdrojovému kódu. Placené období může prodloužit novou objednávkou.

## 4. Aktualizace a podpora

4.1 Během placeného období má nabyvatel nárok na všechny nové verze Pro.

4.2 Podpora se poskytuje e-mailem na bedrichmaly89@gmail.com. Poskytovatel na dotaz odpoví nejpozději v pracovní den, který následuje po dni, kdy dotaz dostal. Odpověď neznamená vyřešení: doba opravy chyby se nezaručuje a nepřetržitá pohotovost není součástí licence.

4.3 Integrace na klíč a jiné práce nad rámec podpory se objednávají a platí zvlášť.

## 5. Evidence tržeb a odpovědnost

5.1 Za splnění povinnosti evidovat tržby odpovídá poplatník. Pro je nástroj, který evidenci usnadňuje. Není oficiálním produktem Finanční správy ČR a nenahrazuje kontrolu, kterou nabyvatel a jeho zákazníci provádějí sami.

5.2 Automatická obnova pokladních certifikátů je ve výchozím stavu vypnutá. Zatím je ověřená jen proti testovacímu serveru postavenému podle oficiální specifikace, ne proti skutečnému serveru CA EET. Kdo ji zapne, nese riziko, že se u skutečného serveru zachová jinak, a sám hlídá platnost certifikátů (Pro k tomu nabízí kontrolu stavu).

5.3 Pro se poskytuje ve stavu, v jakém je. Poskytovatel opravuje nahlášené chyby v rámci podpory podle bodu 4.2, ale nezaručuje, že Pro bude bez chyb nebo že se bude hodit pro konkrétní účel nabyvatele.

5.4 Celková náhrada škody, kterou poskytovatel nabyvateli z této smlouvy hradí, je omezená na cenu, kterou nabyvatel zaplatil za posledních 12 měsíců před vznikem škody. Poskytovatel nehradí ušlý zisk, pokuty uložené nabyvateli nebo jeho zákazníkům ani nepřímé škody.

5.5 Omezení podle bodů 5.3 a 5.4 neplatí pro škodu způsobenou úmyslně nebo z hrubé nedbalosti, pro újmu na přirozených právech člověka a tam, kde je zákon nedovoluje.

## 6. Osobní údaje a mlčenlivost

6.1 Pro běží v aplikaci nabyvatele a poskytovateli nic neposílá. Data tržeb, certifikáty ani hesla se k poskytovateli nedostanou, pokud mu je nabyvatel sám nepošle, třeba v dotazu na podporu. Nabyvatel proto v dotazech neposílá hesla ani obsah certifikátů.

6.2 Poskytovatel zachová mlčenlivost o všem, co se od nabyvatele dozví při podpoře nebo integraci, a nepoužije to k ničemu jinému. Nabyvatel zachová mlčenlivost o zdrojovém kódu Pro podle bodu 2.3.

## 7. Ukončení

7.1 Poruší-li nabyvatel podstatně článek 2 (šíří Pro samostatně nebo zpřístupní jeho zdrojový kód mimo firmu), může poskytovatel od smlouvy odstoupit. Tím zanikají všechna práva nabyvatele z licence včetně práva podle bodu 3.1 a nabyvatel přestane Pro užívat a šířit nejpozději do 30 dnů. Zákazníci, kterým nabyvatel Pro dodal před odstoupením jako součást svého softwaru, ho smí užívat dál.

## 8. Změny a závěr

8.1 Poskytovatel může tyto podmínky změnit. Změna platí od dalšího placeného období, nabyvatele na ni upozorní e-mailem nejméně 30 dní předem. Pro běžící placené období platí podmínky ze dne objednávky.

8.2 Smlouva se řídí právem České republiky. Spory, které se nepodaří vyřešit dohodou, rozhodují české soudy.

8.3 Strany spolu komunikují e-mailem. Zpráva poslaná na e-mail druhé strany uvedený v objednávce, u poskytovatele na adresu z bodu 1.1, se považuje za doručenou.

8.4 Pokud se některé ustanovení ukáže jako neplatné, ostatní platí dál a strany ho nahradí platným ustanovením s co nejbližším účelem.

8.5 Kontakt pro vše, co se týká licence: bedrichmaly89@gmail.com.
