***REMOVED*** Admin Area - Neue Struktur & Dokumentation

***REMOVED******REMOVED*** 📋 Übersicht

Die Admin-Area wurde von einem **monolithischen AdminController** (395 Zeilen) in eine **modulare Architektur** mit **spezialisierten Feature-Controllern** aufgeteilt.

---

***REMOVED******REMOVED*** 🏛️ Neue Controller-Struktur

***REMOVED******REMOVED******REMOVED*** 1. **AdminController** (`/admin`)
**Zweck:** Zentrales Dashboard + Liga-Management

- **Index (Dashboard):** Übersicht über alle Ligas, Statistiken, Audit-Logs
- **CreateLeague, EditLeague, UpdateLeague, DeleteLeague:** Liga CRUD
- **ArchiveLeague, UnarchiveLeague:** Liga-Archivierung
- **ClearLeagueData:** Saisonale Daten löschen
- **RebuildLeagueStats, RebuildAllStats:** Statistik-Neu-Berechnung
- **AuditLogs:** System-Audit-Übersicht

**Route:** `/admin/`

---

***REMOVED******REMOVED******REMOVED*** 2. **AdminApplicationsController** (`/admin/applications`)
**Zweck:** Strukturierter Bewerbungs-Workflow

***REMOVED******REMOVED******REMOVED******REMOVED*** Dashboard & Übersicht
- **Dashboard:** KPI-Übersicht (offene, akzeptierte, pro Division)
- **List:** Alle Bewerbungen mit Paginierung
- **Open:** Nur offene Bewerbungen
- **Accepted:** Nur akzeptierte Bewerbungen
- **ByDivision/{division}:** Nach Division filtern

***REMOVED******REMOVED******REMOVED******REMOVED*** Detail & Workflow
- **Detail:** Einzelne Bewerbung mit Audit-Trail
- **Accept:** Bewerbung akzeptieren → Profil erstellen
- **Reject:** Bewerbung ablehnen (mit Grund)
- **Unaccept:** Akzeptanz rückgängig machen
- **Delete:** Bewerbung löschen
- **Flag:** Zur Überprüfung kennzeichnen

***REMOVED******REMOVED******REMOVED******REMOVED*** Batch & Automatisierung
- **AcceptMultiple:** Mehrere gleichzeitig akzeptieren
- **CleanupExpired:** 48h abgelaufene Bewerbungen entfernen

***REMOVED******REMOVED******REMOVED******REMOVED*** Reporting
- **Export:** CSV-Export
- **Metrics:** Bearbeitungsstatistiken (Annahmequote, Durchsatzzeit, etc.)

**Route:** `/admin/applications/`

---

***REMOVED******REMOVED******REMOVED*** 3. **AdminRacesController** (`/admin/races`)
**Zweck:** Renneingabe, Ergebnisse, Undo-System

***REMOVED******REMOVED******REMOVED******REMOVED*** Übersicht
- **ByLeague/{leagueId}:** Alle Rennen einer Liga
- **Detail:** Einzelner Race mit Ergebnissen

***REMOVED******REMOVED******REMOVED******REMOVED*** Renneingabe
- **Enter:** Formular für Renneingabe
- **Save:** Ergebnisse speichern (mit Undo-Backup)

***REMOVED******REMOVED******REMOVED******REMOVED*** Undo-System
- **Undo:** Letzte Änderung rückgängig machen
- *Automatisches Backup bei jedem Save*

***REMOVED******REMOVED******REMOVED******REMOVED*** Reservist-Verwaltung
- **AssignReserve:** Reservist für Fahrer zuordnen

***REMOVED******REMOVED******REMOVED******REMOVED*** Verwaltung
- **Delete:** Rennen löschen (mit Stat-Neuberechnung)

**Route:** `/admin/races/`

---

***REMOVED******REMOVED******REMOVED*** 4. **AdminTrackSetupsController** (`/admin/track-setups`)
**Zweck:** Track-Konfiguration & Fahrhilfen-Management

***REMOVED******REMOVED******REMOVED******REMOVED*** CRUD
- **Index:** Alle Track-Setups
- **Detail:** Setup mit Fahrhilfen
- **Create:** Neues Setup
- **Update:** Setup bearbeiten
- **Delete:** Setup löschen

***REMOVED******REMOVED******REMOVED******REMOVED*** Fahrhilfen
- **UpdateAssists:** Fahrhilfen speichern (ABS, Traction Control, etc.)

***REMOVED******REMOVED******REMOVED******REMOVED*** Tuning
- **UpdateTuning:** Aero/Suspension/Brakes-Parameter

