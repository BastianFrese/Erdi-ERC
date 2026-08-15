---
datum: 2026-08-15
tags: [entwicklung, feature, bewerbung, apply, admin, season, saison]
status: erledigt
aktualisiert: 2026-08-15
---

# Bewerbungssystem V1 (Rebuild)

Das Bewerbungssystem wurde am 2026-08-05 komplett entfernt (Greenfield-Re-Build). Diese Notiz dokumentiert die neue Architektur.

## Datenmodell

### `Application`
| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `Id` | `varchar(64)` | Primärschlüssel |
| `DiscordId` | `varchar(32)` | OAuth-User-ID (Lookup) |
| `DiscordName` | `varchar(128)` | Anzeige-Name |
| `GamerTag` | `varchar(128)` | EA-/Steam-Game-Tag |
| `Platform` | `varchar(64)` | PC / PS5 / Xbox |
| `TargetLeagueId` | `varchar(64)` FK | Ziel-Liga |
| `Role` | `varchar(32)` | Stammfahrer / Reservefahrer / Probefahrer |
| `Motivation` | `varchar(2000)` | Freitext (optional) |
| `Status` | `int` | 0=Pending, 1=Accepted, 2=Rejected |
| `CreatedAt` | `datetime(6)` | Eingang |
| `DecidedAt` | `datetime(6)?` | Admin-Entscheidung |
| `DecidedByDiscordId` | `varchar(32)?` | Admin |
| `ReviewNote` | `varchar(1000)?` | User-sichtbar (Accepted/Rejected) |
| `DiscordJoinWarning` | `bool` | Snapshot: User fehlt in Community/Liga-Discord |
| `DiscordJoinWarningDetail` | `varchar(500)?` | z.B. "Fehlend: Liga" |
| `Season` | `varchar(16)` | Ziel-Season (z.B. "2026", "2027"), NOT NULL |
| `Season` | `varchar(16)` | Ziel-Season (z.B. "2026", "2027"), NOT NULL |

### `WaitlistEntry`
| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `Id` | `varchar(64)` | PK |
| `DiscordId`, `DiscordName`, `GamerTag`, `Platform` | wie oben | Snapshot |
| `LeagueId` | `varchar(64)` FK | Liga |
| `Position` | `int` | 1-basiert, monoton |
| `Note` | `varchar(500)?` | Admin-Notiz |
| `CreatedAt` | `datetime(6)` | |
| `PromotedToApplicationId` | `varchar(64)?` | Rückverweis nach Promotion |

### Indizes
- `IX_Applications_DiscordId_Status`
- `IX_Applications_Status_CreatedAt`
- `IX_Applications_TargetLeagueId_Status`
- `IX_Applications_TargetLeagueId_Season_Status`
- `IX_WaitlistEntries_DiscordId_LeagueId`
- `IX_WaitlistEntries_LeagueId_Position`
- `IX_WaitlistEntries_LeagueId_Season_Position`
- `IX_WaitlistEntries_DiscordId_LeagueId_Season`

## Service-Schicht

`IApplicationService` (in `Services/IApplicationService.cs`):

```csharp
Task<SubmitApplicationResult> SubmitAsync(SubmitApplicationCommand cmd, CancellationToken ct);
Task<AcceptRejectResult> AcceptAsync(string id, string adminDiscordId, string? note, CancellationToken ct);
Task<AcceptRejectResult> RejectAsync(string id, string adminDiscordId, string? note, CancellationToken ct);
Task<List<Application>> ListAsync(ApplicationStatus? status, string? leagueId, int skip, int take, CancellationToken ct);
Task<Application?> GetByIdAsync(string id, CancellationToken ct);
Task<bool> HasOpenApplicationAsync(string discordId, string leagueId, CancellationToken ct);
Task<List<WaitlistEntry>> ListWaitlistAsync(string leagueId, CancellationToken ct);
Task<PromoteResult> PromoteFromWaitlistAsync(string waitlistEntryId, string adminDiscordId, CancellationToken ct);
```

### Submit-Logik
1. **Validierung**: `TargetLeagueId` muss existieren und darf nicht archiviert sein.
2. **Dedup**: Wenn eine offene `Application` mit `Status=Pending` für `(DiscordId, TargetLeagueId)` existiert → `SubmitOutcome.AlreadyPending`. Analog für Waitlist.
3. **Capacity-Check** (nur Stammfahrer): Wenn `league.Capacity != null && DriverStandings.Count(Stamm) >= Capacity` → Routet auf `AddToWaitlistAsync` (Position = MAX+1).
4. **Ersatzfahrer/Probefahrer**: Bypass Capacity → direkter Insert als `Application`.

