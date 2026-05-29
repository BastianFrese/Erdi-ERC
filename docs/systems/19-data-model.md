***REMOVED*** 19 · Datenmodell (Kurzreferenz)

***REMOVED******REMOVED*** Was es ist
Überblick über die wichtigsten Models und ihre Beziehungen, damit
neue Features schnell die richtigen DbSets nutzen.

***REMOVED******REMOVED*** Wichtige Models (Auszug)

***REMOVED******REMOVED******REMOVED*** `League`
- `Id`, `Name`
- `Races` (Liste)
- `Standings` (Liste `DriverStanding`)

***REMOVED******REMOVED******REMOVED*** `Race`
- `Id`, `Date`, `Track`
- `Finishes` (Liste `RaceFinish`)
- gehört zu einer `League`

***REMOVED******REMOVED******REMOVED*** `RaceFinish`
- `Driver`, `Position`, `FastestLap`, `Points`
- `Position <= 0` ⇒ DNF/DNS

***REMOVED******REMOVED******REMOVED*** `RaceResult`
- aggregierte Sicht für die Loader/Theme-Logik
- Felder u. a. `Date`, `Winner`, `League`

***REMOVED******REMOVED******REMOVED*** `DriverStanding`
- `Driver`, `Team`, `Points`
- Quelle für die Team-Zuordnung im Layout

***REMOVED******REMOVED******REMOVED*** `RaceReserveAssignment`
- mappt Reservisten-Einsätze auf Teams (für Sieger-Theme & Achievements)

***REMOVED******REMOVED******REMOVED*** `StreamSchedule`
- `Title`, `StartAt`, `DurationMinutes`, `Url`
- Quelle für den **Live-Race-Mode** (siehe `04-live-race-mode.md`)

***REMOVED******REMOVED******REMOVED*** `DriverDetailViewModel`
- Aggregat für die Profilseite
- Felder u. a. `LeagueId`, `LeagueName`, `Driver`, `TotalPoints`, `Wins`, `Podiums`,
  `FastestLaps`, `BestFinish`, `AverageFinish`, `Races`, `Achievements`, `Rivalries`

***REMOVED******REMOVED*** DbContext
`AppDbContext` (DI-injiziert) stellt die DbSets bereit.
Layout greift bewusst nur **lesend** und **defensiv** (try/catch) zu, damit
kein DB-Fehler die Seite zerschießen kann.

***REMOVED******REMOVED*** Erweitern
- Neues Model: in `Models/` anlegen, im DbContext registrieren, Migration erzeugen.
- Aggregat für eine neue Page-Variante: ein `*ViewModel` neben `<OWNER_HANDLE>10ViewModel.cs`
  pflegen, damit Views entkoppelt bleiben.
