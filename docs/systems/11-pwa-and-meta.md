***REMOVED*** 11 · PWA & Meta-Tags

***REMOVED******REMOVED*** Was es ist
- Die App ist als **PWA** installierbar (Standalone-Mode auf Mobile/Desktop).
- Alle Seiten haben **Open Graph** und **Twitter Cards**, sodass sie in
  Discord/Twitter/WhatsApp mit Vorschau-Bild geteilt werden können.

***REMOVED******REMOVED*** Komponenten

***REMOVED******REMOVED******REMOVED*** Manifest
`wwwroot/site.webmanifest`
- `name`, `short_name`, `theme_color`, `background_color`
- Icon-Liste inkl. ERC-Logo (512×512)

***REMOVED******REMOVED******REMOVED*** Layout-Head
`Views/Shared/_Layout.cshtml`:
- `<link rel="manifest" href="~/site.webmanifest" />`
- Apple-Webapp-Tags (`apple-mobile-web-app-*`)
- Open-Graph: `og:title`, `og:description`, `og:url`, `og:image`, `og:site_name`
- Twitter: `twitter:card=summary_large_image`, Title/Description/Image

***REMOVED******REMOVED******REMOVED*** Pro-Seiten-Override
Jede View kann eigene Werte setzen:
```csharp
ViewData["OgTitle"] = "Driver: Max Mustermann";
ViewData["OgDescription"] = "Fahrerprofil mit Achievements und Rivalitäten.";
ViewData["OgImage"] = "/images/driver-cards/max.png";
```

Wenn nicht gesetzt, fällt das Layout auf:
- Title = `"<ViewData[Title]> · <OWNER_HANDLE> ERC"`
- Description = ERC-Default-Slogan
- Image = `/images/ERCLogoHighRes1.png`

***REMOVED******REMOVED*** Erweitern
- Pro Liga eigenes OG-Bild: in der jeweiligen View `ViewData["OgImage"]` setzen.
- Service Worker (Offline) später ergänzen (`sw.js` + Registration im Layout).
