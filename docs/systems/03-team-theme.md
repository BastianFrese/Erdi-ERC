***REMOVED*** 03 · Team-Theme

***REMOVED******REMOVED*** Was es ist
Die Akzentfarbe der **gesamten Seite** richtet sich nach dem Team, dessen Fahrer
das letzte Rennen gewonnen hat. Davon profitieren u. a.:
- Brand-Glow im Logo
- aktive Navigations-Items
- Scroll-Progress-Bar
- Auto-Lackierung im Race-Start-Loader

***REMOVED******REMOVED*** Wie es funktioniert

1. `Helpers/F1TeamsHelper.cs` definiert Teams (Name, CssKey, Primary/Secondary, Driver-Liste).
2. `_Layout.cshtml` fragt das letzte Race-Ergebnis ab und mappt Fahrer → Team.
3. Body-Klasse wird gesetzt:
   ```html
   <body class="... team-redbull">
   ```
4. `wwwroot/css/site/11-unique-addons.css` enthält die Theme-Variablen pro Team:
   ```css
   body.team-redbull { --team-primary: ***REMOVED***1e41ff; --team-secondary: ***REMOVED***ffd200; }
   ```
5. Alle Komponenten greifen auf `var(--team-primary)` zurück.

***REMOVED******REMOVED*** Reservisten und Edge-Cases
- Falls der Sieger über `RaceReserveAssignments` für ein anderes Team gefahren ist,
  wird **dieses** Team als „Sieger-Team" verwendet, nicht das Stammteam.
- Wenn keine Daten vorhanden sind, fällt das Theme auf das ERC-Default (Rot) zurück.

***REMOVED******REMOVED*** Erweitern
- Neues Team: in `F1TeamsHelper.Teams` eintragen + CSS-Block in `11-unique-addons.css` ergänzen.
- Andere Akzent-Quelle (z. B. Konstrukteurs-Wertung): Logik in `_Layout.cshtml` anpassen.
