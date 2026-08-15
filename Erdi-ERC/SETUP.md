# Setup-Anleitung für lokale Entwicklung

Dieses Projekt speichert **keine Secrets** im Repo (Connection-Strings, Discord-OAuth-Credentials, Webhook-Tokens). Stattdessen nutzt es `dotnet user-secrets` — die Werte landen verschlüsselt im Windows-DPAPI (User-Profil), nicht in einer Source-File.

> **Warum?** Die `appsettings.Production.json` ist im Repo (nur in privatem Fork), aber `appsettings.Development.json` und alle Dev-Credentials bleiben **lokal** auf deinem Rechner. Bei einem versehentlichen Push oder einem Fork durch einen Kollaborator sind die Dev-Werte sicher.

## Einmalige Initialisierung (nach `git clone`)

```powershell
cd C:\Users\basti\source\repos\Erdi-ERC

# 1) User-Secrets-Slot für das Projekt initialisieren (legt eine GUID an)
dotnet user-secrets init --project Erdi-ERC/Erdi-ERC.csproj

# 2) Connection-String zur Dev-DB setzen
#    Server/Port: typischerweise 192.168.100.51:3306 (Heimnetz)
#    Database:    erditest (NICHT erdierc — das ist LIVE)
#    User/Pass:   dein Dev-DB-Credential (nicht dasselbe wie Prod!)
dotnet user-secrets set "ConnectionStrings:Default" "Server=<DEIN-DEV-HOST>;Port=3306;Database=erditest;User=erditest;Password=<DEIN-DEV-PASSWORT>;TreatTinyAsBoolean=true" --project Erdi-ERC/Erdi-ERC.csproj

# 3) Discord-OAuth-Credentials setzen (für Login lokal)
#    Diese bekommst du aus https://discord.com/developers/applications
dotnet user-secrets set "Discord:ClientId" "<DEIN-CLIENT-ID>" --project Erdi-ERC/Erdi-ERC.csproj
dotnet user-secrets set "Discord:ClientSecret" "<DEIN-CLIENT-SECRET>" --project Erdi-ERC/Erdi-ERC.csproj
```

## Verifikation

```powershell
# Zeigt alle gesetzten User-Secrets (Werte werden maskiert angezeigt)
dotnet user-secrets list --project Erdi-ERC/Erdi-ERC.csproj
```

Erwartete Ausgabe (Beispiel):
```
ConnectionStrings:Default = Server=...;Password=***
Discord:ClientId = ***
Discord:ClientSecret = ***
```

## Pre-Build-Check

Das Projekt hat einen MSBuild-Hook, der beim Build/Run **warnt**, falls User-Secrets leer sind. So bekommst du beim ersten VS-Start den Hinweis:

```
warning SEC001: User-Secrets sind leer. Führe die Schritte in SETUP.md aus.
```

## Falls Werte verloren gehen

User-Secrets liegen in:
```
%APPDATA%\Microsoft\UserSecrets\<GUID>\secrets.json
```

Diese Datei ist **nicht** im Repo. Backup-Strategien:
- Password-Manager-Eintrag für jeden Wert
- Verschlüsselter Cloud-Sync (z.B. Tresorit, OneDrive mit Personal Vault)
- **NICHT** in ein Repo einchecken — auch nicht in privates

## Datenbank-Split (Dev vs. Prod)

| Umgebung | DB          | Connection-File                   | Status |
|----------|-------------|-----------------------------------|--------|
| Dev      | `erditest`  | User-Secrets (oder Dev-Config)    | anfassen ✅ |
| Prod     | `erdierc`   | `appsettings.Production.json` (im Repo, aber private) | **LIVE — nicht anfassen** |

`appsettings.Development.json` enthält **Platzhalter** (`<PROD_HOST>`, `<DEV_DB_PASSWORD_REMOVED>` etc.) als Erinnerung, dass hier nichts Hartes stehen darf. Die echten Werte müssen via User-Secrets kommen.

## Was NICHT funktioniert ohne User-Secrets

- `dotnet run` / VS F5 → crasht mit `MySqlException: Unable to connect to any of the specified MySQL hosts`
- `dotnet ef database update` → crasht mit demselben Fehler
- Login via Discord → crasht mit `ClientId missing`
- Tests laufen weiterhin (siehe `tests/Erdi-ERC.Tests/`), weil sie SQLite-In-Memory nutzen

## Troubleshooting

**`dotnet user-secrets list` zeigt keine Werte, aber ich habe sie gesetzt:**
- Bist du im richtigen Verzeichnis? `cd` muss im Repo-Root sein.
- Richtiges Projekt? `--project Erdi-ERC/Erdi-ERC.csproj` muss exakt so angegeben sein (nicht das Test-Projekt).

**Build-Warnung SEC001 kommt immer noch nach `set`:**
- VS muss die Solution neu laden (`Datei → Close Solution` → reopen). User-Secrets werden zur Build-Zeit evaluiert.

**MySQL-Connection schlägt fehl obwohl User-Secrets gesetzt sind:**
- Bist du im Heimnetz? Dev-DB läuft auf `192.168.100.51` (Heimnetz).
- VPN aktiv? Manche VPNs blocken lokale Subnetze.
- Teste: `Test-NetConnection 192.168.100.51 -Port 3306` (PowerShell).
