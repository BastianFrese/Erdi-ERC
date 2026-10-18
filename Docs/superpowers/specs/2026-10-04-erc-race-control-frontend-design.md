---
datum: 2026-10-04
tags: [entwicklung, design, frontend, spec]
status: in-arbeit
---

# ERC Frontend — „Race Control" Design-System

## Ziel

Die ERC-Site bekommt eine neue visuelle Identität. Die bestehende visuelle Sprache wird
verworfen. Umgesetzt wird das als **Design-System-Schnitt**: Tokens, Base, Layout,
Komponenten und die Shell (`_Layout.cshtml`) werden neu gebaut; die 101 bestehenden
Razor-Views erben den neuen Look, ohne einzeln umgeschrieben zu werden.

Das tragende Bild ist **Race Control / Broadcast**: die Seite soll aussehen wie das
Timing-Paket einer echten Rennübertragung — dicht, präzise, flach, mit echten
Motorsport-Datenkodierungen (Sektorzeiten, Reifenmischungen, Zeitdeltas, Flaggenstatus)
statt dekorativer „Futurismus"-Versatzstücke.

## Nicht-Ziel

- **Kein systematischer View-für-View-Umbau.** Views werden nicht auf Vorrat neu
  geschrieben. **Aber:** wo bestehendes Markup der neuen Gestalt im Weg steht, wird es
  umgebaut. Die alten Klassennamen sind eine Voreinstellung, kein Gesetz — der Umbau
  richtet sich nach dem, was Backend und Modelle tatsächlich liefern. Kein Aufwand wird
  darauf verwendet, altes Markup um jeden Preis am Leben zu halten.
- **Keine Änderung** an Controllern, Services, EF-Modellen, Routing, Auth, Deploy-Weg.
  **Eine Ausnahme, weil der Turm sie erzwingt:** `LayoutData` und `ILayoutDataService`
  werden um eine Top-10-Fahrerwertung erweitert. Das ist nötig, weil der Turm auf *jeder*
  öffentlichen Seite steht, die Daten aber heute nirgends im Layout vorliegen. Der Dienst
  ist bereits 30 s gecacht, die Erweiterung kostet also keine zusätzliche Abfrage pro
  Seitenaufruf. Jede weitergehende Service-Änderung bleibt ausgeschlossen.
- **Kein Redesign des Admin-Bereichs** über das hinaus, was die neuen Tokens automatisch
  mitnehmen. Der Admin behält seine eigene Hülle (`Views/Admin/_AdminLayout.cshtml`) und
  sein `adm-*`-Klassennamespace.
- **Kein Umbau der JS-Logik** (Ampel-Sequenz, globaler Musik-Player, Event-Lightbox,
  Setup-Exklusiv-Einwilligung). Angefasst wird nur, was zwingend an geänderte Selektoren
  oder Markup-Struktur gekoppelt ist.

## Erfolgskriterien

1. `dotnet build` läuft grün, keine Razor-Fehler.
2. Alle Views rendern; jede bewusst gebrochene Stelle ist entweder gefixt oder dokumentiert.
3. Die neue Identität ist auf Home, Results, Tabellen, Fahrerprofil, Setups und Events
   sichtbar — nicht nur auf der Startseite.
4. Keine Glas-, Verlaufs- oder Schattendekoration mehr auf öffentlichen Seiten.
   Signalfarben erscheinen nur dort, wo sie eine Bedeutung tragen.
5. Barrierefreiheit: Textkontrast ≥ 4.5:1, sichtbarer Fokusring, `prefers-reduced-motion`
   respektiert, Sektorfarben zusätzlich durch ein Glyph kodiert (nicht nur Farbe).
