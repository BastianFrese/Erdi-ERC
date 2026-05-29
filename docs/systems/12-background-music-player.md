***REMOVED*** 12 · Background Music Player

***REMOVED******REMOVED*** Was es ist
Ein dezenter, persistenter Audio-Player im Layout. Spielt eine Track-Liste
ab, merkt sich Track, Position und Volume zwischen Seitenwechseln, und
kann eingeklappt werden.

***REMOVED******REMOVED*** Wie es funktioniert
- Markup + Skript in `Views/Shared/_Layout.cshtml`.
- Persistenz über `localStorage` (Track-Index, Time, Volume, collapsed-State).
- `<audio>`-Element bleibt durch das Layout am Leben → keine Neu-Initialisierung
  bei jeder Navigation.
- Buttons: Play/Pause, Prev, Next, Volume, Collapse.

***REMOVED******REMOVED*** Erweitern
- Neue Tracks: Track-Array im Skript ergänzen.
- Komplett deaktivieren: gesamten Player-Block + Skript entfernen oder
  in eine Partial verschieben und nur dort einbinden, wo gewünscht.

***REMOVED******REMOVED*** Hinweise
- Browser unterbinden Auto-Play ohne User-Interaktion.
  Der Player startet daher erst nach dem ersten Klick auf Play.
