***REMOVED*** 01 · Race-Start Lights & Loader

***REMOVED******REMOVED*** Was es ist
Ein cinematisches Lade-/Start-Erlebnis im F1-Stil:
- 5 rote Lichter gehen nacheinander an
- Sie erlöschen schlagartig → **Lights out and away we go!**
- Während dessen fährt ein SVG-F1-Auto von links zur Ampel und beim Grün
  weiter nach rechts aus dem Bild
- Im Hintergrund laufen animierte F1-Icons (Helm, Reifen, Flagge ...)
- Ein wechselnder Lade-Text gibt der Szene Kontext (z. B. „Boxenstopp wird vorbereitet…")

Wird einmal pro Session beim ersten Seitenaufruf abgespielt **und** zusätzlich
immer dann, wenn eine Seite spürbar lange lädt (z. B. die Ewige Liste).

***REMOVED******REMOVED*** Wie es funktioniert

| Datei | Rolle |
|---|---|
| `Views/Shared/_Layout.cshtml` | Markup für Lichter, Auto-SVG, Label, Hintergrund |
| `wwwroot/css/site/10-f1-quickwins.css` | Animationen (`.race-lights*`, `.rl-car*`) |
| Inline-Script im Layout | Steuert Sequenz, Session-Flag, Long-Load-Detection |

Ablauf:
1. Beim DOM-Ready prüft das Skript `sessionStorage.f1Lights`.
2. Erste Anzeige → Sequenz wird gestartet (Lichter rein, Lichter aus, Auto los).
3. Die Page-Transition fadet die Seite ein (siehe `02-page-transitions.md`).
4. Long-Load-Hook: Wenn `fetch`/Navigation nach X ms noch läuft, wird
   die Ampel erneut eingeblendet, mit eigenem Text.

***REMOVED******REMOVED*** Sieger-Auto-Farbe
Das Auto wechselt seine Lackierung je nach Sieger-Team des **letzten gefahrenen
Rennens**. Logik im Layout-Codeblock:

```csharp
var lastRace = _layoutDb.RaceResults.OrderByDescending(r => r.Date).FirstOrDefault();
var winnerName = lastRace?.Winner;
var team = F1TeamsHelper.GetTeamByName(...);
_winnerCarPrimary = team?.PrimaryColor ?? defaultRed;
```

Diese Werte landen in den SVG-Gradients `rlCarBody` / `rlCarShine`.

***REMOVED******REMOVED*** Erweitern
- **Neuer Loader-Text:** Array `loaderTexts` im Layout-Script ergänzen.
- **Andere Auto-Farbe:** `F1TeamsHelper.Teams` erweitern oder `_winnerTeamKey`-Fallback ändern.
- **Sequenz-Geschwindigkeit:** CSS-Variablen `--rl-light-step`, `--rl-hold` in `10-f1-quickwins.css`.