6. Genau **eine** Schriftfamilie wird geladen (Saira + Saira Semi Condensed), nicht drei.
7. Kein sichtbarer Layout-Sprung beim Laden durch den Start-Lights-Überlagerer.
8. **Performance-Budgets werden eingehalten** (siehe Abschnitt *Performance*):
   LCP < 1.5 s, CLS < 0.05, INP < 200 ms auf einem mittleren Mobilprofil;
   CSS gesamt < 60 KB gzip in **einem** Request; **null** Render-blockierende
   Fremdanfragen.
9. Die Start-Lights-Sequenz **verzögert den Seitenaufbau nicht** — sie liegt als
   Überlagerung über bereits gerendertem Inhalt.
10. Kein `backdrop-filter`, keine großflächig weichgezeichneten Ebenen, kein
    Scroll-Handler, der pro Frame schreibt.

---

## Bestandsaufnahme

Zahlen, auf denen die Umsetzung aufsetzt (Stand 2026-10-04, erhoben per Inventar über
`Views/` und `wwwroot/css/`):

**Zwei parallele CSS-Systeme, nur Bootstrap ist gemeinsam.**

| | öffentlich | Admin |
|---|---|---|
| Shell | `Views/Shared/_Layout.cshtml` | `Views/Admin/_AdminLayout.cshtml` |
| Klassennamespace | `f1-*` | `adm-*` |
| Stylesheet | `css/site.bundled.min.css` | `css/site.css` (**ungebündelt**, gleiche Quelle) |

Die beiden Namespaces überschneiden sich nicht. Der Admin erbt also **nicht** automatisch,
was im öffentlichen Bereich passiert — er teilt nur die Tokens. Das ist der Grund, warum
der Admin in dieser Arbeit nur die Farb- und Schriftänderung mitnimmt.

**Der wichtigste Befund: `pages/home-index.css` ist keine Seitendatei.** Sie wird von
**~35 Views** geladen — praktisch allen öffentlichen Seiten mit Datenanzeige (Results,
AllRaces, RaceDetail, LeagueResults, RaceCalendar, Erdi10, EwigeListe, HallOfFame,
DriverLevels, fast alle Community-Seiten, Profile, TrackSetups, Login). Sie ist de facto
das **zweite Design-System** und muss als Kern behandelt werden, nicht als Seiten-CSS.

**Klassennamen:**

- `f1-*`: 85 verschiedene Namen in 58 Views, 674 Vorkommen. Häufigste: `f1-icon` (72),
  `f1-pitcard` (71), `f1-section` (66), `f1-btn` (66), `f1-stat` (46).
- `adm-*`: die größte Familie; `adm-kpi-*` allein 175 Vorkommen.
- Weitere Präfixe: `troll-*`, `community-*`, `rc-*`, `stw-doc-*`, `wh-*`, `abe-*`,
  `dce-*`, `app-*`, `setup-admin-*`, `race-lights-*`, `lap-counter`, `status-pill`,
  `icon-chip`, `live-race-banner`.
- **Es gibt keine `erc-*`- und keine `tower`-Klassen.** Der Turm ist damit ein
  Neubau, kein Umbau.

**Was schon in die richtige Richtung zeigt und formalisiert wird:** `f1-timing-row` (15×),
`f1-timing-card` (13×), `f1-sector-strip` (16×), `f1-results-table`, `f1-table`,
`.sector` und `.tire` decken das Motiv „Timing" bereits ab — heute aber dekorativ und
uneinheitlich. Der Turm ist deren Zusammenführung, kein Fremdkörper.

**Zu entfernende FX-Klassen** (Auswahl, alle rein dekorativ):
`f1-tiltable` (18×), `f1-spark` (12×), `f1-border-anim` (14×), `f1-holographic`,
`f1-lift`, `f1-speedline`, `f1-home-backdrop` (31×), `chequered-sweep`, `race-lights__*`
(teilweise), `page-transition`.