***REMOVED******REMOVED******REMOVED******REMOVED*** Export
- **Export:** JSON-Export aller Setups

**Route:** `/admin/track-setups/`

---

***REMOVED******REMOVED******REMOVED*** 5. **AdminUsersController** (`/admin/users`)
**Zweck:** Admin & Moderator-Verwaltung + Sicherheit

***REMOVED******REMOVED******REMOVED******REMOVED*** Verwaltung
- **Index:** Liste aller Admins & Moderatoren
- **Detail:** Benutzer-Detail mit Audit-Trail

***REMOVED******REMOVED******REMOVED******REMOVED*** Rollen
- **MakeAdmin, RemoveAdmin:** Admin-Rolle
- **MakeModerator, RemoveModerator:** Moderator-Rolle

***REMOVED******REMOVED******REMOVED******REMOVED*** Sicherheit
- **Lock/Unlock:** Benutzer sperren/entsperren
- **ResetPassword:** Passwort zurücksetzen

***REMOVED******REMOVED******REMOVED******REMOVED*** Reporting
- **ActivityLog:** Admin-Aktivitätsprotokoll (30 Tage)

**Route:** `/admin/users/`

---

***REMOVED******REMOVED******REMOVED*** 6. **AdminCommunityController** (`/admin/community`)
**Zweck:** News, Polls, Highlights, Events, Streams

- **Hub:** Community-Hub verwenden
- **Events:** Real-Life Events
- **StreamSchedules:** Stream-Zeitplan
- *Siehe separate Dokumentation*

**Route:** `/admin/community/`

---

***REMOVED******REMOVED******REMOVED*** 7. **AdminMediaController** (`/admin/media`)
**Zweck:** Musik- & Datei-Verwaltung

- **BackgroundMusic:** Musik-Upload/Verwaltung
- *Siehe separate Dokumentation*

**Route:** `/admin/media/`

---

***REMOVED******REMOVED******REMOVED*** 8. **AdminLeagueController** (`/admin/league`)
**Zweck:** Liga-Inhalte (Standings, Events)

- **SaveStanding/DeleteStanding:** Fahrer-Positionen
- **SaveEvent/DeleteEvent:** Liga-Events
- *Siehe separate Dokumentation*

**Route:** `/admin/league/`

---

***REMOVED******REMOVED******REMOVED*** 9. **AdminAchievementsController** (`/admin/achievements`)
**Zweck:** Achievements & Definitionen

- **Index/Save/Delete:** Custom Achievements
- **Definitions:** Achievement-Typen definieren
- *Siehe separate Dokumentation*

**Route:** `/admin/achievements/`

---

***REMOVED******REMOVED*** 🔧 Services-Architektur

***REMOVED******REMOVED******REMOVED*** **IApplicationManagementService**
Umfassender Service für Bewerbungs-Workflow:
- Status-Tracking (Statistiken, History)
- Workflow (Accept, Reject, Unaccept, Delete, Flag)
- Automatisierung (ExpiredCleanup, Batch)
- Reporting (CSV-Export, Metriken)

**Implementierung:** `ApplicationManagementService`

```csharp
// Beispiel-Nutzung:
var stats = await _appService.GetStatisticsAsync();
var result = await _appService.AcceptApplicationAsync(id, userId);
var csv = await _appService.ExportAsCSVAsync();
```

***REMOVED******REMOVED******REMOVED*** **Weitere Services:**
- `IStatsService` - Liga-Statistik-Berechnung
- `IAdminAuditService` - Audit-Logging
- `IDriverProfileService` - Fahrer-Profile
- `IMediaService` - Datei-Verwaltung
- `IDiscordWebhookService` - Discord-Notifikationen

---

***REMOVED******REMOVED*** 📊 Datenfluss: Bewerbung → Fahrer

```
ApplicationForm (eingegangen)
    ↓
AdminApplicationsController.Accept()
    ↓
IApplicationManagementService.AcceptApplicationAsync()
    ↓
[1] DriverProfile erstellen (via IDriverProfileService)
[2] Audit-Log (via IAdminAuditService)
[3] Discord-Notifikation (via IDiscordWebhookService)
[4] 48h Expiry-Timer setzen
    ↓
✅ Fahrer hinzugefügt, Bewerbung markiert
```

---

***REMOVED******REMOVED*** 🔐 Autorisierung

Alle Admin-Controller erfordern:
```csharp
[Authorize(Roles = "Admin")]
```

Rollen-Hierachie:
- **Admin:** Vollständiger Zugriff auf alle Admin-Funktionen
- **Moderator:** Begrenzte Verwaltung (Community, Media)

---

***REMOVED******REMOVED*** 📝 Routing-Übersicht

