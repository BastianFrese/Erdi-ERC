***REMOVED*** Admin-Verwaltungsbereich - Quick Start Guide

***REMOVED******REMOVED*** 🎯 Schnelle Übersicht

Der Verwaltungsbereich wurde von einem monolithischen `AdminController` in eine **modulare, wartbare Architektur** aufgeteilt.

***REMOVED******REMOVED******REMOVED*** Hauptkomponenten

1. **AdminController** - Dashboard & Liga-Verwaltung
2. **AdminApplicationsController** - Bewerbungen
3. **AdminCommunityController** - Community-Content & Events
4. **AdminMediaController** - Datei-Management
5. **AdminLeagueController** - Standings & Events
6. **AdminAchievementsController** - Achievements

---

***REMOVED******REMOVED*** 📂 Typische Aufgaben

***REMOVED******REMOVED******REMOVED*** Task 1: Neue Bewerbungs-Logik hinzufügen

**Änderungsort**: `Services/ApplicationService.cs`

```csharp
// Services/IApplicationService.cs - Interface
public interface IApplicationService
{
    Task<bool> AcceptApplicationAsync(int id, string? actorId);
    Task<bool> MyNewMethod();  // Füge diese neue Methode hinzu
}

// Services/ApplicationService.cs - Implementierung
public class ApplicationService : IApplicationService
{
    public async Task<bool> MyNewMethod()
    {
        // Deine Logik hier
    }
}

// Controllers/AdminApplicationsController.cs - Nutze den Service
[HttpPost]
public async Task<IActionResult> MyNewAction()
{
    var result = await _applicationService.MyNewMethod();
    // ...
}
```

***REMOVED******REMOVED******REMOVED*** Task 2: Neue Community-Feature hinzufügen

**Änderungsort**: `Services/CommunityContentService.cs` → `Controllers/AdminCommunityController.cs`

```csharp
// Services/ICommunityContentService.cs
public interface ICommunityContentService
{
    Task<MyNewFeature?> SaveMyFeatureAsync(...);
}

// Services/CommunityContentService.cs
public async Task<MyNewFeature?> SaveMyFeatureAsync(...)
{
    var entity = new MyNewFeature { ... };
    _db.MyNewFeatures.Add(entity);
    await _db.SaveChangesAsync();
    await _audit.LogAsync("SaveMyFeature", "MyNewFeature", entity.Id.ToString(), ...);
    await _discordWebhook.SendAsync(...);
    return entity;
}

// Controllers/AdminCommunityController.cs
[HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> SaveMyFeature(...)
{
    var result = await _contentService.SaveMyFeatureAsync(...);
    if (result is null)
    {
        TempData["AdminMessage"] = "Fehler...";
        return RedirectToAction(nameof(Hub));
    }
    TempData["AdminMessage"] = "Erfolg!";
    return RedirectToAction(nameof(Hub));
}
```

***REMOVED******REMOVED******REMOVED*** Task 3: Neue Admin-View erstellen

**Änderungsort**: `Views/Admin/MyNewView.cshtml` + Controller

```csharp
// Controllers/AdminCommunityController.cs (Beispiel)
[HttpGet]
public async Task<IActionResult> MyNewView()
{
    var data = await _db.MyEntities.ToListAsync();
    return View("~/Views/Admin/MyNewView.cshtml", data);
}
```

```html
<!-- Views/Admin/MyNewView.cshtml -->
@model List<MyEntity>

<h2>Meine neue Seite</h2>

<form method="post" action="@Url.Action("SaveMyEntity", "AdminCommunity")">
    @Html.AntiForgeryToken()
    <input type="text" name="title" required />
    <button type="submit">Speichern</button>
</form>
```

---

***REMOVED******REMOVED*** 🔄 Routing-Referenzen

***REMOVED******REMOVED******REMOVED*** Wichtige Routes