**Bootstrap:** Utilities dominieren (`.col-*` 307, `.btn` 324, `.form-control` 170,
`.card` 164, `.row` 130, `.d-flex` 116, `.badge` 94, `.table` 54). Echte Komponenten:
Dropdown, Collapse/Tabs, Accordion, Progress, Alert, Navbar, Pagination, Spinner.
**Modals werden nicht benutzt** — die drei Dialoge (`Community/EventDetail:99`,
`Community/Events:188`, `Setups/TrackSetups:489`) sowie das Shortcut-Overlay in
`_Layout:960` sind handgebaut mit `aria-modal="true"` und werden direkt gestylt.

**Icons:** 27 verschiedene Lucide-Namen plus 4 dynamische Bindungen; zusätzlich
Bootstrap-Icons (`bi bi-*`) parallel. Beide Quellen bleiben.

**Inline-Styles: 574 Vorkommen in ~80 Views** — der größte Aufräum-Posten. Schwerpunkte:
`Home/Index`, `Admin/Webhooks/Send`, `Admin/EditLeague`, `Shared/_DriverCard` (64),
`Profile/Index`. Meist Farb-/Abstands-Overrides, die zu Utilities werden sollten.

**Tote Dateien:** `pages/driver-detail.css` (von keiner View referenziert),
`site.community.css` und `site.track-setups.css` (nur im Bundle, zur Laufzeit ungenutzt).

---

## Design

### Farbe

Grundfläche — neutral-kühl, kein Blaustich:

| Token | Wert | Rolle |
|---|---|---|
| `--track` | `#0B0C0E` | Seitenhintergrund (Asphalt) |
| `--tarmac` | `#14171B` | erhöhte Fläche: Zeile, Panel-Körper |
| `--kerb` | `#202630` | Kante, aktiver Zustand, Hover |
| `--rule` | `rgba(255,255,255,.10)` | 1-px-Trennlinien |
| `--ink` | `#F2F4F7` | Haupttext |
| `--ink-2` | `rgba(242,244,247,.68)` | Sekundärtext — Kontrast gegen `--track` prüfen |
| `--ink-3` | `rgba(242,244,247,.38)` | nur für Nicht-Text (Ticks, Rahmen) |

Signalfarben — jede bedeutet genau eine Sache:

| Token | Wert | Bedeutung |
|---|---|---|
| `--fast` | `#B24BF3` | schnellste Runde / schnellster Sektor (Lila) |
| `--best` | `#00D26A` | persönliche Bestzeit, DRS offen (Grün) |
| `--slow` | `#FFD100` | langsamer als Bestzeit, Vorsicht, Medium (Gelb) |
| `--flag` | `#E10600` | Ligafarbe, rote Flagge, Soft-Reifen |
| `--wet` | `#3B8BEA` | Intermediates, Wet, Blaue Flagge |

Reifenmischungen mappen auf dieselben Tokens: `soft → --flag`, `medium → --slow`,
`hard → --ink`, `inter → --best`, `wet → --wet`.

**Meister-Akzent.** Das bestehende `body.team-*`-Muster wird **nicht** als globaler Akzent
übernommen, sondern auf zwei Stellen begrenzt: ein ~12-%-Verlauf der Teamfarbe in der
Kopfzeile, und ein Farbbalken links an der Tabellenzeile des jeweiligen Fahrers.
Begründung: ein globaler Team-Akzent kollidiert mit den vier belegten Signalfarben; die
Teamfarbe wird damit zur **Zuordnung** (Fahrer → Team), nicht zur Stimmung.

**Kantenfall:** `F1TeamsHelper` liefert 11 Teams; **Cadillac ist `#000000`**. Eine schwarze
Teamfarbe ist als Verlauf und Balken unsichtbar. Für farbunkritische Fälle greift ein
Rückfall auf `--ink-3` plus die Sekundärfarbe als schmaler Zusatzstreifen.

Der Hell/Dunkel-Umschalter bleibt erhalten, als „Tageslicht"-Variante über dieselben
Tokens (helle Flächen, Signal- und Teamfarben unverändert in dunklerer Abstufung).

