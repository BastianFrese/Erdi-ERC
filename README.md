***REMOVED*** <OWNER_HANDLE>-ERC

Plattform für die **<OWNER_HANDLE> Racing Community (ERC)** — eine F1-Liga-Website mit
Discord-OAuth-Login, Renn-Ergebnissen, Liga-Meisterschaften, Stewarding-Berichten
und einem Bewerbungssystem für Fahrer.

> ⚠️ **Privates Repo.** Sämtliche Inhalte, Datenbank-Snapshots und
> `appsettings.Production.json` sind nur für interne Nutzung.

***REMOVED******REMOVED*** Tech-Stack

| Schicht        | Technologie                                                      |
|----------------|------------------------------------------------------------------|
| Backend        | ASP.NET Core 9 / 10 (Razor Views, MVC-Controller)                |
| ORM            | Entity Framework Core (Pomelo MySQL Provider)                    |
| DB (Dev)       | MySQL `erditest` auf `<PROD_HOST>:3306`                       |
| DB (Prod)      | MySQL `erdierc` auf `<PROD_HOST>:3306` — **LIVE, nicht anfassen** |
| Tests          | xUnit + SQLite In-Memory (`SqliteTestContext`)                   |
| Auth           | Discord-OAuth via `AspNet.Security.OAuth.Discord`                |
| Frontend       | Razor + Vanilla JS, F1-Design-System (`f1-chip`, `f1-panel`, …)   |
| CI / Deploy    | GitHub Actions: `ci.yml` (build+test) / `publish.yml` (Tar-Artefakt) |

***REMOVED******REMOVED*** Repo-Struktur

```
<OWNER_HANDLE>-ERC/                ASP.NET-Code (Controller, Views, Models, Migrations, Services)
  ├─ Controllers/        MVC-Controller (Home, Admin*, Application)
  ├─ Views/              Razor-Views (Public + Admin)
  ├─ Models/             EF-Entities + ViewModels
  ├─ Services/           Domain-Services (Application, Stats, Stewarding, …)
  ├─ Migrations/         EF-Core-Migrations (timestamp-prefixed)
  ├─ Data/AppDbContext.cs
  ├─ Program.cs          DI + Startup (db.Migrate() nur in IsDevelopment!)
  └─ appsettings*.json   Connection-Strings + Config
tests/<OWNER_HANDLE>-ERC.Tests/    xUnit-Tests (Service-Tests, Controller-Tests, Guest-Validation)
Docs/                    Obsidian-kompatibler Doku-Tresor (YAML-Frontmatter)
  ├─ Features/           Implementierte Features (applications.md, …)
  ├─ Tasks/              Offene ToDos + Task-Trails
  ├─ Daily/              Tagesnotizen
  └─ MIGRATION-SAFETY.md Pflichtlektüre vor jedem EF-Migration/Deploy
.github/workflows/       ci.yml + publish.yml
CLAUDE.md                Projekt-Regeln für KI-Agenten (Verzeichnis, Coding-Style)
```

***REMOVED******REMOVED*** Voraussetzungen

- .NET SDK 10
- MySQL-Zugriff auf `<PROD_HOST>:3306` (für `erditest` via
  `appsettings.Development.json`)
- Discord-OAuth-App (ClientId + ClientSecret für lokales Login)

***REMOVED******REMOVED*** Lokal bauen und starten

```bash
git clone https://github.com/BastianFrese/<OWNER_HANDLE>-ERC.git
cd <OWNER_HANDLE>-ERC

***REMOVED*** Connection-String + Discord-Secrets lokal setzen
dotnet user-secrets set "ConnectionStrings:Default" "Server=<PROD_HOST>;Port=3306;Database=erditest;User=...;Password=..." --project <OWNER_HANDLE>-ERC/<OWNER_HANDLE>-ERC.csproj
dotnet user-secrets set "Discord:ClientId" "..." --project <OWNER_HANDLE>-ERC/<OWNER_HANDLE>-ERC.csproj
dotnet user-secrets set "Discord:ClientSecret" "..." --project <OWNER_HANDLE>-ERC/<OWNER_HANDLE>-ERC.csproj

***REMOVED*** Bauen + Testen
dotnet build <OWNER_HANDLE>-ERC/<OWNER_HANDLE>-ERC.csproj
dotnet test  tests/<OWNER_HANDLE>-ERC.Tests/<OWNER_HANDLE>-ERC.Tests.csproj

***REMOVED*** Starten (Profile http → Port 5005, https → 7030+5005)
dotnet run --project <OWNER_HANDLE>-ERC/<OWNER_HANDLE>-ERC.csproj --launch-profile http
```

`launchSettings.json` setzt `ASPNETCORE_ENVIRONMENT=Development` für beide
Profile → jedes `dotnet ef …` Kommando aus VS/Rider heraus zeigt
**automatisch** auf `erditest`, nie auf `erdierc`.

***REMOVED******REMOVED*** Datenbank & EF-Migrationen

***REMOVED******REMOVED******REMOVED*** Die zwei Datenbanken

| Umgebung | DB             | Connection-File                      | Status              |
|----------|----------------|--------------------------------------|---------------------|
| Dev      | **`erditest`** | `appsettings.Development.json`       | anfassen ✅          |
| Prod     | **`erdierc`**  | `appsettings.Production.json` (Repo) | **LIVE** — niemals automatisch mutieren |

