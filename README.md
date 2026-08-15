# Erdi-ERC

Plattform für die **Erdi Racing Community (ERC)** — eine F1-Liga-Website mit
Discord-OAuth-Login, Renn-Ergebnissen, Liga-Meisterschaften, Stewarding-Berichten
und einem Bewerbungssystem für Fahrer.

> ⚠️ **Privates Repo.** Sämtliche Inhalte, Datenbank-Snapshots und
> `appsettings.Production.json` sind nur für interne Nutzung.

## Tech-Stack

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

## Repo-Struktur

```
Erdi-ERC/                ASP.NET-Code (Controller, Views, Models, Migrations, Services)
  ├─ Controllers/        MVC-Controller (Home, Admin*, Application)
  ├─ Views/              Razor-Views (Public + Admin)
  ├─ Models/             EF-Entities + ViewModels
  ├─ Services/           Domain-Services (Application, Stats, Stewarding, …)
  ├─ Migrations/         EF-Core-Migrations (timestamp-prefixed)
  ├─ Data/AppDbContext.cs
  ├─ Program.cs          DI + Startup (db.Migrate() nur in IsDevelopment!)
  └─ appsettings*.json   Connection-Strings + Config
tests/Erdi-ERC.Tests/    xUnit-Tests (Service-Tests, Controller-Tests, Guest-Validation)
Docs/                    Obsidian-kompatibler Doku-Tresor (YAML-Frontmatter)
  ├─ Features/           Implementierte Features (applications.md, …)
  ├─ Tasks/              Offene ToDos + Task-Trails
  ├─ Daily/              Tagesnotizen
  └─ MIGRATION-SAFETY.md Pflichtlektüre vor jedem EF-Migration/Deploy
.github/workflows/       ci.yml + publish.yml
CLAUDE.md                Projekt-Regeln für KI-Agenten (Verzeichnis, Coding-Style)
```

## Voraussetzungen

- .NET SDK 10
- MySQL-Zugriff auf `<PROD_HOST>:3306` (für `erditest` via
  `appsettings.Development.json`)
- Discord-OAuth-App (ClientId + ClientSecret für lokales Login)

## Lokal bauen und starten

```bash
git clone https://github.com/BastianFrese/Erdi-ERC.git
cd Erdi-ERC

# Connection-String + Discord-Secrets lokal setzen
dotnet user-secrets set "ConnectionStrings:Default" "Server=<PROD_HOST>;Port=3306;Database=erditest;User=...;Password=..." --project Erdi-ERC/Erdi-ERC.csproj
dotnet user-secrets set "Discord:ClientId" "..." --project Erdi-ERC/Erdi-ERC.csproj
dotnet user-secrets set "Discord:ClientSecret" "..." --project Erdi-ERC/Erdi-ERC.csproj

# Bauen + Testen
dotnet build Erdi-ERC/Erdi-ERC.csproj
dotnet test  tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj

# Starten (Profile http → Port 5005, https → 7030+5005)
dotnet run --project Erdi-ERC/Erdi-ERC.csproj --launch-profile http
```

`launchSettings.json` setzt `ASPNETCORE_ENVIRONMENT=Development` für beide
Profile → jedes `dotnet ef …` Kommando aus VS/Rider heraus zeigt
**automatisch** auf `erditest`, nie auf `erdierc`.

> ⚠️ **Wenn die `ASPNETCORE_ENVIRONMENT`-Variable leer ist, defaultet ASP.NET
> Core auf `Production`.** Dann werden weder `appsettings.Development.json`
> noch die Dev-User-Secrets korrekt zusammengeführt, und Discord-OAuth gibt
> `invalid_client` zurück. Der Pre-Build-Hook `WarnIfUserSecretsEmpty`
> warnt in dem Fall mit `SEC002`. Fix: immer mit
> `dotnet run --project Erdi-ERC --launch-profile http` starten oder die
> env-Var explizit setzen.

## Datenbank & EF-Migrationen

### Die zwei Datenbanken

| Umgebung | DB             | Connection-File                      | Status              |
|----------|----------------|--------------------------------------|---------------------|
| Dev      | **`erditest`** | `appsettings.Development.json`       | anfassen ✅          |
| Prod     | **`erdierc`**  | `appsettings.Production.json` (Repo) | **LIVE** — niemals automatisch mutieren |

### Migrations-Workflow

```bash
# 1. EF-Migration offline generieren
dotnet ef migrations add <Name> --project Erdi-ERC/Erdi-ERC.csproj --startup-project Erdi-ERC/Erdi-ERC.csproj

# 2. Lokal auf erditest anwenden (automatisch, weil ASPNETCORE_ENVIRONMENT=Development)
dotnet ef database update --project Erdi-ERC/Erdi-ERC.csproj --startup-project Erdi-ERC/Erdi-ERC.csproj

# 3. Prod NUR über den Deploy-Workflow (publish.yml + manueller Server-Owner)
#    NIEMALS lokal: dotnet ef database update mit env=Production
```

