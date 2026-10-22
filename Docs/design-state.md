---
datum: 2026-10-18
tags: [entwicklung, design, frontend]
status: laufend
---

# Design State — Erdi-ERC

> Projektgedächtnis für Design-Arbeit. Vor jeder visuellen Änderung lesen.
> Aussagen sind mit Quelldatei und Status belegt (bestaetigt / abgeleitet / offen).
> Die Arbeitsregeln stehen im Skill `design-system`, nicht hier.

Zuletzt aktualisiert: 2026-10-18 (Claude-Session)

## 1. Projekt

| Punkt | Wert | Status | Quelle |
|---|---|---|---|
| Name | Erdi-ERC | bestaetigt | `Erdi-ERC/Erdi-ERC.csproj` |
| Kategorie | Community-Webanwendung (Liga-Verwaltung, öffentlich + Admin) | bestaetigt | `Controllers/`, `Views/` |
| Was es ist | Website der Erdi Racing Community: Rennkalender, Ergebnisse, Wertungen, Fahrerprofile, Bewerbungen, Community-Events | bestaetigt | `Views/`, `Docs/README.md` |
| Zielgruppe | Fahrer und Mitglieder der ERC (öffentliche Seiten), Admins (Dashboard) | abgeleitet | Rollen in `Controllers/Admin*`, `DriverProfile` |
| Hauptbedürfnis | Ergebnisse und Wertungen nachschlagen; Admins tragen Rennen ein | abgeleitet | `Views/Races/Results.cshtml`, `Views/Admin/EnterRace.cshtml` |
| Hauptziel | Liga-Betrieb abbilden (kein Verkauf, keine Anmeldung als Conversion) | abgeleitet | gesamte Navigation |
| Markenstimme | Motorsport-sachlich, direkt, deutsch, kein Marketing-Sprech | bestaetigt | `Views/` Copy, `Docs/superpowers/specs/2026-10-04-erc-race-control-frontend-design.md` |
| Sprachen | Deutsch (UI), Code/Kommentare deutsch | bestaetigt | `Views/` |

### 2. Stack

| Punkt | Wert | Quelle |
|---|---|---|
| Sprache / Framework | C# / ASP.NET Core MVC, `net10.0`, Razor Views | `Erdi-ERC/Erdi-ERC.csproj` |
| Rendering | Server-gerendert (MPA) | `Program.cs`, `Views/` |
| Styling | handgeschriebenes CSS in Schichten `wwwroot/css/site/NN-*.css`, gebündelt zu `site.bundled.css` | `wwwroot/css/site/` |
| Build | `dotnet build`; JS-Minify per esbuild über MSBuild/`npm run build` | `package.json`, `.csproj` |
| Tests | `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj` (679 Fälle, Stand 2026-10-18) | Testlauf |
| Frontend-Vertragstests | `tests/Erdi-ERC.Tests/Frontend/DesignContractTests.cs` (verbotene Klassen, Token-Pflichten) | dieselbe Datei |

### 3. Einstiegspunkte

| Concern | Datei |
|---|---|
| Root-Layout (Shell, Turm, Lade-Überlagerer) | `Erdi-ERC/Views/Shared/_Layout.cshtml` |
| Token-Ebene (einzige Quelle) | `Erdi-ERC/wwwroot/css/site/01-tokens.css` |
| Base / Layout / Komponenten | `02-base.css`, `03-layout.css`, `04-components.css` |
| Admin-Seiten | `06-pages-and-admin.css`, `Views/Admin/*` |
| Bild-Partial | `Views/Shared/_Picture.cshtml` + `Helpers/ImageDimensions.cs` |
| Theme-Umschaltung | `[data-theme="light"]` in `01-tokens.css`; Teamfarben über `body.team-<key>` |

### 4. Gestaltungsrichtung

- Profil: adaptiv, konkret **„Race Control / Broadcast"** — vom Projekt selbst gewählt, nicht vom Agenten.
- Ein Satz: Die Seite sieht aus wie das Timing-Paket einer Rennübertragung — dicht, flach, harte Kanten, Signalfarben nur für Signal.
- Begründung: Das Produkt *ist* Renn-Datenverwaltung; dichte Tabellen und Sektor-Kodierungen sind der Inhalt, nicht Dekoration. Quelle: `Docs/superpowers/specs/2026-10-04-erc-race-control-frontend-design.md`.
- Drei Adjektive: präzise, dicht, ruhig.
- Drei Vermeidungen: Verlaufs-Dekoration, Neon/Glow, Kartenstapel mit gleichem Padding.
- Reifegrad: gepflegt (ein System, gemessene Werte in `Projekte/erdi-erc.md` des Obsidian-Vaults).
- Vertrauen: hoch für Richtung, mittel für Detailregeln (nicht jede View ist durchgezogen).

