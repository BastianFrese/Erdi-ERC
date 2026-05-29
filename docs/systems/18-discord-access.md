***REMOVED*** 18 · Discord-basierter Zugang

***REMOVED******REMOVED*** Was es ist
Die App nutzt **Discord-OAuth** für Login. Anhand der Discord-Rollen
des Users werden bestimmte Inhalte (z. B. detaillierte Setups) freigeschaltet.

***REMOVED******REMOVED*** Komponenten

***REMOVED******REMOVED******REMOVED*** Login
- `Controllers/AccountController.cs` startet den OAuth-Flow.
- Token-Tausch und Profil-Abruf passieren serverseitig.
- Resultierender ClaimsPrincipal enthält Discord-User-ID und Rollen.

***REMOVED******REMOVED******REMOVED*** Rollen-basierte Sichtbarkeit
- Razor-Views verwenden Helper/Tags wie `@if (User.IsInRole("Premium")) { ... }`.
- Server-Endpunkte sind zusätzlich mit `[Authorize(Roles="...")]` abgesichert –
  niemals nur via View-Hide.

***REMOVED******REMOVED******REMOVED*** Wichtig: Public-Texte
- In **öffentlichen** Texten wird **nicht** erklärt, welche Discord-Rolle
  was freischaltet (siehe `13-public-text-sanitization.md`).
- Stattdessen neutrale Formulierung: „Inhalte abhängig von deinem Zugang".

***REMOVED******REMOVED*** Erweitern
- Neue Rolle: in Discord anlegen, in der Auth-Konfiguration mappen,
  in Razor/Controller via `IsInRole` / `[Authorize]` absichern.
- Lokales Testen: Discord-Dev-App + redirect URL `https://localhost:.../signin-discord`.