> ⚠️ **Pflichtlektüre vor jeder EF-Migration:** `Docs/MIGRATION-SAFETY.md` —
> listet alle Sicherheitsnetze, Risiko-Trigger und Eskalations-Pfade.
> Prod-Migrationen dürfen niemals per App-Startup (`db.Database.Migrate()`)
> laufen — `Program.cs` ruft das **nur** in `IsDevelopment()` auf.

### MySQL-spezifische Stolperfallen

- **FK-Index-Drop-Pitfall**: Wenn ein Index gedroppt wird, der eine
  FK-Constraint versorgt, schlägt MySQL fehl. Pattern: neuen Index mit
  FK-Spalte als Präfix **zuerst** erstellen, Drops in Stored Procedure mit
  `information_schema.STATISTICS`-Check (idempotent) wrappen.
  → siehe `Erdi-ERC/Migrations/20260815143225_AddApplicationSeason.cs`
- **`SET FOREIGN_KEY_CHECKS = 0`** versteckt das Symptom und ist **kein**
  guter Fix.

## Tests

```bash
dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj
```

- xUnit + EF-In-Memory-SQLite (`SqliteTestContext`) für realistische
  Transaktionen ohne MySQL-Server.
- Service-Tests, Controller-Integrationstests, End-to-End-Validation-Flows.
- Mindestabdeckung 80% (Projekt-Konvention).

## Features (Auszug)

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

## Deployment

- Tag-Push (`v*.*.*`) triggert `.github/workflows/publish.yml`.
- Artefakt: `linux-x64`, `self-contained=false`, als `tar.gz` gepackt.
- Server-Owner entpackt + `systemctl restart erdi-erc`.
- **Kein** automatischer `dotnet ef database update` — Prod-Schema-Migrationen
  laufen **manuell** durch den Server-Owner nach `Docs/MIGRATION-SAFETY.md`.

## Sicherheits-Checkliste

- [ ] Keine Secrets in `appsettings.json` (leerer Connection-String)
- [ ] `appsettings.Development.json` lokal, **nicht** eingecheckt
- [ ] `appsettings.Production.json` (im Repo) nur in privatem Repo
- [ ] `db.Database.Migrate()` niemals unconditional in `Program.cs`
- [ ] Vor jeder EF-Migration: `Docs/MIGRATION-SAFETY.md` lesen
- [ ] Prod-Migrationen nur manuell durch Server-Owner

## Troubleshooting

### Discord-Login: `invalid_client`

Tritt auf, wenn `Discord:ClientId` / `Discord:ClientSecret` beim
OAuth-Token-Endpoint leer oder falsch ankommen. Häufigste Ursachen
(in absteigender Wahrscheinlichkeit):

1. **`ASPNETCORE_ENVIRONMENT` ist nicht gesetzt** → `Production`-Default
   → `appsettings.Development.json` wird nicht geladen. **SEC002**-Warnung
   sollte beim Build erscheinen. Fix: `dotnet run --launch-profile http`
   oder `$env:ASPNETCORE_ENVIRONMENT = "Development"`.
2. **User-Secrets sind leer oder falsch geschrieben.** Prüfen mit
   `dotnet user-secrets list --project Erdi-ERC/Erdi-ERC.csproj`. **SEC001**
   warnt beim Build, falls leer. Siehe `Erdi-ERC/SETUP.md`.
3. **Discord-Secret wurde im Portal rotiert** oder die App-ID stimmt nicht
   mit der App überein, aus der das Secret stammt. Im
   [Discord Developer Portal](https://discord.com/developers/applications)
   nachprüfen und ggf. neu setzen.
4. **Whitespace oder BOM** vor/nach dem Secret (Copy-Paste aus Browser).
   Secret nochmal komplett neu eintippen.

### Pre-Build-Warnungen `SEC001` / `SEC002`

Das MSBuild-Target `WarnIfUserSecretsEmpty` läuft bei `dotnet build` und
`dotnet run`. Es warnt:
- **SEC001** — User-Secrets sind leer → App-Start crasht mit
  `MySqlException: Unable to connect`.
- **SEC002** — `ASPNETCORE_ENVIRONMENT` ist nicht gesetzt →
  Production-Default → `invalid_client` + falsche DB für EF.

Beide Checks sind lokal harmlos und via `DisableUserSecretsCheck=true`
deaktivierbar (für CI-Setups ohne lokale Secrets). In GitHub-Actions ist
`CI=true` gesetzt → Target überspringt sich automatisch.

## Mitwirkende

- **Bastian Frese** (`Erdi`, `Erdi`) — Lead, Bootstrap-Admin
- Beiträge über Issues / PRs willkommen (private Repo)

## Lizenz

Privat — alle Rechte vorbehalten.