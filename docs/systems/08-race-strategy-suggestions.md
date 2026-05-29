***REMOVED*** 08 · Renn-Strategie-Vorschläge

***REMOVED******REMOVED*** Was es ist
Beim Anlegen eines Setups schlägt das System automatisch eine sinnvolle
**Renn-Strategie** vor – abhängig von **Strecke** und **Renn-Länge**
(5 Laps, 25 %, 50 %, 100 %, …). Der Vorschlag kann im Editor noch
verändert werden, bevor er gespeichert wird.

***REMOVED******REMOVED*** Wie es funktioniert

1. Track-Katalog mit Eigenschaften wie:
   - Basis-Rundenzahl
   - Reifenverschleiß-Klasse
   - typisches Boxenstopp-Fenster
2. Längen-Mapping (`F1RaceLength`):
   - 5 Laps → fixe kurze Strategie
   - 25/50/100 % → Skalierung der Basis-Rundenzahl
3. Strategie-Generator berechnet:
   - Stopp-Anzahl
   - Reifen-Reihenfolge (Soft/Medium/Hard)
   - Stopp-Runden (z. B. „Stop 1: Lap 14")
4. Ergebnis wird in das Strategie-Formular vor-ausgefüllt.

***REMOVED******REMOVED*** Verbindung zum Setup-Editor
- Das **Strecken-Dropdown** ist identisch mit dem im Editor.
- Wird die Strecke geändert, aktualisiert sich der Vorschlag automatisch.
- User kann jeden Wert überschreiben – die Speicherung übernimmt nur die
  finalen Felder.

***REMOVED******REMOVED*** Erweitern
- **Neue Strecke:** im Track-Katalog Reifenverschleiß und Boxenfenster pflegen.
- **Andere Reifen-Strategie:** Generator-Funktion (z. B. `BuildStrategy(track, length)`)
  anpassen.
- **Neue Renn-Länge:** Enum erweitern und Skalierung definieren.

***REMOVED******REMOVED*** Anzeige für Nutzer
Im öffentlichen Setup wird die Strategie pro Länge ausgegeben, damit andere
Fahrer sich daran orientieren können.