### 5. Tokens (`01-tokens.css`, vollständig)

Farbe: Grundflächen `--track`, `--tarmac`, `--kerb`, `--rule`, `--rule-strong`; Text `--ink`, `--ink-2`, `--ink-3`; Signale `--fast` (schnellste Runde), `--best`, `--slow`, `--flag` (Liga/Rot), `--wet`; Teamfarben als `body.team-*` und `--team-<key>`.
Typografie: `--ff` Saira (Variable, selbst gehostet, latin), `--ff-cond` Saira Semi Condensed 600; Skala `--fs-xs`…`--fs-3xl` (≈1.25); `--num` tabular.
Abstand: `--sp-1`…`--sp-8` (4-px-Raster). Radien: `--r-1` 2px, `--r-2` 3px (hart, kein Feuerwerk).
Bewegung: `--t-fast` 120ms, `--t-base` 220ms, beide `cubic-bezier`.
Layout: `--rail` 288px Sidebar, `--gutter` clamp(1rem, 2.4vw, 2rem).
Breakpoints: nicht als Tokens, in `99-responsive.css` und inline `clamp()`.
z-index: keine Token-Ebene (offen).

### 6. Komponenten

| Komponente | Pfad | Genutzt auf | Zustände | Anmerkung |
|---|---|---|---|---|
| `_Picture` | `Views/Shared/_Picture.cshtml` | überall mit Uploads | WebP 1x/2x, Original-Fallback | Maße aus Dateikopf seit 2026-10-18 |
| `_F1StatTile` / `_F1StatTileInner` | `Views/Shared/` | Startseite, Wertungen | – | |
| `_DriverCard` | `Views/Shared/` | `/Stats` | – | |
| `_F1Section`, `_F1PageHero`, `_F1PitCard`, `_F1LeagueCard` | `Views/Shared/` | öffentliche Seiten | – | |
| `_AdminPageHero`, `_AdminHeroCard` | `Views/Shared/` | Admin | – | |
| Lade-Überlagerer | `_Layout.cshtml` + `10-f1-quickwins.css` | jede Seite, einmal pro Session | Sequenz 5 Lampen → grün | Effekte hier bewusst erlaubt |
| Renn-Eintragung | `Views/Admin/EnterRace.cshtml` | Admin | 22 Positionszeilen, DNF, Reserve, Gast | dichteste Formularseite |

### 7. Seiten (Auszug)

| Route | Datei | Zweck |
|---|---|---|
| `/` | `Views/Home/Index.cshtml` | Startseite, Turm, nächste Rennen |
| `/Races/Results` | `Views/Races/Results.cshtml` | Ergebnisse je Liga |
| `/Races/Constructors` | `Views/Races/Constructors.cshtml` | Teamwertung über alle Ligen |
| `/Races/RaceCalendar` | `Views/Races/RaceCalendar.cshtml` | Kalender-Poster |
| `/Stats/...` | `Views/Stats/*` | Ewige Liste, Fahrerkarten |
| `/Admin/...` | `Views/Admin/*` | Bewerbungen, Rennen eintragen, Community |
| `/Community/Events` | `Views/Community/Events.cshtml` | Events mit Galerie |

### 8. Geschützte Haken (nicht umbenennen)

| Haken | Abhängig von | Datei |
|---|---|---|
| `data-theme`, `body.team-<key>` | Tokens, Layout | `01-tokens.css`, `_Layout.cshtml` |
| `.race-lights*` | Loader-JS + Vertragstests | `_Layout.cshtml`, `10-f1-quickwins.css` |
| `.race-position-select`, `.dnf-driver-check`, `#dnfList`, `name="positions"` / `"dnfDrivers"` | `EnterRace.cshtml`-JS, Model-Binding | `Views/Admin/EnterRace.cshtml` |
| `data-images`, `data-gallery-index` | `event-lightbox.js` | Community-Views |
| verbotene Klassen in `DesignContractTests` | Vertragstest | `tests/.../Frontend/DesignContractTests.cs` |

### 9. Einschränkungen

- Keine externen Requests (Schriften, Icons selbst gehostet) — `Projekte/erdi-erc.md`.
- Ein CSS-Request pro Seite; Bundle wird mitgeliefert, nicht zur Laufzeit gebaut.
- Ziel: WCAG 2.2 AA, Hell-/Dunkel-Variante, 320 px ohne Überlauf.
- Keine Änderung an Controllern/Services für visuelle Zwecke.

### 10. Bekannte Probleme

