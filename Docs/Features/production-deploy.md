---
datum: 2026-08-16
aktualisiert: 2026-09-28
tags: [doku, deploy, produktion, dev-staging]
status: [erledigt]
---

# Production & Dev-Staging Deploy

## Übersicht

| Ziel | Profil | PublishUrl | Environment | DB |
|---|---|---|---|---|
| **Production** (Server) | `FolderProfile.pubxml` | `T:\` | Production | `erdierc` |
| **Dev-Staging** (lokal) | `FolderProfile2.pubxml` | `Y:\` | Development | `erditest` |

Beide Profile publizieren Framework-dependent (`SelfContained=false`, `RuntimeIdentifier=linux-x64`) — auf dem Server ist `dotnet` installiert.

## Publish-Befehl

```bash
# Production
dotnet publish Erdi-ERC/Erdi-ERC.csproj -c Release -p:PublishProfile=FolderProfile

# Dev-Staging
dotnet publish Erdi-ERC/Erdi-ERC.csproj -c Release -p:PublishProfile=FolderProfile2
```

`PublishUrl` aus dem Profil wirkt **nur in Visual Studio** (Rechtsklick → Publish). Auf der
Kommandozeile legt `dotnet publish` das Output lokal unter
`Erdi-ERC/bin/Release/net10.0/linux-x64/publish/` ab und rührt `T:\` nicht an — geprüft am
2026-09-25: `T:\Erdi-ERC.dll` behielt seinen alten Zeitstempel, während das lokale Output
frisch war. Vom Profil kommt auf dem Server nur `app.env` an (`AfterTargets="Publish"`,
Ziel `$(PublishUrl)app.env`, mit `SkipUnchangedFiles`).

### Von dort auf den Server kopieren (Schritt, der leicht fehlt)

```powershell
robocopy "C:\Users\basti\source\repos\Erdi-ERC\Erdi-ERC\bin\Release\net10.0\linux-x64\publish" "T:\" /E /NFL /NDL /NJH /NJS
# NICHT sofort restarten: erst prüfen, ob die Migration auf der Ziel-DB steht.
# Der Copy selbst ist unkritisch (der laufende Prozess hält seine DLLs im Speicher).
# Siehe "Schema muss VOR dem Restart stehen".
# Danach auf dem Server:
# systemctl restart erdi-erc
```

Kein `/PURGE`/`/MIR`: der Server-Ordner enthält Laufzeitdaten, die im Publish-Output nicht
vorkommen (`data/`, `logs/`, `downloads/`-Uploads) — die dürfen nicht gelöscht werden.
`DeleteExistingFiles=false` im Profil schützt sie ebenfalls.

## Schema muss VOR dem Restart stehen

> [!danger] Der Staging-Copy allein ist unkritisch — der **Restart** ist der Punkt, an dem
> Code und Schema zusammenpassen müssen. Am 2026-09-28 hat ein Restart mit neuem Code gegen
> ein altes Schema **12 Minuten Totalausfall** verursacht (21:32:56–21:44:20).

Warum das tückisch ist: der laufende Prozess hält seine DLLs im Speicher, ein Copy nach `T:\`
ändert also nichts. Erst der Restart lädt den neuen Code, und wenn der Spalten erwartet, die
es noch nicht gibt, bricht **jede** Seite, die sie liest:

```
System.InvalidCastException: Unable to cast object of type 'System.Int32' to type 'System.Decimal'.
   at MySqlConnector.Core.Row.GetDecimal(...)
```
dazu `Unknown column '<alias>.<NeueSpalte>'`. Betroffen sind nur die Seiten, die die Spalte
lesen — Startseite, Statistik- und Rennergebnisseiten — während statische Dateien und der
Login unauffällig weiterlaufen.

> [!warning] **`/health/live` und `/health/ready` bleiben dabei grün.**
> `live` ist DB-frei, `ready` prüft nur die Verbindung, nicht das Schema. Ein grüner
> Health-Check beweist hier **nichts** — es muss eine echte inhaltslesende Seite abgerufen
> werden (z. B. `/`, `/Races/AllRaces`).

### Vor dem Restart: welche Migrationen fehlen?

1. **Was der Build mitbringt** — die letzte Migration im Assembly bzw.:
   ```bash
   ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations list --project Erdi-ERC/Erdi-ERC.csproj --startup-project Erdi-ERC/Erdi-ERC.csproj
   ```
   (`ASPNETCORE_ENVIRONMENT=Development` ist **Pflicht**, siehe
   [[project-migration-safety-aug2026]] — ohne sie trifft der Design-Time-Factory die Live-DB.)
2. **Was die Ziel-DB schon hat:**
   ```sql
   SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 5;
   ```
   Die neueste Zeile muss die letzte Migration des Builds sein.
3. **Falls nicht: erst migrieren, dann restarten.** Ist die Migration nachgezogen, während der
   neue Code schon läuft, ist **kein weiterer Restart nötig** — der Prozess hat den Code
   bereits, die Seiten kommen sofort zurück (nur `HomeIndexDataService` hat einen 60-s-Cache).

### Werkzeug (ohne MySQL-Client, ohne SSH)

`T:\` = `\\192.168.100.7\app` ist eingebunden, und `T:\appsettings.Production.json` enthält den
Prod-Connection-String (`Server=192.168.100.51;Database=erdierc`). Damit ist die Prüfung vom
Dev-Rechner aus möglich:

- **SQL-Runner** `%TEMP%\erc\proddb.cs` (liest den Connection-String aus `T:\`, maskiert das
  Passwort, **bricht ab, wenn `SELECT DATABASE()` nicht `erdierc` ist**). Das Sicherheitsnetz
  nie entfernen — `db.cs` zeigt auf `erditest`, eine Verwechslung wäre still. Liegt im Temp
  und ist damit flüchtig; nachbauen nach dem `db.cs`-Muster
  ([[feedback_dev_db_access_without_mysql_client]]).
- **Logs:** `T:\logs\erdi-erc-YYYYMMDD.log` (Serilog, tagesweise, vollständige Stacktraces).
  `Application started` / `Hosting environment:` markieren jeden Restart.
- **Läuft der neue Build?** Über einen Feature-Diskriminator im Response-Header, z. B.
  `curl -s -o /dev/null -D - https://erdi-erc.de/ | grep -i content-security-policy`.
  Generell gilt: ein Commit, der einen Header ändert, ist der zuverlässigste Build-Indikator
  von außen.

