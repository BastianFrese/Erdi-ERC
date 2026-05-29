***REMOVED*** 09 · Keyboard-Shortcuts

***REMOVED******REMOVED*** Was es ist
Globale Tastatur-Shortcuts im GitHub-Stil (`g h` = go home).
Mit `?` öffnet sich ein Cheatsheet-Overlay.

***REMOVED******REMOVED*** Shortcuts

| Tasten | Ziel |
|---|---|
| `g h` | Racing Hub (`Home/<OWNER_HANDLE>10`) |
| `g r` | Results |
| `g a` | Alle Rennen |
| `g s` | Setups |
| `g e` | Events |
| `g t` | Twitch-Channel (neuer Tab) |
| `?`   | Cheatsheet öffnen/schließen |
| `Esc` | Cheatsheet schließen |

***REMOVED******REMOVED*** Wie es funktioniert
- Markup + JS in `Views/Shared/_Layout.cshtml` (Block „KEYBOARD SHORTCUTS").
- Eingaben in `<input>`, `<textarea>`, `<select>` und `contenteditable`
  werden ignoriert.
- `g` setzt einen `pendingG`-Status mit 1,2 s Timeout, danach erwartet
  das Skript einen zweiten Buchstaben.
- Routen werden serverseitig per `@Url.Action(...)` aufgelöst – kein
  hartkodierter Pfad.

***REMOVED******REMOVED*** Styling
- `.shortcut-overlay`, `.shortcut-row`, `.shortcut-keys`, `kbd`
  in `wwwroot/css/site/11-unique-addons.css`.

***REMOVED******REMOVED*** Erweitern
- Neuen Shortcut hinzufügen:
  1. Eintrag im `map`-Objekt im Layout-Skript ergänzen.
  2. Zeile im Cheatsheet-Markup einfügen.