| # | Problem | Ort | Schwere | Vorschlag |
|---|---|---|---|---|
| ~~1~~ | ~~Fahrerlisten beim Renn-Eintragen sind bei vollem Kader sehr lang~~ — behoben 2026-10-18: Dropdowns mit `optgroup` (Stamm/Ersatz/Gäste), DNF-Liste als aufklappbare Gruppen | `Views/Admin/EnterRace.cshtml` | major | erledigt |
| 2 | z-index hat keine Token-Ebene | `*.css` | minor | bei Bedarf Layer-Token einführen |

### 11. Backlog

| Priorität | Einheit | Dateien |
|---|---|---|
| 1 | Fahrerlisten im Renn-Eintrag gruppieren | `EnterRace.cshtml`, `06-pages-and-admin.css` |
| 2 | z-index-Layer als Tokens | `01-tokens.css` |

### 12. Entscheidungslog

| Datum | Entscheidung | Grund | Verworfen |
|---|---|---|---|
| 2026-10-20 | "Bewerbung" als Button rechts statt als siebter Nav-Link | Auf dem Handy wurde genau dieser Punkt zuerst nach "Mehr" verschoben — der einzige Aufruf, der sichtbar bleiben muss. Der Streifen behält sechs Hauptpunkte | Siebter Link in der Reihe lassen (rutscht auf schmalen Schirmen weg) |
| 2026-10-18 | Fahrerlisten nach Stamm / Ersatz / Gast gruppieren, DNF-Liste als aufklappbare Gruppen | Kader wächst; flache Listen kosten Scrollen und Suchzeit | Filterfeld im Dropdown (JS, mehr Aufwand) |

### 13. Changelog

| Datum | Einheit | Dateien | Tokens | Geprüft durch |
|---|---|---|---|---|
| 2026-10-22 | Bewerbungsformular: Feld **Alter** (`type=number`, 13–99, Pflicht) als drittes Feld neben Gamer-Tag und Plattform; neue Regel `.app-form__grid--three` (3 Spalten, unter 768 px einspaltig wie das übrige Raster) | `Views/Application/Apply.cshtml`, `wwwroot/css/pages/applications.css` (+ Bundle) | keine neuen Tokens; eine Layout-Regel | `dotnet build` 0 Fehler, 726/726 Tests; **nicht im Browser geprüft** (Login + laufende DB nötig) |
| 2026-10-20 | "Bewerbung" aus der Nav-Reihe in `.strip__status` verschoben: primärer Button (`btn-primary`) neben Login, Login dafür auf `btn-ghost`. Damit sechs Hauptpunkte statt sieben; auf schmalen Schirmen rutschte "Bewerbung" vorher als erster Punkt nach "Mehr" | `Views/Shared/_Layout.cshtml`, `wwwroot/js/site.js` (nur Kommentar) | keine | `dotnet build` 0 Fehler; im Browser (Dev-Server, Chromium) bei 1440 / 390 / 320 px geprüft: kein Überlauf, Header auf dem Handy dreizeilig |
| 2026-10-18 | Bestenliste (EditLeague) in zwei Blöcke geteilt: Stammfahrer / Ersatzfahrer, je mit Anzahl; Pillen-Kopf zeigt beide Zahlen | `Views/Admin/EditLeague.cshtml`, `06-pages-and-admin.css` | keine | `dotnet build` 0 Fehler, 679/679 Tests; **nicht im Browser geprüft** |
| 2026-10-18 | Fahrerlisten im Renn-Eintrag gruppiert: `optgroup` in den 22 Positions-Dropdowns, DNF-Liste als drei aufklappbare Gruppen, Reserve-Zuordnung ebenfalls aufklappbar (`<details>`, offen nur bei vorhandener Vorbelegung) | `Views/Admin/EnterRace.cshtml`, `wwwroot/css/site/06-pages-and-admin.css` (+ Bundle) | keine neuen Tokens; Klassen `field-group*` | `dotnet build` 0 Fehler, 679/679 Tests; **nicht im Browser geprüft** (Admin-Login + DB nötig) |
| 2026-10-18 | Ausgangsaufnahme | – | – | Dateien gelesen, `git log` |

### 14. Offene Fragen

| # | Frage | Blockiert | gefragt am |
|---|---|---|---|
| 1 | Gibt es Breakpoint-Tokens, die ich übersehen habe? | nein | 2026-10-18 |

### 15. Session-Log

| Datum | Session | Zusammenfassung | Offen |
|---|---|---|---|
| 2026-10-18 | Claude | Design-State angelegt, Fahrerlisten-Gruppierung begonnen | Rest der Views nicht einzeln geprüft |
