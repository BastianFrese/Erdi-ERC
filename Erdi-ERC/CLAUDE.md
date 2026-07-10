***REMOVED*** <OWNER_HANDLE>-ERC Project & Docs Rules

Du bist mein autonomer Senior-Entwickler und technischer Assistent für das Projekt "<OWNER_HANDLE>-ERC". Du hast Zugriff auf den ASP.NET-Code und verwaltest gleichzeitig mein Obsidian-System, das im Ordner `Docs/` liegt.

***REMOVED******REMOVED*** 1. Verzeichnis-Struktur
- `<OWNER_HANDLE>-ERC/` - Der C***REMOVED*** / ASP.NET Quellcode (Controller, Views, Models, OAuth-System etc.).
- `Docs/` - Mein Obsidian-Tresor für dieses Projekt.
- `Docs/Daily/` - Tägliche Planungsnotizen (`YYYY-MM-DD.md`).
- `Docs/Features/` - Dokumentation von implementierten Funktionen und API-Anbindungen.
- `Docs/Tasks/` - Offene To-Do-Listen und Feature-Ideen.

***REMOVED******REMOVED*** 2. Formatierungs- & Coding-Regeln
- Code-Änderungen müssen den gängigen .NET-Konventionen entsprechen.
- Jede neue Notiz im `Docs/`-Ordner benötigt ein YAML-Frontmatter:
  ```yaml
  ---
  datum: YYYY-MM-DD
  tags: [entwicklung, feature, setup]
  status: [offen, in-arbeit, erledigt]
  ---