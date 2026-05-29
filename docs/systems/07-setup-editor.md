***REMOVED*** 07 · Setup-Editor

***REMOVED******REMOVED*** Was es ist
Ein Ingame-naher Editor im Admin-Bereich, mit dem Setups für **F1 25** gepflegt werden:
- Aerodynamik, Transmission, Suspension Geometry, Suspension, Brakes, Tyres
- Strecken-Auswahl per Dropdown
- **Renn-Strategien** für alle Renn-Längen aus dem F1-Game
  (5 laps, 25 %, 50 %, 100 %, …)

Setups landen normalisiert in der DB und werden auf der öffentlichen Seite
`TrackSetups` dargestellt.

***REMOVED******REMOVED*** Wie es funktioniert

***REMOVED******REMOVED******REMOVED*** Backend
- `Controllers/AdminController.cs` enthält Create/Edit-Actions für Setups.
- `Models/SetupGameSpec` (oder vergleichbar) normalisiert das eingegebene
  Setup auf das von F1 25 unterstützte Wertespektrum.
- Strecken kommen aus einem zentralen Track-Katalog (gleicher Datensatz
  wie für den Strategie-Vorschlag, siehe `08-race-strategy-suggestions.md`).

***REMOVED******REMOVED******REMOVED*** Frontend
- Razor-View des Editors (Admin) mit clientseitiger Logik für Slider/Plausibilitätscheck.
- Strategien sind eine wiederholbare Liste pro Renn-Länge.
- Strecken-Dropdown ist mit dem Strategie-Planer **gekoppelt**, damit
  beide immer auf dieselben Strecken-IDs zeigen.

***REMOVED******REMOVED******REMOVED*** Anzeige für Endnutzer
- `Views/Home/TrackSetups.cshtml` zeigt Setup-Cards mit Reifen, Aero etc.
  und einen Strategie-Block pro Renn-Länge.

***REMOVED******REMOVED*** Erweitern
- **Neue Renn-Länge:** Enum/Konstanten-Liste der Längen erweitern.
- **Neue Strecke:** im Track-Katalog hinzufügen, dann ist sie sowohl im
  Editor-Dropdown als auch im Strategie-Vorschlag verfügbar.
- **Neuer Setup-Wert:** Model + Editor-View + öffentliche Setup-Card anpassen.
