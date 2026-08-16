---
datum: 2026-08-17
tags: [tasks, morgen, deploy, ssh]
status: [offen]
---

# Morgen-Plan: Publish-Profile Environment-Trennung live bringen

**Status:** Repo ist committed + gepusht (`master = f2f7f58`). Was fehlt: lokale `app.env` aus den `.example`-Templates generieren + Server-Deploy.

## Schritt 0: SSH-Key bereitstellen

**Morgen früh:** Bastian gibt mir seinen SSH-Key zum Server (T:-Laufwerk = `/var/www/app` auf dem Live-Server). Ohne den Key kann ich nicht deployen — `dotnet publish` läuft zwar lokal, aber `app.env` muss auf den Server kopiert werden und der Server-Start muss neu aufgesetzt werden.

**Was ich brauche:**
- Public Key (oder den Private Key, falls Bastian mir temporär Zugriff gibt)
- Zielpfad: `/var/www/app/` (Document-Root auf dem Server)
- Zielpfad für Dev-Staging: `/var/www/dev/` (Y:\-Mapping)
- Login-User (vermutlich `bastian` oder `www-data`)
- Bestehender Server-Startbefehl (was läuft da aktuell — systemctl? bare `dotnet`? nginx-Proxy davor?)

## Schritt 1: Lokale app.env-Dateien aus Templates generieren

```bash
cd "C:\Users\basti\source\repos\Erdi-ERC\Erdi-ERC"

# Production
cp app.env.production.example app.env.production
# Dann <..._REMOVED>-Platzhalter durch echte Werte ersetzen.
# Werte kommen NICHT ins Repo — Bastian hat sie in einer separaten
# Notiz (analog bindefinitivamarbeiten.txt) und kopiert sie per Hand
# in die 6 Felder: <PROD_HOST>, <PROD_DB_USER>, <PROD_DB_PASSWORD_REMOVED>,
# <DISCORD_CLIENT_ID_REMOVED>, <DISCORD_CLIENT_SECRET_REMOVED>,
# <DISCORD_WEBHOOK_TOKEN_REMOVED>.

# Development (analog)
cp app.env.development.example app.env.development
# 6 Felder analog: <DEV_DB_HOST>, <DEV_DB_USER>, <DEV_DB_PASSWORD_REMOVED>,
# <DISCORD_CLIENT_ID_REMOVED>, <DISCORD_CLIENT_SECRET_REMOVED>,
# <DISCORD_WEBHOOK_TOKEN_REMOVED>.
```

## Schritt 2: Publish laufen lassen

```bash
# Production → T:\ (Server)
dotnet publish Erdi-ERC/Erdi-ERC.csproj -c Release -p:PublishProfile=FolderProfile

# Development → Y:\ (Dev-Staging)
dotnet publish Erdi-ERC/Erdi-ERC.csproj -c Release -p:PublishProfile=FolderProfile2
```

Erwartet: `app.env` wird jeweils ins Publish-Output kopiert (via `<Target Name="CopyAppEnv..." AfterTargets="Publish">`).

## Schritt 3: Server-Deploy (Production)

```bash
# T:\-Publish-Output auf Server kopieren
scp -r T:\* user@server:/var/www/app/

# Server: app.env korrekt platzieren
ssh user@server 'chmod 600 /var/www/app/app.env'

# Server: start.sh anlegen
cat > /var/www/app/start.sh <<'EOF'
#!/bin/bash
set -a
source /var/www/app/app.env
set +a
exec dotnet /var/www/app/Erdi-ERC.dll
EOF
chmod +x /var/www/app/start.sh
```

## Schritt 4: Server-Startbefehl austauschen

**Aktuell unklar** was auf dem Server läuft. Wenn systemd:
```bash
# /etc/systemd/system/erdi-erc.service neu schreiben mit ExecStart=/var/www/app/start.sh
sudo systemctl daemon-reload
sudo systemctl restart erdi-erc
```

Wenn nginx-Proxy davor: nginx-config unverändert lassen (lauscht auf 127.0.0.1:5000 oder ähnlich).

## Schritt 5: Verifikation

```bash
# Server-seitig:
curl https://erdierc.de  # muss antworten
curl -I https://erdierc.de  # HTTP 200, kein 500
journalctl -u erdi-erc -f  # muss zeigen: "Now listening on: http://localhost:5000"
# DB-Check: Login mit Discord-OAuth muss funktionieren (vs erdierc-DB, NICHT erditest!)
```

## Was ich morgen brauche

1. **SSH-Key** zum Server (Public Key für mich, oder Bastian macht die Server-Schritte selbst).
2. **Bestätigung** welcher Server-User + Pfad.
3. **Aktueller systemd-Service-Status** (oder nix = manueller `dotnet`-Start).
4. **Echte Werte** für die 6 Platzhalter pro `app.env` (aus separater Notiz, nicht im Repo).
5. **5 Minuten** für die Server-Schritte (Schritt 3 + 4).

## Sicherheits-Checkliste

- [ ] `app.env.production` ist in `.gitignore` (✅ schon erledigt)
- [ ] `git status` zeigt KEINE `app.env.production` als tracked
- [ ] `app.env` auf Server hat `chmod 600`
- [ ] `start.sh` hat `chmod +x`
- [ ] systemd-Service läuft NICHT als root (sicherer User)

## Wenn etwas schiefgeht

- **Discord-OAuth invalid_client**: `appsettings.Production.json` + `app.env.production` haben verschiedene Credentials → Werte abgleichen. Memory: `feedback_aspnetcore_env_default.md`.
- **DB-Connection refused**: `ConnectionStrings__Default` in `app.env.production` überschreibt `appsettings.Production.json` — entweder konsistent setzen oder ENV-Var entfernen.
- **EF-Migration läuft auf Prod**: `db.Database.Migrate()` in `Program.cs` ist durch `IsDevelopment()` gated — **darf NICHT** auf Prod laufen. Memory: `project_migration_safety_aug2026.md`.