```csharp
// Dashboard
GET /admin                                  → AdminController.Index()

// Bewerbungen
GET  /admin/applications                    → AdminApplicationsController.Index()
GET  /admin/applications/bydivision/:div    → AdminApplicationsController.ByDivision()
POST /admin/applications/accept/:id         → AdminApplicationsController.Accept()
POST /admin/applications/delete/:id         → AdminApplicationsController.Delete()

// Community
GET  /admin/community/hub                   → AdminCommunityController.Hub()
POST /admin/community/hub                   → AdminCommunityController.SaveNewsPost()
GET  /admin/community/events                → AdminCommunityController.Events()
GET  /admin/community/schedules             → AdminCommunityController.StreamSchedules()

// Media
GET  /admin/media/backgroundmusic           → AdminMediaController.BackgroundMusic()
POST /admin/media/backgroundmusic/upload    → AdminMediaController.UploadBackgroundMusic()
POST /admin/media/backgroundmusic/delete    → AdminMediaController.DeleteBackgroundMusic()

// Ligas
GET  /admin/editLeague/:id                  → AdminController.EditLeague()
POST /admin/CreateLeague                    → AdminController.CreateLeague()

// Achievements
GET  /admin/achievements                    → AdminAchievementsController.Index()
POST /admin/achievements/save               → AdminAchievementsController.Save()
GET  /admin/achievements/definitions        → AdminAchievementsController.Definitions()
```

---

***REMOVED******REMOVED*** 🧪 Testing Services

***REMOVED******REMOVED******REMOVED*** Unit-Test Beispiel

```csharp
[TestFixture]
public class ApplicationServiceTests
{
    private ApplicationService _service;
    private Mock<AppDbContext> _dbMock;
    private Mock<IAdminAuditService> _auditMock;
    private Mock<IDriverProfileService> _profilesMock;

    [SetUp]
    public void Setup()
    {
        _dbMock = new Mock<AppDbContext>();
        _auditMock = new Mock<IAdminAuditService>();
        _profilesMock = new Mock<IDriverProfileService>();

        _service = new ApplicationService(_dbMock.Object, _profilesMock.Object, _auditMock.Object);
    }

    [Test]
    public async Task AcceptApplicationAsync_ShouldMarkAsAccepted()
    {
        // Arrange
        var app = new ApplicationForm { Id = 1, IsAccepted = false };
        _dbMock.Setup(db => db.ApplicationForms.FindAsync(1))
            .ReturnsAsync(app);

        // Act
        var result = await _service.AcceptApplicationAsync(1, "userId123");

        // Assert
        Assert.IsTrue(result);
        Assert.IsTrue(app.IsAccepted);
        Assert.IsNotNull(app.AcceptedAt);
    }
}
```

---

***REMOVED******REMOVED*** 📋 Checkliste für neue Features

Wenn du ein neues Admin-Feature hinzufügst, folge dieser Checkliste:

- [ ] **Service Interface** - Definiere `IMyNewService.cs`
- [ ] **Service Implementierung** - Implementiere `MyNewService.cs`
- [ ] **Controller** - Erstelle oder ergänze `AdminMyFeatureController.cs`
- [ ] **View** - Erstelle `Views/Admin/MyFeature.cshtml`
- [ ] **Dependency Injection** - Registriere Service in `Program.cs`
- [ ] **Audit Logging** - Rufe `_audit.LogAsync()` auf
- [ ] **Fehlerbehandlung** - Verwende `TempData["AdminMessage"]`
- [ ] **Tests** - Schreibe Unit-Tests für Service-Methoden
- [ ] **Dokumentation** - Update `ADMIN_REFACTORING_DOCUMENTATION.md`

---

***REMOVED******REMOVED*** 🛠️ Häufig verwendete Patterns

***REMOVED******REMOVED******REMOVED*** Pattern 1: CRUD-Operation mit Audit

