# Erdi-ERC

Web-Plattform für die Sim-Racing-Community **Erdi's Racing Community (ERC)**. Die Anwendung
verwaltet Liga-Betrieb, Rennergebnisse, Wertungen, Fahrerprofile und den Community-Bereich
und nimmt Rennergebnisse aus einem externen Telemetrie-Client entgegen.

Live im Einsatz unter <https://erdi-erc.de>.

Technisch: ASP.NET Core MVC (Razor Views), .NET 10, Entity Framework Core mit MySQL/MariaDB.

---

## Was die Anwendung tut

Kern ist die Ligaverwaltung: Rennen, Rennergebnisse, Fahrer- und Konstrukteurswertungen über
mehrere Saisons und Divisionen hinweg. Darum herum gruppieren sich Bewerbungs- und
Aufnahmeworkflow, Community-Inhalte, ein Setup-Bereich mit zugriffsgestufter Freigabe, die
Auswertung von Altdaten und ein Ingest-Pfad für Rennergebnisse aus einem Telemetrie-Client.

### Funktionsbereiche

**Liga und Rennen**
Verwaltung von Ligen, Saisons und Rennwochenenden (mehrteilige Wochenenden), Erfassung und
Import von Rennergebnissen – einzeln oder als CSV – inklusive Rücknahme gelöschter Rennen.
Öffentliche Ansichten für Ergebnisse, Gesamtwertungen, Konstrukteurswertungen, Rennkalender
und Renndetails. Reserve- und Gastfahrer lassen sich einzelnen Rennen zuordnen.

**Bewerbungen und Warteliste**
Discord-gestützter Bewerbungsweg mit Ziel-Saison pro Liga und Kapazitätsprüfung. Adminseitig
Annehmen, Ablehnen, Warteliste mit Nachrücken sowie manuelle Registrierung. Beim Annehmen
werden Fahrerprofil, Gamer-Tag und Wertungseintrag angelegt.

**Statistiken und Ehrungen**
Ewige Liste, Hall of Fame über Ligen hinweg, Konstrukteursauswertung sowie ein aus Aktivität
abgeleitetes Fahrerlevel. Wertungen werden nach Ergebnisänderungen neu berechnet; ein
Punkte-Faktor bildet abgebrochene Rennen ab.

**Fahrerprofile und Erfolge**
Profile mit Plattform, Gerät, Nationalität, Lieblingsstrecke und -team, Foto-Upload und
Pinnwand. Fahrerkarten, Discord-Verknüpfung sowie frei definierbare Erfolge.

**Setups**
Strecken-Setups mit Kommentaren, Likes und Freigabe nach Zugriffsstufe. Die Stufe wird aus
Discord-Guild-Rollen abgeleitet; Adminseitig Zuweisung, Sperre und Freigabe.

**Community**
News, Abstimmungen, von Nutzern eingereichte Highlights, Real-Life-Events mit Bildergalerien,
Stream-Pläne und Gewinnspiele. Dazu Team-Seiten und ein Tauschbrett für Reservefahrer.
Stewarding mit Strafen und Strafpunkten.

**Telemetrie-Ingest**
Ein externer Client meldet Rennergebnisse über eine per-Fahrer-API an. Meldungen landen
zunächst in einer Prüfliste (`/admin/telemetry`) und werden erst nach Freigabe zu echten
Renndaten. Zusätzlich steht eine öffentliche Wertungs-API als JSON bereit, die von
Overlay-Grafiken genutzt wird.

**Redaktions- und Systembereich**
Hintergrundmusik, About-Me-Profile, Regelwerk (Upload als PDF oder Markdown), Discord-Webhooks
mit Automatisierungsregeln, granulare Admin-Berechtigungen mit Audit-Log, Wartungsmodus sowie
das Hochladen der Ewigen Liste als Arbeitsmappe.

**Troll/Gags**
Eine Sammlung spielerischer Einblendungen, die beim Login erscheinen können – pro Gag
gewichtet, mit Abkühlzeit und Abbruch nach mehreren Fehlversuchen.

### API-Endpunkte (Auswahl)

