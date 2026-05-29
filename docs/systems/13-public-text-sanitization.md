***REMOVED*** 13 · Public-Text-Sanitization

***REMOVED******REMOVED*** Was es ist
Eine Regel, **welche Informationen** in öffentlich sichtbaren Texten stehen
dürfen – und welche nicht. Ziel: der Besucher liest **was er tun kann**,
nicht **wie die Seite intern aufgebaut ist**.

***REMOVED******REMOVED*** Verboten in Public-Texten
- Verweise auf Quelldateien (z. B. `ERC Ewige Tabelle.xlsx`)
- Erwähnung von Access-Tiers, Discord-Rollen, Login-Status
- Implementierungsdetails wie „Browser im Ingame-Stil mit Track-Filter
  und Access-Tier-Anzeige"
- Hinweise auf Daten-Pipeline, Imports, Admin-Logik

***REMOVED******REMOVED*** Erlaubt
- Was der Nutzer auf der Seite sieht/macht
- Was der Mehrwert ist
- Allgemeine sportliche Beschreibung

***REMOVED******REMOVED*** Beispiele aus dem Repo

| Datei | Vorher | Nachher |
|---|---|---|
| `Views/Home/Results.cshtml` | „Bestenliste & Season Progress … Position pro Rennen, schnellste Runden …" | „Aktueller Stand der Meisterschaften – Punkte, Sieger und Saisonverlauf aller Ligen." |
| `Views/Home/TrackSetups.cshtml` | „Setup-Browser im Ingame-Stil mit Track-Filter und Access-Tier-Anzeige." | „Setups für jede Strecke – schnell finden, vergleichen und ins Spiel übernehmen." |
| `Views/Home/EwigeListe.cshtml` | „… aus ERC Ewige Tabelle.xlsx" | „Das ewige Archiv der <OWNER_HANDLE> Racing Community – alle Saisons und Ligen auf einen Blick." |
| `Views/Home/AllRaces.cshtml` | „Ligaübergreifende Rennübersicht mit Sieger, Podium und Siegerzeit." | „Alle gefahrenen Rennen der ERC – mit Sieger, Podium und Bestzeit." |
| `Views/Home/Index.cshtml` (Setup-Karte) | „Setups pro Strecke – je nach Login/Discord-Rolle freigeschaltet." | „Setups für jede Strecke – Inhalte abhängig von deinem Zugang." |

***REMOVED******REMOVED*** Vorgehen für neue Texte
1. Kurz prüfen: „Verstehe ich das auch ohne das Projekt zu kennen?"
2. Streiche Datei-, Tabellen- und Rollennamen.
3. Schreibe in Verb-Form, was der User tut („Setups finden", „Saisonverlauf sehen").