***REMOVED******REMOVED******REMOVED*** Migrations-Workflow

```bash
***REMOVED*** 1. EF-Migration offline generieren
dotnet ef migrations add <Name> --project <OWNER_HANDLE>-ERC/<OWNER_HANDLE>-ERC.csproj --startup-project <OWNER_HANDLE>-ERC/<OWNER_HANDLE>-ERC.csproj

***REMOVED*** 2. Lokal auf erditest anwenden (automatisch, weil ASPNETCORE_ENVIRONMENT=Development)
dotnet ef database update --project <OWNER_HANDLE>-ERC/<OWNER_HANDLE>-ERC.csproj --startup-project <OWNER_HANDLE>-ERC/<OWNER_HANDLE>-ERC.csproj

***REMOVED*** 3. Prod NUR über den Deploy-Workflow (publish.yml + manueller Server-Owner)
***REMOVED***    NIEMALS lokal: dotnet ef database update mit env=Production
```

> ⚠️ **Pflichtlektüre vor jeder EF-Migration:** `Docs/MIGRATION-SAFETY.md` —
> listet alle Sicherheitsnetze, Risiko-Trigger und Eskalations-Pfade.
> Prod-Migrationen dürfen niemals per App-Startup (`db.Database.Migrate()`)
> laufen — `Program.cs` ruft das **nur** in `IsDevelopment()` auf.

***REMOVED******REMOVED******REMOVED*** MySQL-spezifische Stolperfallen

- **FK-Index-Drop-Pitfall**: Wenn ein Index gedroppt wird, der eine
  FK-Constraint versorgt, schlägt MySQL fehl. Pattern: neuen Index mit
  FK-Spalte als Präfix **zuerst** erstellen, Drops in Stored Procedure mit
  `information_schema.STATISTICS`-Check (idempotent) wrappen.
  → siehe `<OWNER_HANDLE>-ERC/Migrations/20260815143225_AddApplicationSeason.cs`
- **`SET FOREIGN_KEY_CHECKS = 0`** versteckt das Symptom und ist **kein**
  guter Fix.

***REMOVED******REMOVED*** Tests

```bash
dotnet test tests/<OWNER_HANDLE>-ERC.Tests/<OWNER_HANDLE>-ERC.Tests.csproj
```

- xUnit + EF-In-Memory-SQLite (`SqliteTestContext`) für realistische
  Transaktionen ohne MySQL-Server.
- Service-Tests, Controller-Integrationstests, End-to-End-Validation-Flows.
- Mindestabdeckung 80% (Projekt-Konvention).

***REMOVED******REMOVED*** Features (Auszug)

| Bereich        | Features                                                                          |
|----------------|-----------------------------------------------------------------------------------|
| Auth           | Discord-OAuth, Bootstrap-Admins, Session-Cookies, Troll-Gags (Login-Pranks)       |
| Rennen         | Renn-Ergebnisse mit Finishes, Qualifying, Reserve-Fahrer, Gastfahrer-Zuordnung     |
| Ligen          | Multi-Liga-Meisterschaft, Capacity + Warteliste, Saison-Switch, Penalty-Reports    |
| Stewarding     | Stewardsche Berichte (`/Stewarding`, Admin), Penalty-Typen, kein Wertungsabzug    |
| Bewerbungen    | Liga-Opt-in, Waitlist, NextSeason-Awareness, Roll-over per Admin-Aktion           |
| Community      | News, Votings, Highlight-Clips, Real-Life-Events, Stream-Schedules                |
| Fahrer         | Fahrerkarte (PNG-Export), Lieblings-Team, Hardware-Logos, Nummer + Farbe          |
| Stats          | Ewige-Liste (Excel-Legacy + Live-DB), Overall-Constructors-Meisterschaft           |

Detail-Doku pro Feature in `Docs/Features/*.md`.

***REMOVED******REMOVED*** Deployment

- Tag-Push (`v*.*.*`) triggert `.github/workflows/publish.yml`.
- Artefakt: `linux-x64`, `self-contained=false`, als `tar.gz` gepackt.
- Server-Owner entpackt + `systemctl restart erdi-erc`.
- **Kein** automatischer `dotnet ef database update` — Prod-Schema-Migrationen
  laufen **manuell** durch den Server-Owner nach `Docs/MIGRATION-SAFETY.md`.

***REMOVED******REMOVED*** Sicherheits-Checkliste

- [ ] Keine Secrets in `appsettings.json` (leerer Connection-String)
- [ ] `appsettings.Development.json` lokal, **nicht** eingecheckt
- [ ] `appsettings.Production.json` (im Repo) nur in privatem Repo
- [ ] `db.Database.Migrate()` niemals unconditional in `Program.cs`
- [ ] Vor jeder EF-Migration: `Docs/MIGRATION-SAFETY.md` lesen
- [ ] Prod-Migrationen nur manuell durch Server-Owner

***REMOVED******REMOVED*** Mitwirkende

- **Bastian Frese** (`<OWNER_HANDLE>`, `<OWNER_HANDLE>`) — Lead, Bootstrap-Admin
- Beiträge über Issues / PRs willkommen (private Repo)

***REMOVED******REMOVED*** Lizenz

Privat — alle Rechte vorbehalten.