| Route | Auth | Zweck |
|---|---|---|
| `POST /api/telemetry/race` | API-Key | Rennergebnis einliefern (landet in der Prüfliste) |
| `GET /api/telemetry/leagues` | API-Key | Ligen des Key-Inhabers |
| `GET /api/telemetry/me` | API-Key | Key auflösen (Verbindungstest) |
| `GET /api/telemetry/standings` | öffentlich | Fahrer- und Teamwertung als JSON |
| `GET /api/setups` | Discord-Token | Setups nach Strecke und Spieljahr, stufengefiltert |
| `GET /health/live` | öffentlich | Liveness |
| `GET /health/ready` | öffentlich | Readiness inkl. Datenbankverbindung |

---

## Technische Basis

| Bereich | Verwendung |
|---|---|
| Framework | .NET 10 (`net10.0`), ASP.NET Core MVC mit Razor Views |
| Datenbank | MySQL/MariaDB über `Pomelo.EntityFrameworkCore.MySql` |
| ORM | Entity Framework Core 9.0 (`Microsoft.EntityFrameworkCore` + `.Relational`, `.Design`) |
| Authentifizierung | Discord OAuth 2 über `AspNet.Security.OAuth.Discord` 10.0, Cookie-Authentifizierung |
| Autorisierung | Claim-basiert, 19 granulare Berechtigungsschlüssel in fünf Gruppen plus Superadmin |
| Logging | Serilog (Konsole + rollierende Dateien) |
| PDF | `UglyToad.PdfPig` – Textextraktion aus Regelwerk-PDFs |
| Markdown | `Markdig` – Regelwerk als Markdown |
| Tabellenkalkulation | `ClosedXML` – Ewige Liste als `.xlsx` lesen und schreiben |
| Frontend-Build | esbuild (JS-Minify) und MSBuild-Targets (CSS-Bundling); optional, mit Fallback |
| Tests | xUnit, EF Core InMemory/SQLite, `Microsoft.AspNetCore.Mvc.Testing` |

Weitere Eigenschaften der Laufzeit: CSRF-Schutz global für schreibende Verben, Rate Limiting
in getrennten Buckets (global, Auth, Formulare, Telemetrie), Response-Compression (Brotli/Gzip),
Output-Caching für öffentliche JSON-Endpunkte, HSTS in Production, Reverse-Proxy-Header für den
vorgelagerten Nginx sowie ein Wartungsmodus, der Nicht-Admins eine Statuseite zeigt.

---

## Projektstruktur

```
Erdi-ERC.slnx                     Solution (slnx-Format)
Erdi-ERC/                         Web-Anwendung
  Program.cs                      Composition Root, Pipeline, Auth, Policies
  Controllers/                    26 Controller (17 davon im Admin-Bereich)
  Services/                       Anwendungslogik (43 Dateien, davon 15 Schnittstellen)
  Models/                         EF-Entitäten und ViewModels
  ViewModels/                     ViewModels
  Views/                          Razor-Views (102 .cshtml)
  Data/                           AppDbContext (47 DbSets), Design-Time-Factory, Seeder
  Migrations/                     66 EF-Migrationen
  Helpers/                        PDF-, Markdown-, Punkte-, Team- und Wertungshelfer
  Middleware/                     Wartungsmodus
  Options/                        Stark typisierte Konfigurationsklassen (11)
  wwwroot/                        CSS, JS, Bilder, Client-Bibliotheken
  SETUP.md                        Anleitung für die lokale Einrichtung
  appsettings.*.example.json      Konfigurationsvorlagen (ohne Werte)
  app.env.*.example               Entsprechende Vorlagen als Umgebungsvariablen
tests/Erdi-ERC.Tests/             xUnit-Tests
deploy/                           systemd-Unit, MySQL-Backup, secrets.env-Vorlage
tools/MediaWebPConverter/         Konvertiert Upload-Bilder offline nach WebP
Docs/                             Projekt- und Feature-Dokumentation
.github/workflows/                ci.yml, publish.yml
ewige_export/                     CSV-Export der Ewigen Liste
```

---

## Voraussetzungen

- .NET SDK 10.0 (das Projekt zielt auf `net10.0`)
- MySQL 8 oder MariaDB, erreichbar vom Entwicklungsrechner
- Eine Discord-Anwendung für den OAuth-Login
  (Redirect-URI auf `…/signin-discord` eintragen)
- Optional: Node.js mit `npx` für die JS-/CSS-Minifizierung. Fehlt es, läuft der Build weiter
  und die Anwendung lädt die unmifizierten Quelldateien.

---

## Lokales Setup

### 1. Abhängigkeiten und Tools

