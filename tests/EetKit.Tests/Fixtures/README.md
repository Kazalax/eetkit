# Odpovědi z playgroundu

Skutečné odpovědi serveru EET playground, zachycené 6. 11. 2026 odesláním vzorového requestu `CZ00000019.eet.v4.req.xml` z `docs/spec`.

| Soubor | Co bylo odesláno | X-Global-Transaction-Id |
| --- | --- | --- |
| `playground-potvrzeni.xml` | vzor beze změny | `bc626a986aeddff902bee4c0` |
| `playground-chyba-4.xml` | vzor s `celk_trzba="1.00"` (neplatný podpis) | `bc626a986aede03302bee950` |

Certifikáty I.CA v `src/EetKit/Certificates` jsou stažené z `s.ica.cz` a `r.ica.cz`, otisky SHA-256 ověřené proti https://www.ica.cz/korenove-certifikaty.