### Typografie

**Eine Familie: Saira** (Google Fonts), in zwei Schnitten — `Saira` und
`Saira Semi Condensed`. Echte Timing-Tower sind kondensiert, damit lange Nachnamen in eine
schmale Spalte passen; Saira liefert diese Breite innerhalb einer Familie.

- Namen und Spaltenköpfe in Versalien — im Motorsport werden Fahrer so benannt
  („VERSTAPPEN", nicht „Verstappen"). Fließtext bleibt in normaler Schreibung.
- **Alle Zahlen tabellarisch** (`font-variant-numeric: tabular-nums`). Damit richten
  Zeiten, Abstände und Punkte von selbst aus — es braucht **keine Mono-Schrift**.
- Skala ~1.25: `--fs-xs .75rem` · `--fs-s .875rem` · `--fs-m 1rem` · `--fs-l 1.25rem` ·
  `--fs-xl 1.75rem` · `--fs-2xl 2.5rem` · `--fs-3xl 3.5rem`, große Stufen mit `clamp()`.
- Orbitron, Inter und JetBrains Mono entfallen vollständig.

### Layout

```
┌────────────┬────────────────────────────────────────────────┐
│ ERC   ●LIVE│  RACE CONTROL    Runde 7 · Monza    ⏱ 00:12:44  │ ← Broadcast-Streifen (sticky)
├────────────┼────────────────────────────────────────────────┤
│ MEISTERSCH.│  ┌─ HERO ────────────────────────────────────┐  │
│ P  FAHRER  │  │  MONZA · GP VON ITALIEN                    │  │
│ 1 ▎Weber   │  │  Nächstes Rennen in   02:14:08             │  │
│ 2 ▎Krause  │  │  ▣ ▣ ▣ ▣ ▢    Formation                    │  │
│ 3 ▎Söder   │  └────────────────────────────────────────────┘  │
│ 4 ▎Braun   │  ERGEBNISSE ──────────────────────────────────   │
│ …          │  P  FAHRER    TEAM   GAP     LAST    S1 S2 S3     │
│            │  1  WEBER     RBR    —       1:21.4   ▮▮▮        │
│ 288 px     │  2  KRAUSE    MCL   +2.187   1:21.9   ▮▮▯        │
└────────────┴────────────────────────────────────────────────┘
     Turm                  Inhalt, 12-spaltig, linksbündig
```

**Timing-Turm als Rückgrat.** Eine feste linke Spalte (288 px, ab 1200 px) zeigt die
Top 10 der Meisterschaft: Position, Teamfarben-Balken, Fahrername, Punkte. Sie steht auf
allen öffentlichen Seiten und macht die Site strukturell zu einem Broadcast-Feed statt zu
einer Sammlung von Karten. Unter 1200 px klappt sie zu einem ausklappbaren Streifen; im
Admin-Bereich entfällt sie. Sie ist eine echte `<table>` mit `<th scope>`.

**Broadcast-Streifen statt Glas-Navbar.** Die Kopfzeile wird dünn und hartkantig: links
Session/Liga, rechts Countdown bis zum nächsten Rennen und der Live-Punkt. Kein
`backdrop-filter`, keine Carbon-Textur, kein Alu-Trim, kein Ambientelicht.

**Inhalt.** 12-Spalten-Raster, linksbündig; nur Zahlen- und Zeitspalten tabellarisch.
Sektionen werden durch eine Hairline plus einen kurzen Farbtick getrennt, nicht durch
abgerundete Karten.

**Der Hero ist Race Control**, nicht Logo-plus-Verlauf: Sessionname, Flaggenstatus,
nächstes Rennen mit Countdown, Vorschau der Top 5.

### Komponenten

**Neu gebaut** (tragen die Identität):

