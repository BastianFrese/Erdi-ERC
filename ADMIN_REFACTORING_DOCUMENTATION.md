***REMOVED*** Admin-Verwaltungsbereich - Refactoring Dokumentation

***REMOVED******REMOVED*** Überblick

Der monolithische `AdminController` (97 KB, 2200+ Zeilen) wurde in eine **modulare, gut strukturierte Architektur** aufgeteilt.

***REMOVED******REMOVED*** Neue Struktur

***REMOVED******REMOVED******REMOVED*** Services (Geschäftslogik)

***REMOVED******REMOVED******REMOVED******REMOVED*** 1. **ApplicationService** (`IApplicationService`, `ApplicationService.cs`)
- **Zweck**: Verwaltung von Bewerbungen
- **Methoden**:
  - `GetAllApplicationsAsync()` - Alle Bewerbungen abrufen
  - `GetApplicationsByDivisionAsync(division)` - Bewerbungen nach Division filtern
  - `DeleteApplicationAsync(id)` - Bewerbung löschen
  - `AcceptApplicationAsync(id, actorId)` - Bewerbung annehmen
  - `RemoveExpiredAcceptedApplicationsAsync()` - Abgelaufene akzeptierte Apps entfernen

***REMOVED******REMOVED******REMOVED******REMOVED*** 2. **CommunityContentService** (`ICommunityContentService`, `CommunityContentService.cs`)
- **Zweck**: Verwaltung von Community-Inhalten (News, Voting, Highlights)
- **Methoden**:
  - `GetRecentNewsAsync(count)` - Aktuelle News abrufen
  - `GetRecentVotesAsync(count)` - Aktuelle Umfragen abrufen
  - `GetRecentHighlightsAsync(count)` - Aktuelle Highlights abrufen
  - `GetPublicPenaltiesAsync(count)` - Öffentliche Strafen abrufen
  - `SaveNewsPostAsync(...)` - News speichern
  - `SaveVotePollAsync(...)` - Umfrage speichern
  - `SaveHighlightClipAsync(...)` - Highlight speichern

***REMOVED******REMOVED******REMOVED******REMOVED*** 3. **MediaService** (`IMediaService`, `MediaService.cs`)
- **Zweck**: Verwaltung von Dateien (Musik, Event-Bilder, Ewige Liste)
- **Methoden**:
  - Background Music:
    - `GetBackgroundMusicFilesAsync()` - Musikdateien auflisten
    - `UploadBackgroundMusicAsync(file)` - Musik hochladen
    - `DeleteBackgroundMusicAsync(fileName)` - Musik löschen
  - Event Images:
    - `SaveEventImageAsync(image)` - Event-Bild speichern
    - `TryDeleteEventImage(fileName)` - Event-Bild löschen (best effort)
  - Ewige Liste:
    - `UploadEwigeListeAsync(workbook)` - XLSX hochladen

***REMOVED******REMOVED******REMOVED*** Controller (HTTP-Handling)

***REMOVED******REMOVED******REMOVED******REMOVED*** 1. **AdminController** (Dashboard & Liga-Management)
- **Route**: `/admin`, `/admin/index`
- **Verantwortung**: Dashboard, Übersicht, Liga-CRUD-Operationen
- **Methoden**:
  - `Index()` - Admin-Dashboard mit Statistiken
  - `AuditLogs()` - Audit-Logs anzeigen
  - `CreateLeague()`, `EditLeague()`, `UpdateLeague()`, `DeleteLeague()`
  - `ArchiveLeague()`, `UnarchiveLeague()`, `ClearLeagueData()`
  - `RebuildLeagueStats()`, `RebuildAllStats()`

***REMOVED******REMOVED******REMOVED******REMOVED*** 2. **AdminApplicationsController** (Bewerbungsverwaltung)
- **Route**: `/admin/applications/*`
- **Verantwortung**: Bewerbungen verwalten
- **Methoden**:
  - `Index()` - Alle Bewerbungen anzeigen
  - `ByDivision(division)` - Bewerbungen nach Division
  - `Accept(id)` - Bewerbung akzeptieren
  - `Delete(id)` - Bewerbung löschen

***REMOVED******REMOVED******REMOVED******REMOVED*** 3. **AdminCommunityController** (Community-Content & Events)
- **Route**: `/admin/community/*`
- **Verantwortung**: Community-Inhalte, Events, Stream-Schedules
- **Methoden**:
  - `Hub()` - Community Hub Dashboard
  - `SaveNewsPost()`, `SaveVotePoll()`, `SaveHighlightClip()` - Content speichern
  - `Events()` - Event-Management
  - `SaveRealLifeEvent()`, `DeleteRealLifeEvent()` - Events verwalten
  - `AddEventImages()`, `DeleteEventImage()` - Event-Bilder verwalten
  - `StreamSchedules()`, `SaveStreamSchedule()`, `DeleteStreamSchedule()`

***REMOVED******REMOVED******REMOVED******REMOVED*** 4. **AdminMediaController** (Dateiverwaltung)
- **Route**: `/admin/media/*`
- **Verantwortung**: Media-Dateien (Musik, etc.)
- **Methoden**:
  - `BackgroundMusic()` - Musikdateien anzeigen
  - `UploadBackgroundMusic()` - Musik hochladen
  - `DeleteBackgroundMusic()` - Musik löschen

