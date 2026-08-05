---
datum: 2026-08-05
tags: [sicherheit, deployment, datenbank, migration]
status: [erledigt]
---

***REMOVED*** MIGRATION-SAFETY — niemals Schema-Änderungen auf `erdierc` aus versehen

> **Pflichtlektüre vor jeder EF-Migration, vor jedem `dotnet ef database update`,
> vor jedem SSH-Zugriff auf den Live-Server, und vor jedem neuen Build-Job.**

***REMOVED******REMOVED*** Geltungsbereich — ohne Ausnahme

Diese Regel betrifft **den gesamten Test- und Bau-Prozess des neuen Systems**
(Bewerbungs-Rebuild und alles, was danach kommt):

- lokales Bauen (`dotnet build`, `dotnet test`)
- CI (`/.github/workflows/ci.yml`)
- Publish-Workflow (`/.github/workflows/publish.yml`)
- manuelles `dotnet ef database update` auf Entwickler-Maschinen
- manuelles `dotnet ef migrations add` / `script` (offline)
- **alles, was eine EF-Migration in eine echte Datenbank schreibt**

***REMOVED******REMOVED*** Die zwei Datenbanken

Beide liegen auf demselben MySQL-Server `<PROD_HOST>:3306`:

| Umgebung | DB-Name       | Connection-File                          | Status      |
|----------|---------------|------------------------------------------|-------------|
| Dev      | **`erditest`** | `appsettings.Development.json` (lokal)   | anfassen ✅  |
| Prod     | **`erdierc`**   | `appsettings.Production.json` (im Repo!) | **LIVE** ❌ |

> ⚠️ `appsettings.Production.json` ist im Repo eingecheckt (siehe `.gitignore` Kommentar),
> weil das Projekt eine **private Repo** ist. Trotzdem gilt: **Prod-DB-Inhalt ist
> unwiederbringlich, sobald eine Migration läuft.**

***REMOVED******REMOVED*** Erlaubt

```bash
***REMOVED*** Lokal, gegen erditest
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update
dotnet ef database update  ***REMOVED*** wenn env=Development gesetzt ist (launchSettings.json)
```

```bash
***REMOVED*** Offline — Migrationen nur generieren, kein DB-Zugriff
dotnet ef migrations add <Name> --no-build
dotnet ef migrations script <From> <To> --output mig.sql   ***REMOVED*** schreibt nur SQL-Datei
```

***REMOVED******REMOVED*** Verboten — ohne Wenn und Aber

```bash
***REMOVED*** NIEMALS — explizit Production setzen und update fahren
ASPNETCORE_ENVIRONMENT=Production dotnet ef database update

***REMOVED*** NIEMALS — auf dem Live-Server (<PROD_HOST>) per Hand
ssh user@erdierc-host "cd /srv/erdi-erc && dotnet ef database update"

***REMOVED*** NIEMALS — in CI / Publish / einem neuen Build-Job, der prod credentials kennt
```

**Was passiert, wenn man es trotzdem tut?**
- `erdierc` verliert Daten (z. B. die jetzt entfernte `ApplicationForms`-Tabelle
  mit ~125 Datensätzen, alle realen Bewerbungen — wäre **nicht wiederherstellbar**).
- `erdierc` ist LIVE hinter Nginx. Ausfall = öffentlicher Service-Down.
- Es gibt **kein** Backup-Automationsskript, das den Schaden rückgängig macht.

***REMOVED******REMOVED*** Sicherheitsnetze

1. **CI macht keinen `ef database update`** — `/.github/workflows/ci.yml` führt nur
   `dotnet restore` / `build` / `test` aus. Kein DB-Zugriff, keine prod credentials.
   ➡️ Sicher.

2. **Publish macht keinen `ef database update`** — `/.github/workflows/publish.yml` packt
   das Artefakt und legt es via SSH + `systemctl restart` auf den Server.
   ➡️ Sicher.

