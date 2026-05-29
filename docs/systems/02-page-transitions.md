***REMOVED*** 02 · Page Transitions

***REMOVED******REMOVED*** Was es ist
Beim Navigieren zwischen Seiten faded die alte Seite weich aus und die neue
Seite faded mit leichtem Slide ein. Kombiniert mit der Race-Start-Ampel
ergibt das ein durchgehendes „Boxenstopp → Re-Start"-Gefühl.

***REMOVED******REMOVED*** Wie es funktioniert

- CSS-Klassen: `.page-transition`, `.page-enter`, `.page-leave` in
  `wwwroot/css/site/10-f1-quickwins.css`.
- Layout-Skript hängt Click-Listener an interne `<a>`-Links:
  - bei Klick → `.page-leave` aktiv, Navigation wird kurz verzögert.
  - nach `DOMContentLoaded` → `.page-enter` triggert das Einblenden.
- Externe Links und Downloads (`target=_blank`, `download`, andere Hosts) sind
  ausgenommen.

***REMOVED******REMOVED*** Wechselspiel mit der Ampel
- **Erste Session-Navigation:** Ampel zeigt sich → endet → Page-Enter spielt.
- **Folge-Navigation:** nur Page-Leave/Enter, keine Ampel.
- **Long-Load-Fall:** Wenn die nächste Seite zu lange braucht, springt die
  Ampel wieder rein und überdeckt die Lade-Lücke.

***REMOVED******REMOVED*** Erweitern
- Andere Übergangs-Kurven: `--page-trans-easing`, `--page-trans-dur` in CSS.
- Routen ausschließen: Im Layout-Skript Liste `excludePaths` ergänzen.
