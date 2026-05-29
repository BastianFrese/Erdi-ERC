***REMOVED*** 17 · CSS-Modul-System

***REMOVED******REMOVED*** Was es ist
Das gesamte Styling ist in nummerierte Module aufgeteilt, die
`wwwroot/css/site.css` in fester Reihenfolge importiert. So bleibt
die Kaskade vorhersehbar.

***REMOVED******REMOVED*** Reihenfolge

| Datei | Inhalt |
|---|---|
| `01-tokens.css` | Design-Tokens (Farben, Spacing, Radien, Schriftgrößen) |
| `02-base.css` | Reset, Typografie, Body-Defaults |
| `03-layout.css` | Topbar, Footer, App-Shell |
| `04-components.css` | Cards, Badges, Buttons, generische Komponenten |
| `05-f1-modules.css` | F1-spezifische Bausteine (Hero, Stats, Driver-Cards …) |
| `06-pages-and-admin.css` | Spezifische Seitenstile |
| `06b-applications.css` | Liga-Bewerbungsformular & verwandte Flächen |
| `07-utilities.css` | Helper-Klassen |
| `08-animations.css` | Keyframes, Reveal-on-Scroll |
| `09-responsive.css` | Media-Queries, Mobile-Fixes |
| `10-f1-quickwins.css` | Race-Start-Lights, Page-Transitions, Loader-Car |
| `11-unique-addons.css` | Achievements, Rivalries, Live-Pill, Banner, Shortcuts |

***REMOVED******REMOVED*** Konventionen
- Pro Modul ein klar abgegrenzter Verantwortungsbereich.
- Selektoren möglichst flach (BEM-artig, Komponenten-Prefix wie `.rivalry-card__title`).
- Theme-Variablen über `body.team-*` (siehe `03-team-theme.md`).

***REMOVED******REMOVED*** Erweitern
- Neues Modul: Datei `12-xyz.css` anlegen + in `site.css` importieren.
- Reihenfolge nicht ändern, ohne die Auswirkungen auf Cascade zu prüfen.