### Accept-Logik (transaktional)
1. Finde Liga und Application.
2. **DriverProfile** upsert (DiscordId-basiert).
3. **DriverGamerTag** hinzufügen (Trim+ToLower-Dedup).
5. **DriverStanding** nur für Stammfahrer/Reservefahrer (nicht Probefahrer). Dedup über normalisierten `Driver`-String.
6. **Application.Status = Accepted**, `DecidedAt`, `DecidedByDiscordId`, `ReviewNote`.
7. Webhook `application.accepted` (fire-and-forget).

### Reject-Logik
- `Status = Rejected`, `DecidedAt`, `DecidedByDiscordId`, `ReviewNote`.
- KEIN Profil/Standing-Anlegen.
- Webhook `application.rejected`.

## Discord-Join-Warnung

`IDiscordGuildService.CheckMembershipAsync` liefert `DiscordGuildCheckResult`:
- `Ok` → `JoinedCommunity` + `JoinedLeague` (Boolean).
- `Unavailable` / `LoginExpired` → fail-open, Snapshot in `DiscordJoinWarningDetail`.

Bei `JoinedLeague == false` oder `JoinedCommunity == false` wird die Bewerbung trotzdem gespeichert (Stufe-2-Snapshot), die View zeigt einen Warn-Hinweis.

## Controller

### `ApplicationController` (User, `[Authorize]`)
- `GET /Application/Apply` — Formular mit Ligen-Dropdown + DiscordName (readonly).
- `POST /Application/Apply` — Submit + Waitlist-Routing + Redirect auf `Submitted`.
- `GET /Application/Submitted` — Bestätigungsseite (TempData: Outcome, Waitlist-Position, Discord-Warnung).
- `GET /Application/MyApplication` — Eigene Bewerbungen.

### `AdminApplicationsController` (`[Authorize Policy="Admin.Applications.View"]`)
- `GET /AdminApplications/List` — Filter `?status=Pending&leagueId=pro&page=1`, 25/Seite.
- `GET /AdminApplications/Detail/{id}` — Detail + Decider-Name.
- `POST /AdminApplications/Accept` — `[Authorize(Admin.Applications.Manage)]`.
- `POST /AdminApplications/Reject` — `[Authorize(Admin.Applications.Manage)]`.
- `GET /AdminApplications/Waitlist` — Liga-Selector + Promoten-Form.
- `POST /AdminApplications/PromoteFromWaitlist` — `[Authorize(Admin.Applications.Manage)]`.

Alle POSTs: `[ValidateAntiForgeryToken]` + `[EnableRateLimiting("forms")]`.

## Permissions

- `AdminPermissions.ApplicationsView` → Lesen (List, Detail, Waitlist).
- `AdminPermissions.ApplicationsManage` → Accept/Reject/Promote.
- Standardrollen `superadmin` und `applications` decken beides ab.

## Migrationen

- `20260810094420_AddApplicationsAndWaitlist` — erstellt die beiden Tabellen + Indizes. Manuell korrigiert: ApplicationForms-Drop ist bereits in `20260805150724_DropApplicationForms` passiert → Doppeldrop entfernt.
- `20260815143225_AddApplicationSeason` — `Season`-Spalte auf `Applications` + `WaitlistEntries` (NOT NULL), `CurrentSeason`/`NextSeason`/`ApplicationsOpenForNextSeason` auf `Leagues`, neue Indizes. **Backfill-SQL**: Bestehende Apps kriegen `Season = League.CurrentSeason` (Fallback `"current"`), damit die NOT-NULL-Spalte ohne Fehler gefüllt werden kann.

**NUR auf `erditest` anwenden**, niemals automatisch auf Prod (siehe `Docs/MIGRATION-SAFETY.md`).

## Tests

138 Tests grün, davon 41 neue:
- `ApplicationServiceTests` (21) — Submit-Pfade, Capacity, Dedup, Accept/Reject, Promote.
- `ApplicationControllerTests` (9) — User-Controller mit `FakeDiscordGuildService`.
- `AdminApplicationsControllerTests` (11) — Admin-Controller mit TestAuthHelper.

Stand 2026-08-15: 197 Tests grün (9 neue Season-Tests dazugekommen).

## Season-Awareness (Erweiterung 2026-08-15)

Bewerber können sich für die **nächste** Saison bewerben, bevor die aktuelle zu Ende ist. Pro Liga lässt sich eine `NextSeason` definieren und ein Flag setzen, ob die Bewerbungen dafür schon offen sind.

### Liga-Konfiguration

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `CurrentSeason` | `varchar(16)?` | Aktive Season (z.B. `"2026"`) |
| `NextSeason` | `varchar(16)?` | Folgesaison (z.B. `"2027"`) |
| `ApplicationsOpenForNextSeason` | `bool` | Bewerben für `NextSeason` aktiviert? |

