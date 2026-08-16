---
datum: 2026-08-16
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

## app.env-Mechanik

Jedes Profil kopiert beim Publish automatisch das passende `app.env.{environment}.example` als `app.env` ins Publish-Output:

```
T:\app.env                    ← aus app.env.production.example
Y:\app.env                    ← aus app.env.development.example
```

**`app.env` enthält `ASPNETCORE_ENVIRONMENT=Production` bzw. `=Development` plus ConnectionString + Discord-Credentials als Override.**

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