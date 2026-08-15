---
datum: 2026-08-15
tags: [entwicklung, feature, bewerbung, season, task-trail]
status: erledigt
---

# Saison-Awareness im Bewerbungsprozess

Bewerber können sich für die **nächste** Saison bewerben, bevor die aktuelle zu Ende ist. Pro Liga eigenes `NextSeason`-Feld + `ApplicationsOpenForNextSeason`-Flag. Admin kann im Dashboard Saisons verwalten (Übersicht, Filter, Saisonschluss-Aktion).

## Architektur

```
League (CurrentSeason, NextSeason, ApplicationsOpenForNextSeason)
   ↓
ApplicationTargetingService (ResolveSeasonAsync, GetTargetingInfoAsync)
   ↓
ApplicationService (SubmitAsync mit Season-Persistenz + Dedup/Capacity-Scope)
   ↓
ApplicationController + AdminApplicationsController (Season-Filter, neue Endpoints)
```

## Commits (atomar, innen→außen)

1. **Domain** — `Application.Season` + `WaitlistEntry.Season` (varchar(16), NOT NULL) + `League.CurrentSeason`/`NextSeason`/`ApplicationsOpenForNextSeason`.
2. **EF-Konfig + Migration** — Column-Mapping, neue Indizes (`IX_Applications_TargetLeagueId_Season_Status`, `IX_WaitlistEntries_LeagueId_Season_Position`, `IX_WaitlistEntries_DiscordId_LeagueId_Season`). Backfill-SQL: `UPDATE Applications JOIN Leagues ... SET Season = League.CurrentSeason`.
3. **Service** — `IApplicationTargetingService` (neu), `ApplicationService.SubmitAsync` → `ResolveTargetSeasonAsync`, Dedup-Scope `(DiscordId, LeagueId, Season)`, Capacity `max(acceptedInSeason, historicalStandings)`. Neue Methoden: `GetSeasonSummaryAsync`, `CloseSeasonAsync` (Rollover/RejectAll). Webhook `application.season.closed`.
4. **Controller** — `AdminApplicationsController`: `season`-Query-Param, neue Aktionen `Seasons`, `LeagueSeasons`, `CloseSeason`. `ApplicationController.Apply` füllt `ViewBag.TargetSeason`/`IsNextSeason`.
5. **User-Views** — `Apply.cshtml` mit Hero-Subtitle („Du bewirbst dich für Season 2027 [Vorschau]"), Liga-Karten mit Season-Chips. `MyApplication.cshtml` gruppiert nach Season.
6. **Admin-Views** — `Seasons.cshtml` (Übersicht pro Liga+Season), `LeagueSeasons.cshtml` (Tab-View mit Close-Season-Aktion), `List.cshtml` Season-Filter in Filter-Bar.
7. **Tests** — 9 neue Season-Tests (Persistenz, Dedup-Scope, Capacity-Scope, GetSeasonSummary, CloseSeason Rollover+RejectAll, TargetingInfo). 197 Tests grün.
8. **Doku** — `Docs/Features/applications.md` Season-Sektion, dieser Task-Trail, Memory-Eintrag.

## Schlüsselentscheidungen

- **Season als Freitext (varchar(16))** statt Enum — kein Saison-Backfill-Code-Re-Run nötig.
- **Beide Modi (Rollover + RejectAll)** — Admins sollen flexibel entscheiden können, je nach Saison-Ende-Szenario.
- **Default = Season der Application** — SubmitAsync nimmt immer Liga-CurrentSeason oder Liga-NextSeason (je nach OpenForNext-Flag). Kein Override-Dropdown im Formular, weil das die Konsistenz mit Liga-Konfig garantiert.
- **CloseSeason ändert Liga.CurrentSeason NICHT** — das passiert separat im Liga-Edit. Sonst könnte ein Admin zwei CloseSeason-Pfade koppeln, was Liga-Workflows kompliziert. Aktuell: Rollover moved Apps, Admin macht danach manuell den Liga-Season-Wechsel.
- **Backfill mit JOIN Leagues** — sauber, idempotent, fällt auf `"current"` zurück.
- **Capacity `max(acceptedInSeason, historicalStandings)`** — nicht `sum`, weil Standings über Seasons persistieren aber nur die aktive Season für Stamm-Slots zählt.

## Offene Punkte

- **Keine** — Feature ist komplett ausgeliefert. Backfill deckt Altdaten ab.

## Deployment

- `erditest` manuell: `ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --project Erdi-ERC.csproj --startup-project Erdi-ERC.csproj`
- `erdierc` (Prod): **NICHT ANFASSEN** — bleibt auf altem Schema (siehe `Docs/MIGRATION-SAFETY.md`).
- Nach Deploy: Ligen müssen `CurrentSeason`/`NextSeason` einmalig in der DB gesetzt bekommen (Admin-Dashboard aktuell ohne Saison-Edit-Form — manuelle SQL notwendig).