```csharp
[HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> SaveEntity(string id, string name)
{
    // 1. Validierung
    if (string.IsNullOrWhiteSpace(name))
    {
        TempData["AdminMessage"] = "Name ist erforderlich.";
        return RedirectToAction(nameof(SomeView));
    }

    // 2. Datenbankoperation
    var entity = await _db.MyEntities.FindAsync(id) ?? new MyEntity();
    entity.Name = name.Trim();

    if (entity.Id == 0) _db.MyEntities.Add(entity);
    await _db.SaveChangesAsync();

    // 3. Audit Logging
    await _audit.LogAsync("SaveEntity", "MyEntity", id, $"Name={entity.Name}");

    // 4. Discord Webhook (optional)
    await _discordWebhook.SendAsync("📝 Entity gespeichert", new[] { entity.Name });

    // 5. User Feedback
    TempData["AdminMessage"] = "Entity gespeichert.";
    return RedirectToAction(nameof(SomeView));
}
```

***REMOVED******REMOVED******REMOVED*** Pattern 2: Service mit Dependency Injection

```csharp
public interface IMyService
{
    Task<List<Entity>> GetAllAsync();
    Task<bool> SaveAsync(Entity entity);
}

public class MyService : IMyService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _audit;

    public MyService(AppDbContext db, IAdminAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<Entity>> GetAllAsync()
    {
        return await _db.MyEntities.OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<bool> SaveAsync(Entity entity)
    {
        if (entity == null) return false;

        _db.MyEntities.Add(entity);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("SaveEntity", "Entity", entity.Id.ToString(), "...");
        return true;
    }
}

// Registrierung in Program.cs:
builder.Services.AddScoped<IMyService, MyService>();
```

---

***REMOVED******REMOVED*** 🐛 Debugging-Tipps

***REMOVED******REMOVED******REMOVED*** Debug View Context
```csharp
// In Controller-Methode
ViewBag.DebugData = new { 
    UserId = User.Identity?.Name,
    Roles = User.Claims.Where(c => c.Type == "role").Select(c => c.Value),
    Timestamp = DateTime.UtcNow
};
```

***REMOVED******REMOVED******REMOVED*** Audit-Logs prüfen
```csharp
// In AdminController.AuditLogs()
var recentLogs = await _db.AdminAuditLogs
    .Where(x => x.CreatedAt >= DateTime.UtcNow.AddHours(-24))
    .OrderByDescending(x => x.CreatedAt)
    .ToListAsync();
```

---

***REMOVED******REMOVED*** 🚀 Performance-Tipps

1. **Eager Loading verwenden**:
```csharp
// Nicht: Lazy Loading ❌
var league = _db.Leagues.FirstOrDefault(x => x.Id == id);
var standings = league.Standings; // Extra Query!

// Besser: Eager Loading ✅
var league = _db.Leagues
    .Include(l => l.Standings)
    .FirstOrDefault(x => x.Id == id);
```

2. **Select() für große Abfragen**:
```csharp
// Nicht: Komplette Entities laden ❌
var all = _db.RaceResults.ToList();

// Besser: Nur nötige Felder ✅
var simple = _db.RaceResults
    .Select(r => new { r.Id, r.Track, r.Date })
    .ToList();
```

3. **Pagination bei großen Listen**:
```csharp
const int pageSize = 50;
var page = 1;
var items = _db.AdminAuditLogs
    .OrderByDescending(x => x.CreatedAt)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

---

***REMOVED******REMOVED*** 📚 Weiterführende Ressourcen

- [ADMIN_REFACTORING_DOCUMENTATION.md](ADMIN_REFACTORING_DOCUMENTATION.md) - Detaillierte Dokumentation
- [ADMIN_ARCHITECTURE_OVERVIEW.md](ADMIN_ARCHITECTURE_OVERVIEW.md) - Architektur-Übersicht
- [Service Layer Pattern](https://www.c-sharpcorner.com/article/service-layer-design-pattern/) - Externe Ressource

---

**Zuletzt aktualisiert**: 2026-05-05  
**Status**: ✅ Production-ready  
**Version**: 1.0.0