***REMOVED******REMOVED******REMOVED******REMOVED*** 5. **AdminLeagueController** (Standings & Events)
- **Route**: `/admin/league/*`
- **Verantwortung**: Fahrer-Platzierungen und anstehende Events
- **Methoden**:
  - `SaveStanding()`, `DeleteStanding()` - Standings verwalten
  - `DriverSuggestions(q)` - Driver Auto-Complete
  - `SaveEvent()`, `DeleteEvent()` - Events verwalten

***REMOVED******REMOVED******REMOVED******REMOVED*** 6. **AdminAchievementsController** (Achievements)
- **Route**: `/admin/achievements/*`
- **Verantwortung**: Custom Achievements und Definitionen
- **Methoden**:
  - `Index()` - Achievements anzeigen
  - `Save()`, `Delete()` - Achievements verwalten
  - `Definitions()` - Definitionen anzeigen
  - `SaveDefinition()`, `DeleteDefinition()` - Definitionen verwalten

***REMOVED******REMOVED*** Dependency Injection (Program.cs)

Neue Service-Registrierungen in `Program.cs`:

```csharp
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<ICommunityContentService, CommunityContentService>();
builder.Services.AddScoped<IMediaService, MediaService>();
```

***REMOVED******REMOVED*** Vorher vs. Nachher

| Aspekt | Vorher | Nachher |
|--------|--------|---------|
| **AdminController-Größe** | 97 KB (~2200 Zeilen) | ~8 KB (~300 Zeilen) |
| **Separation of Concerns** | Monolithisch | Modular, 6 spezialisierte Controller |
| **Services** | In Controller vermischt | Zentral, wiederverwendbar |
| **Testbarkeit** | Schwierig | Einfach (Services unabhängig testbar) |
| **Wartbarkeit** | Schwierig | Hoch (klare Struktur, verantwortliche Fehler) |
| **Erweiterbarkeit** | Limited | Einfach (neue Features in eigenständigen Services) |

***REMOVED******REMOVED*** Views

Die Views wurden **nicht geändert** - sie verweisen auf die neuen Controller:

- `~/Views/Admin/Applications.cshtml` → `AdminApplicationsController.Index()`
- `~/Views/Admin/ApplicationsByDivision.cshtml` → `AdminApplicationsController.ByDivision()`
- `~/Views/Admin/CommunityHub.cshtml` → `AdminCommunityController.Hub()`
- `~/Views/Admin/BackgroundMusic.cshtml` → `AdminMediaController.BackgroundMusic()`
- `~/Views/Admin/Events.cshtml` → `AdminCommunityController.Events()`
- `~/Views/Admin/Achievements.cshtml` → `AdminAchievementsController.Index()`
- `~/Views/Admin/AchievementDefinitions.cshtml` → `AdminAchievementsController.Definitions()`

***REMOVED******REMOVED*** Route-Struktur

```
/admin/                               → AdminController.Index()
/admin/auditlogs                      → AdminController.AuditLogs()
/admin/applications/                  → AdminApplicationsController.Index()
/admin/applications/bydivision        → AdminApplicationsController.ByDivision()
/admin/community/hub                  → AdminCommunityController.Hub()
/admin/community/events               → AdminCommunityController.Events()
/admin/community/schedules            → AdminCommunityController.StreamSchedules()
/admin/media/music                    → AdminMediaController.BackgroundMusic()
/admin/league/standings               → AdminLeagueController.SaveStanding()
/admin/achievements/                  → AdminAchievementsController.Index()
/admin/achievements/definitions       → AdminAchievementsController.Definitions()
```

***REMOVED******REMOVED*** Zukünftige Verbesserungen

1. **Weitere Service-Extraktion**:
   - `RaceManagementService` - Für Rennen-Verwaltung (aktuell noch in AdminController)
   - `TrackSetupService` - Für Track Setup Verwaltung
   - `AchievementManagementService` - Achievements optimieren

2. **REST-API-Endpoints**:
   - Admin-API für externe Tools
   - Mobile Admin-App

3. **Caching**:
   - Statistiken-Cache
   - Application-Count-Cache

4. **Async-Verbesserungen**:
   - Batch-Processing für große Operationen
   - Background Jobs für Heavy-Lifting-Tasks

***REMOVED******REMOVED*** Migration: Alte vs. Neue Routes

Falls Sie alte Admin-Routes haben, müssen diese angepasst werden:

| Alt | Neu | Controller |
|-----|-----|-----------|
| POST `/admin/saveapplication` | POST `/admin/applications/accept` | AdminApplicationsController |
| GET `/admin/applications` | GET `/admin/applications` | AdminApplicationsController |
| POST `/admin/savecommunitypost` | POST `/admin/community/hub` | AdminCommunityController |
| GET `/admin/backgroundmusic` | GET `/admin/media/backgroundmusic` | AdminMediaController |

---

**Erstellt**: 2026-05-05  
**Projekt**: <OWNER_HANDLE>-ERC  
**Status**: ✅ Kompiliert erfolgreich
