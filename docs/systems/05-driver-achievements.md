***REMOVED*** 05 · Driver Achievements

***REMOVED******REMOVED*** Was es ist
Game-artige Erfolge auf jeder Fahrer-Profilseite mit Tier-System
**Bronze · Silber · Gold · Platin**, Fortschritts-Anzeige für gesperrte
Achievements, Filter, Kategorien, „NEU"-Markierung und einem Featured-Badge
im Profil-Header.

***REMOVED******REMOVED*** Datenmodell
`Achievement(Key, Title, Description, Icon, Tone, Tier, Category,
IsUnlocked, Current, Target, UnlockedAt)`

- **Tier**: `bronze` · `silver` · `gold` · `platinum`
- **Category**: `Siege` · `Podien` · `Pace` · `Konstanz` · `Karriere`
- **Current/Target**: für Progress-Bar, auch bei freigeschalteten Items
- **UnlockedAt**: Datum, an dem die Bedingung erfüllt wurde (wenn ableitbar)

***REMOVED******REMOVED*** Helper
`Helpers/DriverAchievementsHelper.cs`
- Definitionen werden zentral als `Def`-Liste geführt – jeder Eintrag liefert
  einen `CurrentSelector` und `UnlockedAtSelector`.
- `Compute(DriverDetailViewModel)` gibt **alle** Achievements zurück
  (auch die noch nicht freigeschalteten) – sortiert: unlocked vor locked,
  innerhalb dessen Platin → Gold → Silber → Bronze.
- `GetFeatured(...)` liefert das höchstwertige freigeschaltete Achievement
  für den Profil-Header.

***REMOVED******REMOVED******REMOVED*** Aktuelle Achievements (Auszug)
| Key | Bedingung | Tier | Kategorie |
|---|---|---|---|
| `first-win` | 1 Sieg | Bronze | Siege |
| `hi-five` | 5 Siege | Silber | Siege |
| `legend` | 10 Siege | Gold | Siege |
| `hall-of-fame` | 25 Siege | Platin | Siege |
| `first-podium` | 1 Podium | Bronze | Podien |
| `podium-machine` | 10 Podien | Silber | Podien |
| `podium-master` | 25 Podien | Gold | Podien |
| `speed-demon` | 1 FL | Bronze | Pace |
| `turbo` | 5 FL | Silber | Pace |
| `purple-sector` | 15 FL | Gold | Pace |
| `perfect-weekend` | Sieg + FL im selben Rennen | Gold | Pace |
| `iceman` | 5 Rennen Streak ohne DNF | Silber | Konstanz |
| `iceman-pro` | 10 Rennen Streak ohne DNF | Gold | Konstanz |
| `metronome` | Ø Finish ≤ 6 (≥ 5 Rennen) | Silber | Konstanz |
| `metronome-elite` | Ø Finish ≤ 4 (≥ 5 Rennen) | Gold | Konstanz |
| `super-sub` | Reservist in den Punkten | Silber | Karriere |
| `veteran` | 20 Rennen | Silber | Karriere |
| `legend-of-the-grid` | 50 Rennen | Gold | Karriere |

***REMOVED******REMOVED*** View
`Views/Home/DriverDetail.cshtml`

- **Featured Badge**: höchstwertiges Achievement direkt neben dem Fahrernamen.
- **Filterleiste**: *Alle · Freigeschaltet · Gesperrt · Platin · Gold · Silber · Bronze*.
- **Gruppierung** nach Kategorie inkl. Counter pro Gruppe (`3 / 7`).
- **Chip-Karten** statt einfacher Inline-Badges:
  - Icon, Titel, Tier-Badge
  - Meta-Zeile mit Datum oder „Fortschritt 7/10"
  - Mini-Progress-Bar am unteren Rand
  - desaturiert + leicht abgedunkelt, wenn `IsLocked`
  - „NEU"-Marker mit Pulse, falls neu gegenüber `localStorage`

***REMOVED******REMOVED******REMOVED*** „NEU"-Markierung
Pro Profil wird unter `localStorage["erc_seen_ach::<league>::<driver>"]` eine
Liste der bereits gesehenen Keys geführt. Beim ersten Anzeigen erscheint
für neu freigeschaltete Achievements ein roter „NEU"-Tag, danach nicht mehr.
Wenn `localStorage` blockiert ist, wird der Tag einfach weggelassen.

***REMOVED******REMOVED*** CSS
`wwwroot/css/site/11-unique-addons.css`

- `.achievements__filters`, `.ach-filter` – Pill-Filter im Team-Theme.
- `.achievements__group`, `.achievements__grid` – Auto-Fill-Grid.
- `.achievement-chip` – Chip-Karte mit Hover-Lift, Focus-Ring (Team-Farbe).
- `.achievement-chip__progress*` – horizontale Progress-Bar.
- `.achievement-chip.is-locked` – Greyscale + reduziertes Brightness.
- `.achievement-chip__new` + `is-new` – pulsierender NEU-Badge.
- `.featured-achievement` – kompakter Hero-Badge.
- Respektiert `prefers-reduced-motion`.

***REMOVED******REMOVED*** Erweitern

***REMOVED******REMOVED******REMOVED*** Neues Achievement
1. Im Helper einen weiteren `Def(...)`-Eintrag hinzufügen:
   - `Key` (eindeutig, kebab-case)
   - `Title`, `Description`
   - `Icon` (Bootstrap-Icon-Klasse, z. B. `bi-rocket-takeoff`)
   - `Tone`, `Tier`, `Category`
   - `Target`
   - `CurrentSelector` und `UnlockedAtSelector`
2. Fertig – View und CSS müssen nicht angefasst werden.

***REMOVED******REMOVED******REMOVED*** Eigene Tier-Farbe
- Block `.achievement-chip--<tier>` in `11-unique-addons.css` ergänzen.
- Falls auch in der Filterleiste sichtbar: Filter-Button + JS-Filter-Liste anpassen.

***REMOVED******REMOVED******REMOVED*** Liga-spezifische Achievements
- Eigene `Compute(League)`-Variante hinzufügen oder im bestehenden
  `Compute` zusätzlich Liga-Kontext nutzen.

***REMOVED******REMOVED*** Accessibility
- Jeder Chip ist `tabindex="0"`, `role="img"` mit beschreibendem `aria-label`.
- Filter-Buttons sind echte `<button>`-Elemente mit `is-active`-State.
- Animationen werden bei `prefers-reduced-motion` deaktiviert.
