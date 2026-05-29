***REMOVED*** 10 · Browser-Tab Race Lights

***REMOVED******REMOVED*** Was es ist
Sobald der Browser-Tab in den Hintergrund geht, animiert der Title eine
Mini-Ampel-Sequenz mit Emojis:

```
🟥 Title → 🟥🟥 Title → 🟥🟥🟥 Title → … → 🟩 GO! Title
```

Wechselt der Nutzer zurück, wird der Originaltitel sofort wiederhergestellt.
Das macht den Tab in einer Tab-Reihe sofort wiedererkennbar.

***REMOVED******REMOVED*** Wie es funktioniert
- Vanilla-JS-Block am Ende von `Views/Shared/_Layout.cshtml`.
- Nutzt `document.visibilitychange`.
- Intervall 700 ms, Sequenz definiert als Array.
- Greift nicht auf den Server zu, läuft komplett clientseitig.

***REMOVED******REMOVED*** Erweitern
- Andere Sequenz: Array `seq` anpassen.
- Andere Geschwindigkeit: `setInterval(..., 700)` ändern.
- Auch im aktiven Tab animieren: `document.hidden` Check entfernen
  (nicht empfohlen – wirkt unruhig).
