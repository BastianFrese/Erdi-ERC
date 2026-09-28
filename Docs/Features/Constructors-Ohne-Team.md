---
datum: 2026-09-25
tags: [entwicklung, bugfix, constructors, teamwertung, stammdaten, admin]
status: erledigt
aktualisiert: 2026-09-25
---

# Constructors-Wertung: die „Ohne Team"-Zeile

Entstanden aus dem Bugreport „In der constructors championchip, bei 'Ohne Team' schauen,
welche Fahrer dort drinne 'Stecken' damit generell kein 'Ohne Team' mehr in der constructors
championchip steht".

## Woher die Zeile kam

Die Teamwertung wird **nicht** aus einer gespeicherten Team-Spalte je Rennen gebildet.
`RaceTeamHelper.ResolveTeamForRaceDriver` löst das Team für jedes Finish zur Laufzeit auf,
in dieser Reihenfolge:

1. rennbezogene Ersatzfahrer-Zuordnung (`RaceReserveAssignments`) → Team des Hauptfahrers
2. ligaübergreifende Gast-Zuordnung (`RaceGuestAssignments`) → Team des Hauptfahrers
3. **eigenes `DriverStanding.Team`**
4. `IsReserveDriver && ReserveForDriver` → Team des Hauptfahrers
5. sonst `null` → Bucket „Ohne Team"

Der Bucket entstand damit für **jede** Standings-Zeile ohne auflösbares Team — auch für
Fahrer mit 0 Punkten. In Prod stand so eine Zeile in der Tabelle, die zu keiner Zahl
beitrug.

## Die Regel

Liegt einmalig in `Helpers/ConstructorTeamHelper.cs`:

- `LabelFor(team)` — leeres Team → „Ohne Team", sonst getrimmt
- `IsNoTeamBucket(name)` — Vergleich case-insensitiv und getrimmt
- `IsVisibleConstructor(team, points)` — `points > 0 || !IsNoTeamBucket(team)`

Also: **ein echtes Team bleibt auch mit 0 Punkten sichtbar, der Bucket nur, wenn er Punkte
trägt.** Der Bucket darf nicht einfach gelöscht werden — sonst verschwinden seine Punkte
still aus der Teamwertung, statt aufzufallen (Signal an den Admin: Stammdaten fehlen).

Verwendet an drei Stellen, damit die Regel nicht dreimal getippt wird:

| Stelle | Datei |
| --- | --- |
| Constructors-Tabelle der Ligaseite | `Views/Races/LeagueResults.cshtml` |
| Overlay-Export `GET /api/telemetry/standings` | `Services/StandingsExportService.cs` |
| Ewige Liste (Blatt „Teams") | `Controllers/StatsController.cs` |

Dort wird der Bucket gar nicht erst geführt: der Finish-Filter verlangt ein nicht-leeres
Team, die Zeile hätte also nie Zahlen.

Die Positionsnummerierung folgt dem Filter (`position++` nach dem `Where`), bleibt also
lückenlos.

## Stammdaten-Fix in Prod (2026-09-25)

Read-only-Analyse über `DriverStandings`: 112 Standings, davon nur **3 teamlos**, alle
`IsReserveDriver = 1` mit `ReserveForDriver = NULL`:

| Liga | Fahrer | Punkte | Finish |
| --- | --- | --- | --- |
| second | `ERC \| Lungentorpedo` | 18 | Imola P3 |
| second | `ERC \| xLolxxd` | 0 | keins |
| rookie | `Knotzi` | 0 | keins |

Nur Lungentorpedo trug Punkte. Für Imola existierte **keine** Zuordnung
(`RaceReserveAssignments` Imola: michi582015→Erdi0815, xHermann90→holymoly,
Fc_Bayern08→Shox, Schnitzelbrot→Massa), deshalb löste Stufe 3 auf `null` auf.

**Der Normalfall in Prod ist ein eigenes Team am Reservisten** — alle anderen Reservisten
tragen es (`Dr_Ducman` → Red Bull, `Maumau` → Cadillac …). Schlagender Beweis: `ERC | Terntor`
(Ferrari-Reserve) fuhr Imola P12 **ebenfalls ohne Zuordnung** und wurde allein durch sein
eigenes `Team = Ferrari` gerettet.

Gesetzt wurde deshalb `Team` in der Standings-Zeile, nicht `ReserveForDriver` — Stufe 3
wirkt in der Teamwertung genauso und füllt zusätzlich die Team-Spalte der Fahrertabelle
(nach Vorbild `AdminLeagueController.SaveAllStandings`: `team?.Trim()`).

```sql
-- Rollback-Punkt: RowId 102, Team war vorher leer
UPDATE DriverStandings SET Team='Ferrari'
WHERE RowId = 102 AND LeagueId='second' AND (Team IS NULL OR TRIM(Team)='');
```

„Letztes Team" ist aus den Daten **nicht** ableitbar: `RaceFinishes`/`RaceResults` haben
keine Team-Spalte, `DriverRoleHistories` ist leer und kennt ohnehin nur Rollen. Der einzige
Hinweis war das Profil (`iambetterxp` / `ERC | Lungentorpedo`, `FavoriteTeam = ferrari`);
die Bestätigung kam vom Betreiber.

Gemessene Wirkung über die öffentliche API: Ferrari `second` **70 → 88 Punkte, Rang 5 → 4**
(vorbei an Racing Bulls mit 78). Teamlos mit Punkten gibt es danach ligaweit keinen mehr.

## Tests

- `tests/Erdi-ERC.Tests/Helpers/ConstructorTeamHelperTests.cs` — 11 Fälle
- `tests/Erdi-ERC.Tests/Services/StandingsExportServiceTeamTests.cs` — 3 Fälle
  (Bucket ohne Punkte weg, echtes Team mit 0 Punkten bleibt, Bucket **mit** Punkten bleibt)

Razor-Views laufen in den Tests nicht mit — die Regel liegt deshalb bewusst in
`ConstructorTeamHelper` statt im View, so ist sie überhaupt prüfbar.

## Angleichung der Gesamtwertung (2026-09-25)

`OverallConstructorsService` verwarf Finishes ohne auflösbares Team per `continue` — die
Ligaseite zählte sie im Bucket mit, dieselben Rennen ergaben also zwei verschiedene Summen.
Jetzt zählen alle drei Stellen dieselben Finishes:

- Der Bucket-Name „Ohne Team" ist überall **immer** ein Kandidat
  (`Append(ConstructorTeamHelper.NoTeamLabel)` in `Views/Races/LeagueResults.cshtml` und
  `Services/StandingsExportService.cs`). Vorher kam er ausschließlich aus leeren
  Standings-Zeilen: in einer Liga mit voll besetztem Kader fielen die Punkte eines Gastes
  ohne Zuordnung still aus der Wertung.
- In `OverallConstructorsService` ist dafür **kein** `IsVisibleConstructor`-Filter nötig —
  der Bucket entsteht dort erst durch ein Finish mit Punkten, kann also nie bei 0 stehen.
  Er bekommt den eigenen `CssKey` `no-team`, damit unbekannte Teamnamen auf dem
  Fallback `"unknown"` nicht hineinlaufen (sonst würden zwei Konstrukteure addiert).

Bewusster Unterschied bleibt: die Ligaseite listet auch **echte** Teams mit 0 Punkten, die
Gesamtwertung nur punktende Konstrukteure — sie ist eine Punkttabelle („Es gibt noch keine
Constructor-Punkte").

Wirkung in Prod: **keine**. Die beiden einzigen Finishes ohne Standings-Zeile sind
`ERC | SpeedPhantom` (`pro`, P18) und `el arzeus` (`second`, P16) — beide außerhalb der
Punkteränge.

Deployt am 2026-09-25; seither liefert `GET /api/telemetry/standings` in keiner Liga mehr
eine „Ohne Team"-Zeile.

## Offen

- Die beiden 0-Punkte-Fälle (`ERC | xLolxxd`, `Knotzi`) haben weiterhin kein Team in den
  Stammdaten. In der Teamwertung unsichtbar (Regel oben), in der **Fahrer**zeile steht
  weiterhin „Ohne Team" — kosmetisch, bei Bedarf füllbar.
