---
datum: 2026-08-10
tags: [entwicklung, feature, bewerbung, apply, admin]
status: erledigt
---

***REMOVED*** Bewerbungssystem V1 (Rebuild)

Das Bewerbungssystem wurde am 2026-08-05 komplett entfernt (Greenfield-Re-Build). Diese Notiz dokumentiert die neue Architektur.

***REMOVED******REMOVED*** Datenmodell

***REMOVED******REMOVED******REMOVED*** `Application`
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

***REMOVED******REMOVED******REMOVED*** `WaitlistEntry`
| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `Id` | `varchar(64)` | PK |
| `DiscordId`, `DiscordName`, `GamerTag`, `Platform` | wie oben | Snapshot |
| `LeagueId` | `varchar(64)` FK | Liga |
| `Position` | `int` | 1-basiert, monoton |
| `Note` | `varchar(500)?` | Admin-Notiz |
| `CreatedAt` | `datetime(6)` | |
| `PromotedToApplicationId` | `varchar(64)?` | Rückverweis nach Promotion |

***REMOVED******REMOVED******REMOVED*** Indizes
- `IX_Applications_DiscordId_Status`
- `IX_Applications_Status_CreatedAt`
- `IX_Applications_TargetLeagueId_Status`
- `IX_WaitlistEntries_DiscordId_LeagueId`
- `IX_WaitlistEntries_LeagueId_Position`

***REMOVED******REMOVED*** Service-Schicht

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

***REMOVED******REMOVED******REMOVED*** Submit-Logik
1. **Validierung**: `TargetLeagueId` muss existieren und darf nicht archiviert sein.
2. **Dedup**: Wenn eine offene `Application` mit `Status=Pending` für `(DiscordId, TargetLeagueId)` existiert → `SubmitOutcome.AlreadyPending`. Analog für Waitlist.
3. **Capacity-Check** (nur Stammfahrer): Wenn `league.Capacity != null && DriverStandings.Count(Stamm) >= Capacity` → Routet auf `AddToWaitlistAsync` (Position = MAX+1).
4. **Ersatzfahrer/Probefahrer**: Bypass Capacity → direkter Insert als `Application`.

***REMOVED******REMOVED******REMOVED*** Accept-Logik (transaktional)
1. Finde Liga und Application.
2. **DriverProfile** upsert (DiscordId-basiert).
3. **DriverGamerTag** hinzufügen (Trim+ToLower-Dedup).
5. **DriverStanding** nur für Stammfahrer/Reservefahrer (nicht Probefahrer). Dedup über normalisierten `Driver`-String.
6. **Application.Status = Accepted**, `DecidedAt`, `DecidedByDiscordId`, `ReviewNote`.
7. Webhook `application.accepted` (fire-and-forget).

***REMOVED******REMOVED******REMOVED*** Reject-Logik
- `Status = Rejected`, `DecidedAt`, `DecidedByDiscordId`, `ReviewNote`.
- KEIN Profil/Standing-Anlegen.
- Webhook `application.rejected`.

***REMOVED******REMOVED*** Discord-Join-Warnung

`IDiscordGuildService.CheckMembershipAsync` liefert `DiscordGuildCheckResult`:
- `Ok` → `JoinedCommunity` + `JoinedLeague` (Boolean).
- `Unavailable` / `LoginExpired` → fail-open, Snapshot in `DiscordJoinWarningDetail`.

Bei `JoinedLeague == false` oder `JoinedCommunity == false` wird die Bewerbung trotzdem gespeichert (Stufe-2-Snapshot), die View zeigt einen Warn-Hinweis.

***REMOVED******REMOVED*** Controller

***REMOVED******REMOVED******REMOVED*** `ApplicationController` (User, `[Authorize]`)
- `GET /Application/Apply` — Formular mit Ligen-Dropdown + DiscordName (readonly).
- `POST /Application/Apply` — Submit + Waitlist-Routing + Redirect auf `Submitted`.
- `GET /Application/Submitted` — Bestätigungsseite (TempData: Outcome, Waitlist-Position, Discord-Warnung).
- `GET /Application/MyApplication` — Eigene Bewerbungen.

***REMOVED******REMOVED******REMOVED*** `AdminApplicationsController` (`[Authorize Policy="Admin.Applications.View"]`)
- `GET /AdminApplications/List` — Filter `?status=Pending&leagueId=pro&page=1`, 25/Seite.
- `GET /AdminApplications/Detail/{id}` — Detail + Decider-Name.
- `POST /AdminApplications/Accept` — `[Authorize(Admin.Applications.Manage)]`.
- `POST /AdminApplications/Reject` — `[Authorize(Admin.Applications.Manage)]`.
- `GET /AdminApplications/Waitlist` — Liga-Selector + Promoten-Form.
- `POST /AdminApplications/PromoteFromWaitlist` — `[Authorize(Admin.Applications.Manage)]`.

Alle POSTs: `[ValidateAntiForgeryToken]` + `[EnableRateLimiting("forms")]`.

***REMOVED******REMOVED*** Permissions

- `AdminPermissions.ApplicationsView` → Lesen (List, Detail, Waitlist).
- `AdminPermissions.ApplicationsManage` → Accept/Reject/Promote.
- Standardrollen `superadmin` und `applications` decken beides ab.

***REMOVED******REMOVED*** Migrationen

- `20260810094420_AddApplicationsAndWaitlist` — erstellt die beiden Tabellen + Indizes. Manuell korrigiert: ApplicationForms-Drop ist bereits in `20260805150724_DropApplicationForms` passiert → Doppeldrop entfernt.

**NUR auf `erditest` anwenden**, niemals automatisch auf Prod (siehe `Docs/MIGRATION-SAFETY.md`).

***REMOVED******REMOVED*** Tests

138 Tests grün, davon 41 neue:
- `ApplicationServiceTests` (21) — Submit-Pfade, Capacity, Dedup, Accept/Reject, Promote.
- `ApplicationControllerTests` (9) — User-Controller mit `FakeDiscordGuildService`.
- `AdminApplicationsControllerTests` (11) — Admin-Controller mit TestAuthHelper.

***REMOVED******REMOVED*** Deployment-Checkliste

1. ✅ Code deployen (CI).
2. ⚠️ **Migration `20260810094420_AddApplicationsAndWaitlist` manuell auf `erditest`** via `ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --project <OWNER_HANDLE>-ERC.csproj --startup-project <OWNER_HANDLE>-ERC.csproj`.
3. ❌ **Niemals auf `erdierc` automatisch ausführen** — Prod bleibt unverändert bis manuelle Freigabe.
4. Sidebar-Eintrag "Bewerbungen" ist nur sichtbar für Admins mit `Admin.Applications.View`.

***REMOVED******REMOVED*** Siehe auch

- `Docs/Tasks/2026-08-05-bewerbungssystem-v1-offene-punkte.md` — Klärungen vor Commit 1.
- `Docs/MIGRATION-SAFETY.md` — Wie Migrationen auf Prod laufen.
- Memory: `project_application_rebuild_v1_aug2026.md`.