| Klasse | Zweck | Verhältnis zum Bestand |
|---|---|---|
| `.tower`, `.tower__row` | P · Farbbalken · Name · Zeit · Delta | führt `f1-timing-row` + `f1-timing-card` zusammen |
| `.sector` (`--fast/--best/--slow`) | drei Segmente je Runde, Glyph als zweite Kodierung | ersetzt `f1-sector-strip` |
| `.compound` (`--soft/--medium/--hard/--inter/--wet`) | Reifen-Badge | formalisiert `.tire` |
| `.delta` | Abstand, vorzeichenfarbig | neu |
| `.flagstate` | Streckenstatus | löst `.status-pill` ab |
| `.data-table` | dichte Datentabelle | löst `f1-table`/`f1-results-table`-Optik ab |
| `.panel`, `.panel__head` | harter Kasten mit Kopfzeile | behält `f1-panel` |
| `.strip` | Broadcast-Streifen | neu |

**Übernommen, in der neuen Sprache neu gestylt** — die Views bleiben unangetastet, weil
Razor diese Klassen bereits ausgibt:

`.f1-hero`, `.f1-section__*`, `.f1-stat*`, `.f1-leaguecard`, `.f1-panel`, `.f1-btn*`,
`.f1-chip*`, `.f1-pitcard`, `.f1-hub-*`, `.f1-card`, `.reveal`, `.stat-tile`,
`.result-row`, `.driver-tile`, `.event-card`, `.setup-card`, `.section-head*`,
`.page-header*`, `.status-pill`, `.live-race-banner`, `.erdi-select*`, `data-lucide`.

**Bootstrap bleibt geladen** und wird in `04-components.css` weiter auf die neuen Tokens
gemappt: `row/col-*`, `d-flex`, `btn`, `card`, `table`, `dropdown`, `badge`, `nav`,
`form-control`, `form-select`, `pagination`, `progress`, `alert`, `accordion`.
`data-bs-*`-Attribute werden nicht angefasst. Die drei handgebauten Dialoge und das
Shortcut-Overlay werden direkt gestylt.

### Motion

**Ein** orchestrierter Moment: die Start-Lights-Sequenz beim ersten Laden einer Session.
Sie ist das Signaturmoment und bleibt.

**Entfernt** (namentlich): `f1-speedline`, `f1-spark`, `chequered-sweep`,
`race-lights__chequers`, `race-lights__scan`, `race-lights__speedlines`, `f1-border-anim`,
`f1-holographic`, `f1-lift`, `f1-tiltable`, `f1-home-backdrop`, `page-transition`
(`is-entering`/`is-leaving`), Scroll-Progress-Balken, `reveal`-Einblendung auf jeder Sektion.

Der globale Musik-Player bleibt (funktional), wird aber zum schmalen Streifen.
Motion, die auf eine Nutzeraktion antwortet, bleibt erlaubt, wenn sie zeigt, was sich
geändert hat.

### Barrierefreiheit

- Sektorfarben erhalten ein zusätzliches Glyph (▲ schneller / = gleich / ▼ langsamer),
  damit die Information nicht allein über Farbe transportiert wird.
- Alle Text-/Hintergrund-Paare gegen 4.5:1 geprüft; `--ink-2` wird angehoben, falls nötig.
- Sichtbarer Fokusring auf allen interaktiven Elementen (`:focus-visible`).
- `prefers-reduced-motion: reduce` deaktiviert Start-Lights und alle Übergänge; der Inhalt
  ist dann sofort sichtbar (kein `opacity: 0`-Restzustand).

---

## Performance

Geschwindigkeit ist keine Nacharbeit, sondern Teil dieser Gestalt — die flache, harte
Sprache ist ausdrücklich auch deshalb gewählt, weil sie billig zu rendern ist. Konkrete
Budgets und die Maßnahmen, die sie einhalten:

### Budgets

