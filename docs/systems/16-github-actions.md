***REMOVED*** 16 · GitHub Actions (CI/CD)

***REMOVED******REMOVED*** Was es ist
Automatisierte Workflows in `.github/workflows/`, die bei jedem Push/PR
- bauen,
- Tests ausführen,
- ggf. Artefakte / Deployments erzeugen.

***REMOVED******REMOVED*** Typische Jobs

***REMOVED******REMOVED******REMOVED*** Build & Test
- `actions/checkout@v4`
- `actions/setup-dotnet@v4` mit `.NET 10`
- `dotnet restore`
- `dotnet build --configuration Release --no-restore`
- `dotnet test --no-build --verbosity normal`

***REMOVED******REMOVED******REMOVED*** Publish
- `dotnet publish <OWNER_HANDLE>-ERC/<OWNER_HANDLE>-ERC.csproj -c Release -o ./publish`
- Upload als Artefakt oder Deploy-Schritt (z. B. zu eigenem Server / Azure / FTP).

***REMOVED******REMOVED******REMOVED*** Branch-Strategie
- `master` baut und testet.
- Feature-Branches öffnen PRs gegen `master`.

***REMOVED******REMOVED*** Erweitern
- Code-Coverage hochladen (`reportgenerator` + `actions/upload-artifact`).
- Lint/Format-Check via `dotnet format --verify-no-changes`.
- Auto-Deploy nur bei Tags `v*`.

***REMOVED******REMOVED*** Hinweise
- Geheimnisse (DB, Discord) **nicht** ins Repo legen, sondern als
  GitHub-Actions-Secrets pflegen und im Workflow per
  `${{ secrets.MY_SECRET }}` injizieren.
