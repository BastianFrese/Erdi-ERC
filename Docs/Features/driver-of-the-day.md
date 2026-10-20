---
datum: 2026-10-20
tags: [entwicklung, feature, streaming, twitch]
status: erledigt
---

# Driver of the Day — Chat-Voting pro Liga

Zuschauer wählen im Twitch-Chat den Fahrer des Tages. Die Wahl wird im
Admin-Dashboard geöffnet und geschlossen; im Stream zeigt eine Browserquelle
die Zwischenstände als wackelnde Balken ohne Zahlen.

## Wege

| Was | Wo |
|---|---|
| Overlay (Streamlabs-Browserquelle) | `https://erdi-erc.de/overlay/dotd` |
| Overlay-Daten (JSON, Polling alle 2 s) | `https://erdi-erc.de/overlay/dotd/data` |
| Verwaltung | Admin-Dashboard → Community → Driver of the Day oder `/admin/driver-of-day` |
| Berechtigung | `Admin.Community.Hub` (kein eigener Schlüssel) |

Quelle: `Erdi-ERC/Controllers/OverlayController.cs`,
`Erdi-ERC/Controllers/AdminDriverOfDayController.cs`.

## Chat-Befehl

```
!vote wiener
!vote wienr
!vote müller
!dotd max berger
```