| Größe | Ziel |
|---|---|
| LCP (mittleres Mobilprofil, 4G) | < 1.5 s |
| CLS | < 0.05 |
| INP | < 200 ms |
| CSS übertragen, gesamt | < 60 KB gzip, **ein** Request |
| Render-blockierende Fremdanfragen | 0 |
| Web-Fonts | ≤ 2 Dateien, selbst gehostet |

### Maßnahmen

**Schriften.** Heute werden drei Familien von Google Fonts geladen — Orbitron (5 Schnitte),
Inter (7), JetBrains Mono (4), also bis zu 16 Dateien über einen Fremd-Origin, mit
`preconnect` und einem `media="print"`-Trick gegen das Render-Blocking. Künftig: **eine**
Familie (Saira), **selbst gehostet**, auf die tatsächlich benutzten Schnitte beschnitten,
als WOFF2 mit `font-display: swap`. Damit entfallen der Fremd-Origin, die
`preconnect`-Handshakes und der Lade-Trick. (Google liefert „latin" inklusive der Umlaute
ä/ö/ü/ß — deutsche Texte sind abgedeckt.)

**StyleSheets.** Heute lädt der öffentliche Bereich `bootstrap.min.css`,
`site.bundled.min.css` plus null bis zwei Seitendateien; der Admin lädt
`bootstrap.min.css`, `site.css` (13 ungebündelte Partials) und eine Seitendatei. Künftig:
Bootstrap wird **in das Bundle aufgenommen**, sodass öffentlich wie Admin **ein**
Stylesheet laden. Die 14 Seitendateien werden aufgelöst — ihr Inhalt wandert in die
Ebenen, wo er hingehört; `home-index.css` (≈1040 Zeilen, 35 Views) zuerst.

**Icons.** Bootstrap Icons kommt heute als vollständiger Icon-Font von jsDelivr, obwohl
nur rund 30 Glyphen benutzt werden; Lucide wird parallel als JS geladen und rendert SVG.
Für die Performance: Bootstrap Icons **selbst hosten und auf die benutzten Glyphen
beschneiden** (oder die betroffenen Stellen auf Lucide ziehen, wo es billig ist). Die
Fremdanfrage nach jsDelivr entfällt damit. Beide Icon-Quellen bleiben erlaubt, solange
keine von außen kommt.

**Bilder.** Das Hero-Logo wird heute als 247-KB-PNG vorgeladen. Künftig AVIF/WebP mit
PNG-Rückfall über das vorhandene `_Picture`-Partial, das bereits `Sizes`, `Retina` und
`Priority` unterstützt — es muss nur benutzt werden. Kein `preload` für Bilder, die nicht
above the fold stehen.

**Der Überlagerer darf nichts blockieren.** Aktuell setzt das Layout `#appMain` auf
`opacity: 0`, solange die Ampel-Sequenz läuft, und blendet den Inhalt erst danach ein.
Das ist der größte Einzelposten im wahrgenommenen Tempo: der LCP wird um die Dauer der
Sequenz nach hinten geschoben. Neu: der Inhalt wird **normal und sofort gerendert**, die
Ampel liegt als `position: fixed`-Überlagerung darüber und wird entfernt, sobald sie
durchgelaufen ist. Die Sequenz selbst wird auf ~1.2 s gekürzt und erscheint nur beim
ersten Besuch einer Sitzung.

**Compositing.** `backdrop-filter: blur(10px) saturate(135%)` auf der Sticky-Kopfzeile
zwingt den Browser zu einer Neuberechnung bei jedem Scroll-Frame. Verläufe, Carbon-Texturen,
Scanlines, Funken, Partikel und die vier Hintergrundebenen des Heros kosten zusätzliche
Compositor-Ebenen. Alles davon entfällt (siehe *Motion*). Der Effekt ist gleichzeitig
Design- und Performance-Gewinn.

