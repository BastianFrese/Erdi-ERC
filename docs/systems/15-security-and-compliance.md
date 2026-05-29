***REMOVED*** 15 · Security & Compliance (CSP · Datenschutz · Impressum)

***REMOVED******REMOVED*** Was es ist
Die rechtlich/sicherheitstechnisch nötigen Bausteine für den Live-Betrieb
einer öffentlichen Website in DE/EU.

***REMOVED******REMOVED*** Komponenten

***REMOVED******REMOVED******REMOVED*** Content Security Policy (CSP)
- Wird als HTTP-Header (Middleware in `Program.cs` oder `Startup`) gesetzt.
- Erlaubt nur die explizit benötigten Quellen:
  - `self` für eigene Skripte/Styles
  - Google Fonts (`fonts.googleapis.com`, `fonts.gstatic.com`)
  - Bootstrap-Icons CDN (`cdn.jsdelivr.net`)
  - Twitch (für Live-Embed)
- `script-src` ohne `'unsafe-inline'` – Inline-Snippets sollten Hashes/Nonces nutzen
  oder in eigene Dateien gezogen werden.

***REMOVED******REMOVED******REMOVED*** Datenschutzerklärung
- Eigene Razor-Seite (z. B. `Views/Home/Datenschutz.cshtml`).
- Verweis im Footer.
- Inhalte: Hosting-Provider, eingesetzte Cookies/LocalStorage,
  Discord-OAuth, Logging, Twitch-Embeds.

***REMOVED******REMOVED******REMOVED*** Impressum
- Eigene Razor-Seite (z. B. `Views/Home/Impressum.cshtml`).
- Verweis im Footer.
- Inhalte gemäß § 5 TMG / DDG.

***REMOVED******REMOVED******REMOVED*** Cookies / LocalStorage
- Aktuell verwendet: `localStorage` für Player und ggf. Theme-State,
  `sessionStorage` für die Race-Lights-Sequenz.
- Keine Tracking-Cookies → kein Consent-Banner zwingend nötig,
  Hinweis in der Datenschutzerklärung trotzdem führen.

***REMOVED******REMOVED*** Erweitern
- CSP-Quellen erweitern: Header in der Middleware ergänzen, möglichst
  granular (nicht `*`).
- Bei zusätzlichen externen Diensten (Analytics, Maps, etc.):
  Datenschutzerklärung und CSP **gleichzeitig** anpassen.