| Controller | Route | Zweck |
|-----------|-------|-------|
| **AdminController** | `/admin/` | Dashboard, Ligas |
| **AdminApplicationsController** | `/admin/applications/` | Bewerbungs-Workflow |
| **AdminRacesController** | `/admin/races/` | Renneingabe, Undo |
| **AdminTrackSetupsController** | `/admin/track-setups/` | Track-Setups |
| **AdminUsersController** | `/admin/users/` | User-Verwaltung |
| **AdminCommunityController** | `/admin/community/` | Community-Inhalte |
| **AdminMediaController** | `/admin/media/` | Musik & Dateien |
| **AdminLeagueController** | `/admin/league/` | Liga-Details |
| **AdminAchievementsController** | `/admin/achievements/` | Achievements |

---

***REMOVED******REMOVED*** 🎯 Neue Features der Bewerbungs-Verwaltung

***REMOVED******REMOVED******REMOVED*** 1. **Strukturierte Dashboard**
```
📊 KPI-Cards:
  - Offene Bewerbungen
  - Akzeptierte Bewerbungen
  - Pro Division
  - Letzte Bewerbung vor X Minuten
```

***REMOVED******REMOVED******REMOVED*** 2. **Intelligente Filter**
```
/admin/applications/open
/admin/applications/accepted
/admin/applications/by-division/Main Division 1
```

***REMOVED******REMOVED******REMOVED*** 3. **Umfassender Audit-Trail**
```
- Wer hat wann was getan
- Gründe für Ablehnung/Unaccept
- Zeitleiste pro Bewerbung
```

***REMOVED******REMOVED******REMOVED*** 4. **Batch-Verarbeitung**
```csharp
// Mehrere Bewerbungen gleichzeitig akzeptieren
POST /admin/applications/accept-multiple
{
    "applicationIds": [1, 2, 3, 4, 5]
}
```

***REMOVED******REMOVED******REMOVED*** 5. **Automatische Cleanup**
```csharp
// 48h nach Akzeptanz automatisch löschen
POST /admin/applications/cleanup-expired
```

***REMOVED******REMOVED******REMOVED*** 6. **Export & Reporting**
```
CSV-Export mit allen Details
Metriken: Annahmequote, Bearbeitungszeit, Durchsatz
```

---

***REMOVED******REMOVED*** 🛠️ Dependency Injection

```csharp
// Program.cs
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IApplicationManagementService, ApplicationManagementService>();
builder.Services.AddScoped<ICommunityContentService, CommunityContentService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IAdminAuditService, AdminAuditService>();
builder.Services.AddScoped<IDriverProfileService, DriverProfileService>();
builder.Services.AddScoped<IDiscordWebhookService, DiscordWebhookService>();
```

---

***REMOVED******REMOVED*** 🔄 Migration vom alten System

***REMOVED******REMOVED******REMOVED*** Alt (Monolith):
```csharp
AdminController.Applications() // 50+ Zeilen
AdminController.Applications_AcceptMultiple() // Vermischt
AdminController.Applications_Delete() // Unklar
```

***REMOVED******REMOVED******REMOVED*** Neu (Modular):
```csharp
AdminApplicationsController.List()
AdminApplicationsController.AcceptMultiple()
AdminApplicationsController.Delete()
// + Dashboard + Metrics + Export + Filter
```

---

***REMOVED******REMOVED*** 📚 Nächste Schritte

1. **Views aktualisieren:** `/Views/Admin/Applications/` → Dashboard.cshtml, List.cshtml, Detail.cshtml, etc.
2. **Admin-Menü:** Navigation zu neuen Controllern hinzufügen
3. **Testing:** Unit-Tests für ApplicationManagementService
4. **Dokumentation:** Benutzerhandbuch für Admins

---

***REMOVED******REMOVED*** 💡 Best Practices

***REMOVED******REMOVED******REMOVED*** ✅ Machen:
- Immer `await _audit.LogAsync()` nach kritischen Operationen
- Discord-Notifikationen für wichtige Events
- Pagination für lange Listen
- Batch-Operationen für Performance

***REMOVED******REMOVED******REMOVED*** ❌ Nicht machen:
- Direkt `_db.SaveChanges()` ohne Audit
- N+1 Queries → `.Include()` nutzen
- HardcodedStrings → `string.IsNullOrWhiteSpace()` checken

---

***REMOVED******REMOVED*** 📞 Support

Bei Fragen zur neuen Admin-Struktur:
1. Dieses Dokument lesen
2. Source-Code-Kommentare checken
3. Service-Interfaces (`IApplicationManagementService`, etc.) studieren
