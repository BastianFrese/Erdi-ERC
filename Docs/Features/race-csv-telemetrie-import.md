---
datum: 2026-09-14
tags: [entwicklung, feature, renn-ergebnisse, csv, telemetrie, admin, import]
status: erledigt
---

# CSV-Rennimport: ERDi-Telemetrie-Export (Semicolon-Format)

## Kurzbeschreibung

Beim **Rennen eintragen** (`EnterRace` → `POST /AdminLeague/ParseRaceCsv`) versteht der
Import jetzt **zusätzlich** zum F1-Spiel-Export (Komma-Format) das **semicolon-getrennte
CSV-Export** der ERDi-Telemetrie-App (`ResultsExporter.WriteCsv`). Das Format wird
automatisch erkannt; der bestehende Komma-Pfad bleibt unverändert.

```
position;name;team;raceNumber;numLaps;gridPosition;points;resultStatus;bestLapMs;totalRaceSeconds;penaltiesTime;numPenalties
```

## Feld-Mapping (Telemetrie → Formular)

| CSV-Spalte | Erdi-ERC Verwendung |
|---|---|
| `position` | Positions-Slot (1-basiert) |
| `name` | Fahrer-Dropdown (case-insensitive Match gegen Liga-Fahrerliste) |
| `team` | Tooltip-Hinweis bei unbekanntem Fahrer |
| `gridPosition` | Quali-Feld (`qualiPositions`) |
| `totalRaceSeconds` + `penaltiesTime` | Gesamtzeit = `(totalRaceSeconds + penaltiesTime) * 1000` ms |
| `resultStatus` | `IsDnf = true` für alles ≠ `Finished` (auch DidNotFinish, Disqualified, Retired …) |
| `bestLapMs` | min über Nicht-DNF-Fahrer → `fastestLapDriver` (hidden Input, schnellste Runde) |
| `raceNumber`, `numLaps`, `points`, `numPenalties` | nicht übernommen |

## Format-Erkennung

1. Kopfzeile mit Token **`resultStatus`** in den ersten 10 nicht-leeren Zeilen → Spalten-
   Indizes aus der Kopfzeile (token-basiert, Exporter-Reihenfolge als Default).
2. Fallback ohne Kopfzeile: Datenzeilen semicolon-getrennt, ≥9 Felder, Feld 0 ganzzahlig +
   Feld 7 bekannter ResultStatus-String → feste Default-Indizes.

Der Spiel-Export hat keine Semikola/Kollisionen → Erkennung ist eindeutig.

## Änderungen

| Datei | Änderung |
|-------|----------|
| `Erdi-ERC/Services/RaceCsvParser.cs` | `RaceCsvEntry` + `int? QualifyingPosition`; `RaceCsvParseResult` + `string? FastestLapDriver`; `SplitCsvLine` → quote-sicheres `SplitDelimited(line, char)` für Komma **und** Semikolon; `TryDetectTelemetry` / `ReadTelemetryHeader` / `ParseTelemetry`; `KnownResultStatus`-Liste; `TelemetryColumns`-Record + Default-Index-Order; bounds-sichere `Field`-Accessor |
| `Erdi-ERC/Controllers/AdminLeagueController.cs` | `ParseRaceCsv`-JSON: pro Entry `qualiPosition`, top-level `fastestLapDriver` |
| `Erdi-ERC/Views/Admin/EnterRace.cshtml` | `applyRaceCsv`-JS: `qualiPositions[]` + `fastestLapDriver`-hidden Input füllen; Hilfetext um Telemetrie-Format ergänzt. Quali-Werte werden NUR bei Telemetrie-Import (hat Grid-Daten) überschrieben — der Komma-Spiel-Export leert manuell eingegebene Quali-Felder nicht; DNF-Zeilen werden auch befüllt (tragen gridPosition); `refreshRaceFieldArrays` synchronisiert `qualiInputs` nach Drag&Drop/Sortierung; `fastestLapDriver` wird geleert, wenn kein Wert kommt |
| `tests/Erdi-ERC.Tests/RaceCsvParserTests.cs` | 11 neue Telemetrie-Tests (Sample des Users, 21 Autos) |

## Tests

`RaceCsvParserTests` (neu): komplettes 21-Auto-Sample → Einträge/Fahrer/Team korrekt, Skip
nur Kopfzeile · Gesamtzeit inkl. Strafe (LUKS 2680.616 + 10 s ≈ 2 690 616 ms) · DNF aus
`resultStatus` (DidNotFinish, Disqualified …) · `gridPosition` → `QualifyingPosition` ·
`bestLapMs` → `FastestLapDriver` (Nicht-DNF, min) · Name mit `;` als EIN Feld (Quoting) ·
Headerlos-Fallback. Bestehende Komma/Gap-Tests unverändert grün.

Gesamt: **383 Tests grün** (382 bisher + 1 neu), Build grün. Code-Review: 0 CRITICAL/0 HIGH, 3 MEDIUM + 2 LOW behoben (s. o.), Rest LOW als Note belassen.

## Deployment

Deploy nur auf **erditest** (keine Migration nötig, keine DB-Änderung), Prod (erdierc)
nicht anfassen. Verwandt: [[project_csv_race_import]].