### Deploy-Checkliste (Prod)

- [ ] Lokaler Build grün, Tests grün.
- [ ] Migration ist auf `erditest` angewendet und dort verifiziert.
- [ ] Publish + robocopy nach `T:\` (ohne `/PURGE`).
- [ ] `T:\appsettings.json` / `appsettings.Production.json` / `app.env` unverändert (Zeitstempel prüfen).
- [ ] **`__EFMigrationsHistory` gegen die letzte Migration des Builds abgeglichen** (s. o.).
- [ ] Bei Bedarf migriert — Rollback-Referenz vorher notiert (Spaltentypen + Defaults aus `information_schema`).
- [ ] Erst jetzt `systemctl restart erdi-erc`.
- [ ] Live-Check mit einer **inhaltslesenden** Seite: `/`, `/Races/AllRaces`, `/Stats/EwigeListe` → 200, kein Redirect auf `/Home/Error`.
- [ ] Fehlerfreiheit im Log bestätigen: `T:\logs\erdi-erc-<heute>.log` nach dem Restart-Zeitpunkt.

## app.env-Mechanik

Jedes Profil kopiert beim Publish automatisch das passende `app.env.{environment}.example` als `app.env` ins Publish-Output:

```
T:\app.env                    ← aus app.env.production.example
Y:\app.env                    ← aus app.env.development.example
```

**`app.env` enthält `ASPNETCORE_ENVIRONMENT=Production` bzw. `=Development` plus ConnectionString + Discord-Credentials als Override.**

> [!warning] Das Profil schreibt `app.env` **auf das Publish-Ziel**, nicht ins lokale Output:
> `CopyAppEnvProduction` kopiert `app.env.production.example` (Platzhalter!) nach
> `$(PublishUrl)app.env`. Eine Publish aus Visual Studio überschreibt damit `T:\app.env`.
> Aktuell folgenlos — `T:\app.env` enthält ohnehin nur Platzhalter (Stand 23.08.2026), weil
> Prod `EnvironmentFile=/etc/erdi-erc/secrets.env` nutzt. Wer per CLI publisht, lässt das
> Profil weg (`-c Release -r linux-x64 --self-contained false`) oder prüft `T:\app.env`
> hinterher.

## Server-Startbefehl (manuell, ohne systemd)

```bash
#!/bin/bash
# /var/www/app/start.sh (Production)
set -a
source /var/www/app/app.env
set +a
exec dotnet /var/www/app/Erdi-ERC.dll
```

`set -a` macht alle zugewiesenen Variablen automatisch export. Dadurch liest ASP.NET Core `ASPNETCORE_ENVIRONMENT=Production` und lädt `appsettings.Production.json`. Die Override-Variablen (`ConnectionStrings__Default`, `Discord__ClientId`, etc.) überschreiben Werte aus der JSON-Datei.

## Lokal vs. Repo

| Datei | Git-Status | Inhalt |
|---|---|---|
| `app.env.production.example` | tracked | Platzhalter (`<..._REMOVED>`) |
| `app.env.development.example` | tracked | Platzhalter |
| `app.env.production` | **gitignored** (lokal) | echte Werte, NICHT committen |
| `app.env.development` | **gitignored** (lokal) | echte Werte, NICHT committen |

**Setup beim ersten Deploy:**
1. Lokal: `cp app.env.production.example app.env.production` und Platzhalter ersetzen.
2. Publish-Profil wählen, `dotnet publish` ausführen.
3. `app.env` auf den Server kopieren (in `/var/www/app/`).
4. Startskript wie oben anlegen, `chmod +x start.sh`.

## Sicherheits-Checkliste

- [ ] `app.env.production` ist in `.gitignore` (bereits erledigt).
- [ ] `app.env.production` enthält KEINE Platzhalter mehr vor dem Publish.
- [ ] `app.env.production` wird NIE ins Repo committed (`git status` checken).
- [ ] Server: `app.env` ist nur für den Service-User lesbar (`chmod 600`).

## Siehe auch

- `Erdi-ERC/Properties/PublishProfiles/FolderProfile.pubxml`
- `Erdi-ERC/Properties/PublishProfiles/FolderProfile2.pubxml`
- `Erdi-ERC/app.env.production.example`
- `Erdi-ERC/app.env.development.example`
- [[project-migration-safety-aug2026]] — `db.Database.Migrate()` läuft nur in `IsDevelopment()`, also nie auf Production-Server.
- [[project_prod_deploy_workflow_sep2026]] — verifizierter Ablauf inkl. Vorfall vom 2026-09-28.
- [[feedback_prod_diagnostics_without_ssh]] — Logs auf `T:\logs`, Prod-SQL-Runner, Build-Diskriminator.