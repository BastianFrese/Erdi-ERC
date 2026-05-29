***REMOVED*** 14 · Production-Konfiguration & Publish

***REMOVED******REMOVED*** Was es ist
Beim Veröffentlichen der App wird automatisch eine **`appsettings.Production.json`**
mitgenommen und beim Start verwendet, sobald `ASPNETCORE_ENVIRONMENT=Production`
gesetzt ist.

***REMOVED******REMOVED*** Aufbau

***REMOVED******REMOVED******REMOVED*** `appsettings.json`
Basiswerte für die Entwicklung – kann gefahrlos im Repo liegen.

***REMOVED******REMOVED******REMOVED*** `appsettings.Production.json`
Enthält die **konsolidierten** produktiven Einstellungen:
- ConnectionString zur Produktions-MySQL
- Discord-Konfiguration (Client/Secret/Bot-Token)
- Log-Level
- Mailing/SMTP (falls genutzt)
- Feature-Flags

> Diese Datei darf **keine** Test-/Dev-Geheimnisse mehr enthalten.

***REMOVED******REMOVED******REMOVED*** Publish
- Im `.csproj` ist die Datei mit `<Content Include="appsettings.Production.json" CopyToPublishDirectory="Always" />`
  (oder vergleichbarer Eintrag) hinterlegt, damit sie beim Publish kopiert wird.
- Alle anderen Umgebungs-`appsettings.*.json` bleiben Dev-Only.

***REMOVED******REMOVED*** Wahl der Umgebung
- ASP.NET Core liest `ASPNETCORE_ENVIRONMENT` als allererstes.
- `Production` → lädt `appsettings.json` + `appsettings.Production.json`.
- Werte in der Production-Datei **überschreiben** Werte aus `appsettings.json`.

***REMOVED******REMOVED*** Erweitern
- Neuer Konfig-Block: Klasse mit `IOptions<T>` definieren, in
  `appsettings.json` (Default) und `appsettings.Production.json`
  (echte Werte) eintragen.
- Geheimnisse besser zusätzlich per **User Secrets** (Dev) oder
  **Environment Variables / KeyVault** (Prod) injizieren.
