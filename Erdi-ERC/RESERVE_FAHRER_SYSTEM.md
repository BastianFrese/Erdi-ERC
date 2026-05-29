***REMOVED*** Ersatzfahrer-System – Einfaches Admin-Handbuch

Dieses Dokument erklärt das Ersatzfahrer-System so, dass es auch ohne Technik-Wissen leicht zu verwalten ist.

---

***REMOVED******REMOVED*** 1) Kurzfassung

Wenn ein Ersatzfahrer fährt:

- Er bekommt seine Punkte **für sich selbst**.
- Seine Punkte zählen auch fürs **Team**.
- Die Zuordnung „Ersatz fährt für Hauptfahrer“ kann
  - als **Standard** hinterlegt werden,
  - oder **pro Rennen** überschrieben werden.

Alte Rennen bleiben dabei korrekt gespeichert (historisch richtig).

---

***REMOVED******REMOVED*** 2) Das System in 2 Ebenen

***REMOVED******REMOVED******REMOVED*** A) Standard-Zuordnung (Stammdaten)
Wird in der Fahrer-Verwaltung gepflegt:

- `Ersatz?` = Ja/Nein
- `Ersatz für` = Hauptfahrer

Das ist die normale Zuordnung für alle Rennen.

***REMOVED******REMOVED******REMOVED*** B) Renn-Zuordnung (Override, optional)
Beim Renn-Eintrag kann die Zuordnung für **dieses eine Rennen** geändert werden.

Wenn du nichts änderst, nutzt das System automatisch die Standard-Zuordnung.

---

***REMOVED******REMOVED*** 3) Wo mache ich was?

***REMOVED******REMOVED*** 3.1 Standard pflegen
Pfad:
1. **Admin**
2. Liga auswählen
3. **EditLeague**
4. Bereich **Bestenliste**

Dort pflegst du:
- Fahrer
- Team
- Ersatz-Status
- Standard-„Ersatz für"

***REMOVED******REMOVED*** 3.2 Pro Rennen anpassen
Pfad:
1. Liga öffnen
2. **Renn-Ergebnis eintragen**
3. Bereich **Ersatz-Zuordnung für dieses Rennen (optional)**

Dort kannst du für einzelne Ersatzfahrer die Zuordnung für genau dieses Rennen setzen.

***REMOVED******REMOVED*** 3.3 Nachträglich prüfen
In **EditLeague** in der Rennen-Tabelle gibt es die Spalte **Ersatz-Einsätze**.

Dort steht pro Rennen z. B.:
- `Fahrer2 → Fahrer1`
- `Fahrer2 → Fahrer3`

So ist immer sichtbar, wer in welchem Rennen für wen gefahren ist.

---

***REMOVED******REMOVED*** 4) Empfohlener Ablauf (einfach)

1. Vorher: Stammfahrer + Teams sauber pflegen.
2. Ersatzfahrer als Ersatz markieren, Standard-„Ersatz für“ setzen.
3. Rennen eintragen.
4. Nur wenn nötig: Renn-Override setzen.
5. Speichern.
6. Optional: In **Ersatz-Einsätze** kurz prüfen.

---

***REMOVED******REMOVED*** 5) Punkte-Logik

Punkteschema:

`25, 21, 18, 16, 14, 12, 10, 8, 7, 6, 5, 4, 3, 2, 1, danach 0`

Wichtig:
- Punkte gehen an den Fahrer, der gefahren ist (also auch Ersatzfahrer selbst).
- Teamwertung wird mit der gültigen Zuordnung des Rennens berechnet.

Reihenfolge der Zuordnung:
1. Renn-Override (falls gesetzt)
2. sonst Standard-Zuordnung aus Stammdaten

---

***REMOVED******REMOVED*** 6) Historie: Warum bleiben alte Rennen korrekt?

Weil pro Rennen gespeichert wird, welche Zuordnung tatsächlich genutzt wurde.

Das bedeutet:
- Wenn du später die Standard-Zuordnung änderst,
- ändern sich alte Rennen **nicht rückwirkend**.

---

***REMOVED******REMOVED*** 7) Eingebaute Fehlersicherheit

Das System blockiert automatisch:

- gleichen Fahrer mehrfach im gleichen Rennen,
- Fahrer gleichzeitig als Position + DNF,
- Ersatzfahrer und zugehörigen Hauptfahrer gleichzeitig im selben Rennen.

Diese Prüfungen laufen im UI und zusätzlich auf dem Server.

---

***REMOVED******REMOVED*** 8) Datenprüfung (Admin)

Button: **Ersatz-Daten prüfen**

Prüft u. a.:
- doppelte Fahrer,
- Ersatz ohne Hauptfahrer,
- ungültigen Hauptfahrer,
- Ersatz zeigt auf Ersatz,
- fehlendes Team.

Danach ggf. korrigieren und **Stats neu berechnen**.

---

***REMOVED******REMOVED*** 9) Sichtbarkeit im Frontend

Ersatz-Bezug wird angezeigt in:
- Results
- RaceDetail
- <OWNER_HANDLE>10 (vergangene Rennen, letzter Sieger, Theme)

Wenn ein Ersatzfahrer gewinnt, steht jetzt pro Rennen auch dabei, **für wen** er gefahren ist.

---

***REMOVED******REMOVED*** 10) FAQ

***REMOVED******REMOVED******REMOVED*** Kann ich Ersatzfahrer mitten in der Saison umhängen?
Ja. Nutze dafür den Renn-Override beim Renn-Eintrag.

***REMOVED******REMOVED******REMOVED*** Muss ich alte Rennen danach anpassen?
Nein. Die Historie bleibt korrekt.

***REMOVED******REMOVED******REMOVED*** Wann soll ich „Stats neu berechnen“ nutzen?
Nach größeren Änderungen an Fahrern/Zuordnungen oder wenn etwas unplausibel wirkt.

---

***REMOVED******REMOVED*** 11) Kurzfazit

Dieses Hybrid-System bietet:

- **Flexibilität** (pro Rennen anpassbar)
- **wenig Aufwand** (Standard läuft automatisch)
- **saubere Historie** (keine falschen Rückwirkungen)
- **korrekte Punkte und Teamwertung**