**Scroll und Eingabe.** Der heutige Scroll-Handler schreibt pro Frame die Breite des
Fortschrittsbalkens; `f1-tiltable` liest bei jeder Zeigerbewegung Layout. Künftig: der
Fortschrittsbalken entfällt, und was übrig bleibt, läuft über CSS-Animationen statt über
JS-Property-Writes. `content-visibility: auto` auf langen Listen (Tabellen, Setups,
Events) erspart das Layout weit unterhalb des Viewports.

**Kein Layout-Thrash.** Feste Höhen oder `aspect-ratio` für Bilder, Tabellenzeilen und
den Turm, damit beim Nachladen nichts springt (CLS-Budget).

---

## Umsetzung

### Bundling-Mechanik (zwingend zu beachten)

`wwwroot/css/site.bundled.css` und `.min.css` sind **generierte Artefakte**, erzeugt von
zwei MSBuild-Targets in `Erdi-ERC/Erdi-ERC.csproj`: `BundleSiteCss` (Konkatenation) und
`MinifySiteCss` (esbuild, Fallback reines Kopieren).

Die Liste der 15 Eingabedateien steht **zweimal** im Target — in `Inputs=` und in den
`_CssBundle Include=`-Zeilen. Beide Stellen müssen bei jeder Änderung mitgezogen werden,
sonst wird eine neue Datei stillschweigend nicht gebündelt und das Layout lädt sie nie.
Gelöschte Dateien müssen aus beiden Listen raus, sonst bricht der Build.

Der Admin lädt dagegen `~/css/site.css` ungebündelt. Beide Wege werden auf
`site.bundled.min.css` vereinheitlicht.

### Reihenfolge

1. **Tokens und Schriften** — `01-tokens.css` neu; `12-tokens-f1.css` und
   `13-tokens-light.css` darin auflösen und löschen; Team- und Signalfarben, Schrift-Stack,
   Skala, Abstände. Saira als WOFF2 ins Projekt holen und aus der Google-Fonts-Verlinkung
   im Layout entfernen.
2. **Base** — `02-base.css`: Typografie-Skala, Fokus, Selektion, Scrollbar.
3. **Layout** — `03-layout.css`: Streifen, Turm-Spalte, Inhaltsraster, Footer,
   Musik-Streifen. Die Cockpit-Navbar-Fassung entfällt vollständig.
4. **Komponenten** — `04-components.css`: Bootstrap-Mapping plus Turm, Sektor, Delta,
   Compound, `data-table`, `panel`, `strip`.
5. **`home-index.css` (≈1040 Zeilen) überführen** — wegen der ~35 Views der eigentliche
   zweite Kern. Erst danach greifen die folgenden Schritte sichtbar.
6. **`05-f1-modules.css`, `10-f1-quickwins.css`, `11-unique-addons.css`** in die neue
   Sprache überführen; FX-Klassen streichen, nachdem ihre Vorkommen per grep belegt leer
   sind.
7. **Shell** — `Views/Shared/_Layout.cshtml`: Navbar, Turm-Einbindung, Footer,
   Start-Lights (verschlankt), Musik-Player. Das eingebettete JS (~400 Zeilen) blockweise
   auf den Rest reduzieren, nach jedem Block die Seite laden.
8. **Shared Partials** — `_F1PageHero`, `_F1Section`, `_F1StatTile`/`Inner`, `_F1LeagueCard`,
   `_F1PitCard`, `_DriverCard`, `_LiveRaceBanner`, `_AdminPageHero`, `_AdminHeroCard`.
9. **Startseite** — `Views/Home/Index.cshtml` + `home-index-fx.css` (letztere entfällt) als
   Referenz-Umsetzung.
10. **`06-pages-and-admin.css` und `09-responsive.css`** auf den Rest reduzieren, den der
    Admin und die Sonderfälle wirklich brauchen.