### Targeting-Logik (`IApplicationTargetingService`)

Pro Liga wird die Ziel-Season aufgelöst:
- Wenn `ApplicationsOpenForNextSeason` **und** `NextSeason` gesetzt → `TargetSeason = NextSeason`, `IsNextSeason = true`.
- Sonst → `TargetSeason = CurrentSeason` (oder `"current"` als Fallback), `IsNextSeason = false`.

`GetTargetingInfoAsync` liefert pro Liga einen `LeagueTargetingInfo`-Datensatz (fürs `Apply`-Hero) sowie `ResolveTargetSeasonAsync(leagueId)` für den `SubmitAsync`-Pfad.

### Capacity-Scoping

`SubmitAsync` zählt Capacity nur **innerhalb der Ziel-Season**:
- `acceptedInSeason` = `Application.Status == Accepted && Season == targetSeason`.
- `historicalStandings` = `DriverStandings.LeagueId` (alle Seasons, weil Standings zwischen Seasons persistieren können).
- `occupied = max(acceptedInSeason, historicalStandings)`.
- Wenn `occupied >= Capacity` → Waitlist statt Pending.

Damit blockieren alte Standings aus 2025 nicht einen neuen 2026-Stamm-Slot, sobald 2026 wieder offen ist.

### Dedup-Scope

Bisher: `(DiscordId, TargetLeagueId)` → ein User kann maximal eine offene Bewerbung pro Liga haben.
Neu: `(DiscordId, TargetLeagueId, Season)` → ein User kann sich für 2026 und 2027 parallel bewerben, solange die Seasons unterschiedlich sind.

### Saison-Wechsel (Admin)

`ApplicationService.CloseSeasonAsync(leagueId, fromSeason, toSeason, mode, adminDiscordId, ct)`:

- **`Rollover`**: Pending-Applications + Waitlist werden in `toSeason` verschoben, Liga-`CurrentSeason`/`NextSeason` werden **nicht** automatisch geändert (Admin macht das separat nach Entscheidung).
- **`RejectAll`**: Pending-Applications werden auf `Rejected` gesetzt (mit `ReviewNote = "Saison {fromSeason} geschlossen durch {adminDiscordId}"`), Waitlist-Einträge gelöscht.

Beide Pfade: Audit (`CloseSeason` auf `League`) + Webhook `application.season.closed` mit `MovedApplications`, `MovedWaitlist`, `RejectedApplications`, `Actor`.

### Admin-UI

- `GET /AdminApplications/Seasons` — Übersicht pro Liga+Season (Pending/Accepted/Rejected/Waitlist-Counts).
- `GET /AdminApplications/LeagueSeasons/{leagueId}` — Tabs pro Season, mit Close-Season-Aktion (Rollover/RejectAll).
- `GET /AdminApplications/List?season=2027` — Filter nach Season.

### User-UX

- `Apply`-Hero zeigt „Du bewirbst dich für Season 2027 [Vorschau]"-Banner, wenn alle Ligen auf dieselbe NextSeason mappen.
- `MyApplication` gruppiert Apps + Waitlist nach Season (neueste zuerst).
- Liga-Karten kennzeichnen NextSeason mit `f1-chip--info`, CurrentSeason mit `f1-chip--ghost`.

## Deployment-Checkliste

1. ✅ Code deployen (CI).
2. ⚠️ **Migration `20260810094420_AddApplicationsAndWaitlist` manuell auf `erditest`** via `ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --project Erdi-ERC.csproj --startup-project Erdi-ERC.csproj`.
3. ⚠️ **Migration `20260815143225_AddApplicationSeason` manuell auf `erditest`** (selbe Methode). Backfill-SQL füllt `Season` aus `League.CurrentSeason` (Fallback `"current"`).
4. ❌ **Niemals auf `erdierc` automatisch ausführen** — Prod bleibt unverändert bis manuelle Freigabe.
5. Sidebar-Eintrag "Bewerbungen" ist nur sichtbar für Admins mit `Admin.Applications.View`.
6. Ligen mit `ApplicationsOpenForNextSeason = true` müssen nach Season-Aktivierung das Liga-`CurrentSeason`/`NextSeason` separat setzen (Close-Season-Rollover ändert nur Apps, nicht das League-Entity).

## Siehe auch

- `Docs/Tasks/2026-08-05-bewerbungssystem-v1-offene-punkte.md` — Klärungen vor Commit 1.
- `Docs/MIGRATION-SAFETY.md` — Wie Migrationen auf Prod laufen.
- Memory: `project_application_rebuild_v1_aug2026.md`.