```bash
git clone <repository-url>
cd Erdi-ERC

dotnet restore Erdi-ERC.slnx
dotnet tool restore --tool-manifest Erdi-ERC/dotnet-tools.json   # dotnet-ef 10.0.7
```

Das Manifest bindet `dotnet-ef` in der Version 10.0.7. Ohne `dotnet tool restore` steht der
Befehl `dotnet ef` nicht zur Verfügung.

### 2. Secrets setzen

Secrets werden **niemals** eingecheckt. Für die lokale Entwicklung kommen sie aus
`dotnet user-secrets`, in Production aus Umgebungsvariablen:

```bash
dotnet user-secrets init  --project Erdi-ERC/Erdi-ERC.csproj
dotnet user-secrets set "ConnectionStrings:Default" "<connection-string>" --project Erdi-ERC/Erdi-ERC.csproj
dotnet user-secrets set "Discord:ClientId"     "<client-id>"     --project Erdi-ERC/Erdi-ERC.csproj
dotnet user-secrets set "Discord:ClientSecret" "<client-secret>" --project Erdi-ERC/Erdi-ERC.csproj

dotnet user-secrets list --project Erdi-ERC/Erdi-ERC.csproj   # Prüfen
```

Der Connection-String hat die Form
`Server=<host>;Port=3306;Database=<db>;User=<user>;Password=<passwort>;TreatTinyAsBoolean=true`.

Ein MSBuild-Hook warnt beim Build bzw. Start, wenn die User-Secrets leer sind (`SEC001`) oder
`ASPNETCORE_ENVIRONMENT` nicht gesetzt ist (`SEC002`). In CI ist die Prüfung deaktiviert.

### 3. Konfigurationsschlüssel

Im Repository liegen ausschließlich Vorlagen mit Platzhaltern, niemals echte Werte:

- `Erdi-ERC/appsettings.Development.example.json`
- `Erdi-ERC/appsettings.Production.example.json`
- `Erdi-ERC/app.env.development.example` / `app.env.production.example`
  (dieselben Schlüssel als Umgebungsvariablen, Trennzeichen `__`)

Zu belegende Schlüssel:

| Schlüssel | Bedeutung |
|---|---|
| `ConnectionStrings:Default` | Datenbankverbindung (in Production zwingend erforderlich) |
| `Discord:ClientId` | OAuth-Client der Discord-Anwendung (in Production zwingend) |
| `Discord:ClientSecret` | OAuth-Secret der Discord-Anwendung (in Production zwingend) |
| `Discord:SetupAccess:GuildId` | Guild, aus der die Setup-Zugriffsstufe abgeleitet wird |
| `Discord:Guilds:CommunityGuildId` | Gemeinschafts-Guild |
| `Discord:Guilds:LeagueGuildId` | Liga-Guild |
| `Discord:Webhook:Url` | Webhook für Benachrichtigungen |
| `Discord:Webhook:Username` | Anzeigename des Webhook-Absenders |
| `BootstrapAdmins` | Liste von `{ DiscordId, DisplayName }` – beim ersten Start in die Admintabelle übernommen |

Fehlen in der Umgebung `Production` der Connection-String oder die Discord-Credentials,
bricht der Start mit einer klaren Meldung ab, statt mit falscher Konfiguration weiterzulaufen.
Die vollständige Anleitung steht in `Erdi-ERC/SETUP.md`.

### 4. Datenbank und Migrationen

Das Projekt enthält 66 EF-Migrationen. Sie werden **nur in der Umgebung `Development`
automatisch** beim Start angewendet; in Production laufen sie ausdrücklich von Hand, damit
der Anwendungsstart keine unkontrollierten Schemaänderungen auslöst:

```bash
# Development: passiert automatisch beim Start.
# Manuell bzw. Production:
dotnet ef database update --project Erdi-ERC/Erdi-ERC.csproj
```

Vor jeder Migration im Produktivbetrieb gehört ein Blick in `Docs/MIGRATION-SAFETY.md`.
Der Seeder legt beim ersten Start Bootstrap-Admins, Erfolgsdefinitionen und – falls keine
vorhanden sind – Beispielligen an.

### 5. Starten

```bash
dotnet run --project Erdi-ERC --launch-profile http
```

Die Profile aus `Properties/launchSettings.json`:

