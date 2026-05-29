***REMOVED*** 06 · Driver Rivalries

***REMOVED******REMOVED*** Was es ist
Auf jeder Fahrer-Profilseite werden die häufigsten **Direktduelle** in der
gleichen Liga angezeigt – mit Win/Loss/Draw, Win-Quote-Bar und kleiner
Sparkline der letzten Duelle.

***REMOVED******REMOVED*** Wie es funktioniert

***REMOVED******REMOVED******REMOVED*** Helper
`Helpers/DriverRivalryHelper.cs`
- Record `RivalrySummary(Rival, RivalTeam, RacesTogether, Wins, Losses, Draws, Recent)`.
- `Compute(League league, string driver, int top = 5)`:
  - Iteriert alle Rennen der Liga
  - Findet den eigenen Finish (>0)
  - Vergleicht Position 1:1 mit jedem anderen Finisher
  - `Recent` enthält die letzten 8 Ergebnisse als `"W"`/`"L"`/`"D"`

***REMOVED******REMOVED******REMOVED*** Controller
`HomeController.DriverDetail` setzt:
```csharp
vm.Rivalries = DriverRivalryHelper
    .Compute(league, standing.Driver)
    .ToList();
```

***REMOVED******REMOVED******REMOVED*** View
`Views/Home/DriverDetail.cshtml`
- Karten-Grid (`col-md-6 col-xl-4`)
- Border-Akzent in Rival-Team-Farbe
- Win-Quote-Bar `.rivalry-bar`
- Sparkline `.rivalry-spark` mit farbigen Dots:
  - grün = Sieg, rot = Niederlage, grau = Gleichstand

***REMOVED******REMOVED******REMOVED*** CSS
`wwwroot/css/site/11-unique-addons.css`:
- `.rivalry-card`, `.rivalry-bar`, `.rivalry-spark*`

***REMOVED******REMOVED*** Erweitern
- Mehr/weniger Rivalen: Parameter `top` ändern.
- Andere Sortierung: `OrderByDescending(...)` im Helper anpassen.
- Reine Saison-Rivalen: Vor `foreach` `league.Races` filtern.