3. **`launchSettings.json`** setzt für beide lokalen Profile
   `ASPNETCORE_ENVIRONMENT=Development`. Damit zeigt ein versehentliches
   `dotnet ef database update` aus VS/Rider heraus **immer** auf `erditest`,
   solange niemand die Env-Variable überschreibt.
   ➡️ Sicher **für den Normalfall**.

4. **`appsettings.json`** (committed) hat **leeren** Connection-String — Prod-Daten
   stehen ausschließlich in `appsettings.Production.json` und werden nur per
   `ASPNETCORE_ENVIRONMENT=Production` aktiv.
   ➡️ Sicher.

5. **`Program.cs` ruft `Database.Migrate()` ausschließlich in Development auf** —
   Prod-Server-Start führt **keine** Schema-Änderungen durch. Prod-Migrationen
   laufen **explizit** per `dotnet ef database update` durch den Server-Owner.
   ➡️ Sicher. (Das ist neu seit 2026-08-05; war vorher ein Leck.)

***REMOVED******REMOVED*** Wo die Regel gebrochen werden kann (Risiko-Trigger)

| Trigger                                                                    | Risiko                                                                             |
|----------------------------------------------------------------------------|------------------------------------------------------------------------------------|
| `ASPNETCORE_ENVIRONMENT=Production` wird lokal exportiert                  | jedes `ef` Kommando geht live                                                      |
| `ef database update` läuft auf dem **Prod-Server** direkt (nicht via Deploy) | umgeht den Deploy-Prozess komplett                                                  |
| Neuer Build-Job mit prod credentials auf GitHub-Actions                     | prod-DB wird vom CI getriggert, nicht nur vom Code                                  |
| `appsettings.Production.json` ändert sich still                           | Hinweis auf prod-Konfig-Drift; jede Schema-Änderung sollte in der PR sichtbar sein  |
| `Database.Migrate()` wird in `Program.cs` **wieder** unconditional aufgerufen | Re-Leckt das Startup-Migrate — Sicherheitsnetz 5 weg                            |

> Aktueller Stand: `Program.cs` ist so gepatcht, dass `db.Database.Migrate()` NUR
> in `IsDevelopment()` läuft. Das ist Absicht und soll so bleiben.

***REMOVED******REMOVED*** Checkliste vor jeder Migration

```
[ ] ASPNETCORE_ENVIRONMENT ist NICHT 'Production'
[ ] Connection-String zeigt auf 'erditest', nicht 'erdierc'
[ ] Lokaler `dotnet build` ist grün
[ ] Migration lokal generiert UND auf erditest angewendet — funktioniert
[ ] Bei Breaking-Change: Down-Migration im selben Commit
[ ] Auf Prod NUR über den Deploy-Workflow (publish.yml), NIEMALS lokal ssh + ef update
```

***REMOVED******REMOVED*** Wenn doch mal was schief geht — Eskalation

1. **Stoppen**: nichts mehr ausführen.
2. **Audit**: letzte ausgeführten `ef`-Kommandos nachvollziehen
   (`Update-Database.sql`-History, EF-Migrationen-Tabelle `__EFMigrationsHistory`).
3. **User informieren** — der hat den prod-Zugriff und das Backup-Privileg.
4. **Keine eigene Reparatur** — Datenrettung läuft gegen prod, nicht gegen dev.

***REMOVED******REMOVED*** Bezug zu bestehendem Setup

- siehe `<OWNER_HANDLE>-ERC/appsettings.Production.json` (Repo, mit prod credentials)
- siehe `<OWNER_HANDLE>-ERC/Properties/launchSettings.json` (Dev-Profile)
- siehe `.github/workflows/ci.yml` (Build+Test, keine DB)
- siehe `.github/workflows/publish.yml` (Deploy, kein `ef update`)
- verwandt: `Docs/README.md`

***REMOVED******REMOVED*** Versionierung

- 2026-08-05 — Erstversion, entstanden während des Bewerbungssystem-Removes
  (Migration `20260805150724_DropApplicationForms` darf nur erditest treffen).