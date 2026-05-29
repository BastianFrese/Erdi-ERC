***REMOVED*** 04 · Live Race Mode

***REMOVED******REMOVED*** Was es ist
Wenn gerade ein offizieller Stream läuft, sieht der Besucher das sofort:
- **Topbar:** kleine pulsierende „Live"-Pill neben dem Logo
- **Hero-Bereich:** großer Live-Banner mit Streamtitel und Stream-Link

***REMOVED******REMOVED*** Wie es funktioniert

***REMOVED******REMOVED******REMOVED*** Datenquelle
- DbSet `StreamSchedules` (Model `<OWNER_HANDLE>_ERC.Models.StreamSchedule`).
- Felder: `Title`, `StartAt`, `DurationMinutes`, `Url`.
- Ein Stream gilt als „aktiv", wenn `StartAt <= now <= StartAt + Duration`.

***REMOVED******REMOVED******REMOVED*** Resolver im Layout
`Views/Shared/_Layout.cshtml`:
```csharp
var _now = DateTime.Now;
_activeStream = _layoutDb.StreamSchedules
    .Where(s => s.StartAt <= _now)
    .OrderByDescending(s => s.StartAt)
    .Take(20)
    .ToList()
    .FirstOrDefault(s =>
        s.StartAt.AddMinutes(s.DurationMinutes <= 0 ? 120 : s.DurationMinutes) >= _now);
ViewData["ActiveStream"] = _activeStream;
```

***REMOVED******REMOVED******REMOVED*** Anzeige
- **Topbar-Pill:** Markup direkt in `_Layout.cshtml`, Klassen `.live-race-pill*` in `11-unique-addons.css`.
- **Hero-Banner:** Partial `Views/Shared/_LiveRaceBanner.cshtml`, eingebunden mit
  `@await Html.PartialAsync("_LiveRaceBanner")` in `Index`, `<OWNER_HANDLE>10`, `Results`, `AllRaces`.

***REMOVED******REMOVED*** Erweitern
- Banner auf weiteren Seiten: einfach die Partial einbinden.
- Default-Stream-URL ändern: in der Partial unten (`https://twitch.tv/erdi10`).
- Eigene Quelle (z. B. Twitch-API statt Schedule-Tabelle): nur den Resolver
  im Layout ersetzen, alle Anzeige-Bausteine bleiben gleich.