Groß-/Kleinschreibung ist egal. Ein Twitch-Nutzer hat eine Stimme; ein neuer
Befehl überschreibt die alte. Mehrdeutige Namen (`max` bei zwei „Max") werden
verworfen und im Admin unter „Nicht zugeordnete Chat-Versuche" angezeigt.

Der Abgleich normalisiert Clantag (`ERC |`), Umlaute, Diakritika und
Satzzeichen und probiert vier Stufen: exakter Name → exaktes Namenswort →
Teilwort → Levenshtein (Tippfehler). Quelle:
`Erdi-ERC/Helpers/DriverNameMatcher.cs`, Tests in
`tests/Erdi-ERC.Tests/Helpers/DriverNameMatcherTests.cs`.

## Fahrerpool

Alle Fahrer der gewählten Liga aus `DriverStandings` (inklusive Reservisten).
Der Kader wird beim Öffnen der Wahl eingefroren; eine Umbenennung mitten in der
Wahl ändert weder Kandidaten noch bereits abgegebene Stimmen. Nach einem
Neustart mitten in der Wahl wird der Kader aus dem aktuellen DB-Stand neu
aufgebaut. Quelle: `Erdi-ERC/Services/DotdVoteService.cs`.

## Overlay-Verhalten

- Höchstens acht Balken, die Fahrer mit den meisten echten Stimmen.
- Während der Wahl: Balkenlänge = gerundeter 5-%-Anteil plus Zufallsrauschen;
  die Seite bewegt sie alle 400 ms um bis zu ±4 Prozentpunkte. **Keine Zahlen,
  keine Platzierung.**
- Nach dem Schließen: Balken laufen auf die echten Endwerte, Sieger wird mit
  Prozent angezeigt.
- Das letzte Ergebnis bleibt sichtbar, bis die nächste Wahl geöffnet wird —
  auch nach einem Neuladen oder App-Neustart.
- Bei DB-Ausfall liefert der Endpunkt den letzten Stand mit `"stale": true`,
  damit das Overlay im Stream nicht leer wird.
- Bei jeder neuen Stimme erscheint für 3 s ein Toast mit dem **zuletzt
  wählenden** Nutzer (`@Name` + „Deine Stimme wurde gezählt"). Bewusst einer pro
  Poll — eine Einblendung pro Stimme wäre bei 120 Wählern Namens-Spam.

Quelle: `Erdi-ERC/Views/Overlay/DriverOfDay.cshtml`,
`Erdi-ERC/Helpers/DotdShareBuilder.cs`, `Erdi-ERC/Services/DotdVoteService.cs`.

## Daten

| Tabelle | Inhalt |
|---|---|
| `DotdVotes` | eine Wahl: Liga, Start/Ende (UTC), Sieger, Stimmen, JSON-Endverteilung |
| `DotdBallots` | eine Zeile pro Nutzer und Wahl (Twitch-User-Id + Fahrername) |

Beim Schließen werden `WinnerDriver`, `WinnerVotes`, `TotalVotes` und
`FinalSharesJson` gespeichert; danach werden die Ballots **gelöscht**. Es bleibt
keine dauerhafte Zuordnung „Twitch-Nutzer → Fahrer". „Wahl verwerfen" löscht
Wahl und Ballots hart und schreibt nur einen Audit-Eintrag (`DotdVoteVoid`).

## Chat-Leser

Ein `BackgroundService` liest den öffentlichen Chat über IRC-over-WebSocket
(`wss://irc-ws.chat.twitch.tv:443`, Kanal aus `Application:TwitchChannel`).
Ohne Bot-Token ist der Zugriff anonym und nur lesend; er antwortet dann nur auf
`PING` mit `PONG` und verbindet bei Abbruch nach 5 s neu.

### Chat-Bestätigung (optional)

Mit einem Bot-Token schreibt der Leser nach einer gezählten Stimme:

```
@Nutzer Vote für Wiener gezählt.
```

Konfiguration (in Production über Umgebungsvariablen, nie committen):

```json
"Dotd": {
  "BotUsername": "leutnant_kampfwiener",
  "BotOAuthToken": "oauth:…"
}
```

Der Token braucht die Scopes `chat:read` und `chat:edit`. Der Bot antwortet
höchstens alle 2 Sekunden (Twitch-Limit für normale Accounts); einzelne
Bestätigungen können daher ausfallen. Ohne Token bleibt es beim reinen Lesen.

Außerhalb von Production bleibt der Leser aus. Für einen lokalen Test:
`"Dotd": { "ConnectOutsideProduction": true }`. Quelle:
`Erdi-ERC/Services/TwitchChatReaderService.cs`, `Erdi-ERC/Options/DotdOptions.cs`.

> [!warning] Twitch beantwortet einen **mehrzeiligen Handshake-Block**
> (`CAP REQ` + `PASS` + `NICK` + `JOIN` in einer WebSocket-Nachricht) mit
> `CAP NAK`. Die Verbindung sieht dann gesund aus, liest aber nichts. Die
> Befehle werden deshalb einzeln mit kurzer Pause gesendet (Commit `d7404be`).

## Skalierung (120 Wähler)

Gemessen am 2026-10-20 gegen `erditest` über den Dev-Endpunkt
`GET /overlay/dotd/demo/burst?count=120`: **120 Stimmen in 318–441 ms**
(2,65–3,68 ms pro Stimme), 8 Balken, keine Zahlen im JSON. Die
Overlay-Abfrage nutzt den Index `(VoteId, VotedAt)`; die Stimmen laufen
sequenziell durch das Singleton-Gate. Test:
`Hundertzwanzig_Stimmen_werden_sequenziell_verarbeitet` (Suite 716/716).

Eine persönliche Chat-Bestätigung für alle 120 ist mit einem normalen
Twitch-Account nicht möglich (~20 Nachrichten/30 s); der Bot antwortet
höchstens alle 2 s. Deshalb zeigt das Overlay nur den letzten Wähler.

## Deploy

1. Migration `AddDriverOfDay` auf `erditest` anwenden.
2. Production: Dienst stoppen, Publish kopieren, Dienst starten — nicht über den
   laufenden Dienst kopieren (siehe [[production-deploy]]).
3. Migration auf `erdierc` erst nach Sicherung und Freigabe; Schema muss vor dem
   Restart stehen.
4. In Streamlabs eine Browserquelle mit `https://erdi-erc.de/overlay/dotd`
   anlegen (transparenter Hintergrund).

## Verwandt

- Spec: `Docs/superpowers/specs/2026-10-20-driver-of-the-day-design.md`
- Plan: `Docs/superpowers/plans/2026-10-20-driver-of-the-day.md`
- Zeitzonen: [[Zeitzonen-Konvention]]
- Deploy: [[production-deploy]]