11. **Assets** — Bootstrap-CSS in das Bundle aufnehmen, sodass öffentlich wie Admin **ein**
    Stylesheet laden; Bootstrap Icons selbst hosten und auf die benutzten Glyphen
    beschneiden (die jsDelivr-Anfrage entfällt); Hero-Logo als AVIF/WebP mit PNG-Rückfall
    über das vorhandene `_Picture`; `_AdminLayout` auf das gebündelte Stylesheet umstellen.
12. **Start-Lights aus dem kritischen Pfad holen** — `opacity: 0` auf `#appMain` entfernen,
    die Sequenz als Überlagerung über bereits gerendertem Inhalt legen, auf ~1.2 s kürzen.
13. **Aufräumen** — tote Dateien (`pages/driver-detail.css`, `site.community.css`,
    `site.track-setups.css`) entfernen, Bundle-Liste an beiden Stellen nachziehen,
    574 Inline-Styles schrittweise in Utilities überführen (nach Seite, nicht in einem Rutsch).

### Verifikation

- `dotnet build` nach jedem Schritt.
- Lokal starten (`dotnet run --project Erdi-ERC --launch-profile http`, Port 5005) und die
  betroffenen Seiten screenshotten — die Selbstkritik am Design läuft über Bilder.
- Kontrastprüfung der Token-Paare.
- Durchklick der Hauptpfade: Home, Racing Hub, Results, Alle Rennen, Renn-Detail, Setups,
  Events, Bewerbung, Kalender, Login, plus eine Admin-Seite.
- **Performance messen, nicht schätzen:** Lighthouse-Audit und ein Trace der Startseite und
  der ergebnisreichsten Seite (Results) gegen die Budgets oben. Gemessen wird auf einem
  gedrosselten Mobilprofil. Netzwerk-Panel: Anzahl und Größe der Requests, und dass **keine**
  Anfrage an eine fremde Domain geht.
- **Der LCP darf nicht hinter der Ampel liegen:** im Trace muss das größte Element deutlich
  vor dem Ende der Sequenz gemalt sein.

---

## Risiken

| Risiko | Umgang |
|---|---|
| Views hängen an Klassennamen, die ersatzlos gestrichen werden | Nur Klassen strikt löschen, deren Vorkommen vorher per grep belegt leer sind; sonst umgestylen |
| `home-index.css` wird unterschätzt, weil sie wie eine Seitendatei aussieht | Sie ist Schritt 5 und wird als Kern behandelt, nicht als Seiten-CSS |
| Der Admin erbt nichts (eigener Namespace, eigenes Stylesheet) | Bewusst: der Admin nimmt nur Tokens mit; darüber hinaus nichts |
| Der Turm zerstört schmale Viewports | Er existiert ab 1200 px; darunter Streifen, im Admin gar nicht |
| `site.bundled.css` wird stillschweigend nicht neu gebaut | Dateiliste an beiden Stellen im csproj pflegen, Bundle nach dem Build prüfen |
| Das eingebettete Layout-JS bricht beim Entfernen von Effekten | Blockweise entfernen, nach jedem Block laden |
| Cadillac (`#000000`) ist als Teamfarbe unsichtbar | Rückfall auf `--ink-3` plus Sekundärstreifen |
| 574 Inline-Styles sind zu viel für einen Durchgang | Nach Seite abarbeiten, priorisiert nach Besucherzahl; kein Big Bang |
| Selbst gehostete Schriften springen beim Nachladen (FOUT/CLS) | `font-display: swap` plus `size-adjust`-Metrik-Override auf eine Systemschrift als Rückfall |
| Beim Beschneiden der Bootstrap Icons geht ein Glyph verloren | Erst **alle** `bi bi-*`-Vorkommen per grep sammeln, dann beschneiden; fehlende Glyphen fallen im Durchklick auf |
| Bootstrap ins Bundle aufnehmen bricht Reihenfolge/Spezifität | Bootstrap zuerst ins Bundle, eigene Ebenen dahinter; danach die drei dichtesten Seiten prüfen |