| Profil | Adresse | Umgebung |
|---|---|---|
| `http` | <http://localhost:5005> | Development |
| `https` | <https://localhost:7030> | Development |

Für den Discord-Login muss die Redirect-URI der Discord-Anwendung zum verwendeten Profil
passen, also auf `http://localhost:5005/signin-discord` oder
`https://localhost:7030/signin-discord` zeigen.

---

## Tests

```bash
dotnet test Erdi-ERC.slnx --nologo
```

Der Testbestand umfasst **626 Testfälle** in 96 Testklassen (56 Dateien); die Fälle verteilen
sich auf 411 `[Fact]` und 44 `[Theory]` (deren `[InlineData]`-Zeilen als eigene Fälle gezählt
werden). Die Suite läuft ohne Datenbank – sie nutzt EF Core InMemory bzw. SQLite sowie
`Microsoft.AspNetCore.Mvc.Testing` – und ist in wenigen Sekunden durch.

```bash
# Nur auflisten, ohne auszuführen
dotnet test Erdi-ERC.slnx --nologo -v q --list-tests
```

Abgedeckt sind unter anderem Liga- und Wertungslogik, CSV- und Telemetrie-Parser,
Bewerbungs- und Setups-Controller, Berechtigungsprüfungen, der Wartungsmodus sowie
Hilfsfunktionen für Punkte, Farben und Initialen.

---

## CI/CD

### `ci.yml` – Build und Tests

Ausgelöst bei Pushes und Pull Requests auf `master`. Richtet .NET 10 ein, restauriert, baut
im Release-Profil und führt die Tests aus.

### `publish.yml` – Veröffentlichung und Deployment

Ausgelöst über Tags der Form `v*.*.*` oder manuell über `workflow_dispatch`.

Der Job `publish` veröffentlicht framework-abhängig für `linux-x64`, packt das Ergebnis als
`tar.gz`, legt es als Build-Artefakt ab und erstellt bei einem Tag einen GitHub-Release mit
generierten Release-Notes.

Der Job `deploy` läuft nur bei Tags und in der Umgebung `production`. Er kopiert das Archiv
per `scp` auf den Server, entpackt es nach `<app-dir>/releases/<zeitstempel>`, schaltet den
Symlink `current` um, startet den systemd-Dienst neu, prüft dessen Status und ruft
abschließend `https://<public-url>/health/live` als Smoke-Test auf. Die letzten fünf Releases
werden vorgehalten, ältere entfernt.

Benötigte Repository-Secrets: `DEPLOY_SSH_KEY`, `DEPLOY_HOST`, `DEPLOY_USER`,
`DEPLOY_APP_DIR`, `DEPLOY_SERVICE`, `DEPLOY_PUBLIC_URL`, optional `DEPLOY_SSH_PORT`.

### Betrieb auf dem Server

`deploy/erdi-erc.service` ist die systemd-Unit: sie startet die Anwendung als Benutzer `erdi`
auf `http://127.0.0.1:5000`, liest die Secrets aus `/etc/erdi-erc/secrets.env` und startet bei
einem Absturz automatisch neu. Davor liegt ein Reverse Proxy, der TLS terminiert.

---

## Secrets im Repository

Echte Konfigurationsdateien werden **nie** eingecheckt. `.gitignore` schließt
`appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json`,
`appsettings.Local.json`, `secrets.json`, Schlüsseldateien (`*.pfx`, `*.key`) sowie
hochgeladene Inhalte, Datenbank-Backups und Laufzeitdaten aus.

Im Repository liegen ausschließlich die Vorlagen `*.example.json` und `app.env.*.example` –
mit Platzhaltern, ohne Werte. Echte Secrets kommen aus Umgebungsvariablen oder, lokal,
aus `dotnet user-secrets`. Der Build-Hook, der die User-Secrets prüft, liest bewusst nur die
Anzahl der Schlüssel und schreibt keine Werte ins Build-Log.

---

## Status

Dieses Repository dient als Portfolio-Einblick in eine produktiv genutzte Anwendung. Die
Plattform läuft live unter <https://erdi-erc.de> und wird für den laufenden Liga-Betrieb einer
Sim-Racing-Community weiterentwickelt. Der Code ist an die Bedürfnisse dieser Community
zugeschnitten – Discord-Guild-Strukturen, Zugriffsstufen und Liga-Regeln sind entsprechend
spezifisch und nicht als allgemeine Bibliothek gedacht.
