---
datum: 2026-10-20
tags: [entwicklung, design, feature, streaming, dotd]
status: entwurf
---

# Driver of the Day — Chat-Voting pro Liga

## Ziel

Die ERC-Website bekommt ein Driver-of-the-Day-Voting, das komplett über die bestehende
Webseite läuft:

- Der Streamer öffnet im **Admin-Dashboard** eine Wahl für eine Liga.
- Zuschauer stimmen im **Twitch-Chat** mit `!vote <name>` ab.
- Eine **Browser-Source-Seite** auf der Webseite zeigt die Wahl live im Stream.
- Nach dem Schließen wird der Sieger aufgelöst und in der Datenbank gespeichert.

Es wird **kein neuer Dienst** gebaut. Alles läuft in der bestehenden ASP.NET-Anwendung
auf `erdi-ws` (LXC 1000). Keine neuen NuGet-Pakete.

## Entscheidungen aus dem Gespräch

| Thema | Entscheidung |
|---|---|
| Start/Ende | Kleine Fläche im Admin-Dashboard, kein Start-Befehl im Chat |
| Fahrerpool | **Alle** Fahrer der gewählten Liga, keine Teilnehmer-Auswahl |
| Gleichzeitige Wahlen | Genau **eine** offene Wahl zur Zeit |
| Stimmrecht | Jeder Twitch-Nutzer eine Stimme; Umentscheiden erlaubt; nicht sub-only |
| Balken live | Ja, aber **ohne Zahlen** und mit ständigem Random-Vor-und-Zurück |
| Auflösung | Ja, nach dem Schließen |
| Chat-Antworten | Nein (anonymes IRC kann nur lesen) |

## Nicht-Ziele

- Kein Vote-Start per Chat-Befehl.
- Keine Moderation (Sub-only, Follower-only, Captcha).
- Keine Chat-Antwort des Bots („Vote registriert"). Dafür wäre ein OAuth-Token nötig —
  mögliche spätere Erweiterung, nicht Teil dieses Entwurfs.
- Keine öffentliche Ergebnisseite. Die Historie steht im Admin; eine öffentliche
  Anzeige kann später aus denselben Daten gebaut werden.
- Keine Änderung an Rennergebnis-, Punkte- oder Meisterschaftslogik.

## Wiederverwendung im Repo

| Bedarf | Vorhanden | Ort |
|---|---|---|
| „ERC \|" aus Namen entfernen | `DriverNameHelper.Display()` | `Helpers/DriverNameHelper.cs` |
| Tippfehler-Toleranz | `DriverProfileService.Levenshtein()` (private) + `DriverMatchingOptions` | `Services/DriverProfileService.cs`, `Options/DriverMatchingOptions.cs` |
| Twitch-Kanal | `ApplicationOptions.TwitchChannel` (Default `erdi10`) | `Options/ApplicationOptions.cs` |
| Admin-Auth | Policy `Admin.Community.Hub` | `Program.cs` |
| Admin-Karten-Layout | `adm-kpi-card` / Dashboard-Karten | `Views/Admin/Index.cshtml` |
| Design-Sprache | „Race Control", Tokens aus `wwwroot/css/site/` | `Docs/design-state.md` |

Die Levenshtein-Methode ist privat. Sie wird nach `Helpers/DriverNameHelper.cs` oder
einen kleinen `Helpers/DriverNameMatcher.cs` verschoben und von beiden Stellen genutzt —
keine zweite Implementierung.

## Architektur

```
Twitch-Chat ──IRC/WebSocket──▶ TwitchChatReaderService (BackgroundService)
                                      │  !vote <name>
                                      ▼
                              DotdVoteService (Singleton, State + DB)
                                 │                 │
                 Admin-UI ◀──────┤                 ├──────▶ Overlay-Seite
              (exakte Zahlen)    │                 │        (JSON-Polling, 2 s)
                                 ▼                 ▼
                            MySQL: DotdVotes, DotdBallots
```

- **`DotdVoteService`** (Singleton): hält die aktuell offene Wahl im Speicher (Id, Liga,
  Startzeit) und schreibt Stimmen. Beim App-Start wird eine offene Wahl aus der DB
  geladen, damit ein Neustart mitten in der Wahl nichts verliert.
- **`TwitchChatReaderService`** (`BackgroundService`): verbindet sich anonym mit
  `wss://irc-ws.chat.twitch.tv:443`, tritt `#erdi10` bei, antwortet auf `PING` mit
  `PONG`, verbindet bei Abbruch mit 5 s Backoff neu. Pro Nachricht wird nur geparst,
  wenn eine Wahl offen ist.
- **Overlay** ist eine normale Razor-Seite ohne Login; der Streamer trägt die URL als
  Browser-Source ein. Kein SignalR nötig — die Seite fragt alle 2 Sekunden nach.

## Datenmodell

Zwei neue Tabellen, Migration **nur auf `erditest`** (siehe `Docs/MIGRATION-SAFETY.md`).

```csharp
public class DotdVote
{
    public int Id { get; set; }
    public string LeagueId { get; set; } = "";
    public string LeagueName { get; set; } = "";   // Snapshot für die Historie
    public DateTime StartedAt { get; set; }         // UTC
    public DateTime? ClosedAt { get; set; }
    public string? WinnerDriver { get; set; }       // null = geschlossen ohne gültige Stimme
    public int? WinnerVotes { get; set; }
    public int? TotalVotes { get; set; }
    /// <summary>Endverteilung (max. 8 Einträge) als JSON-Snapshot für Overlay und Historie.
    /// Wird beim Schließen geschrieben, damit die Ballots danach gelöscht werden können.</summary>
    public string? FinalSharesJson { get; set; }
    public string? StartedByDiscordId { get; set; } // Audit
}

public class DotdBallot
{
    public int Id { get; set; }
    public int VoteId { get; set; }
    [MaxLength(32)] public string VoterKey { get; set; } = ""; // Twitch user-id, sonst Login
    [MaxLength(160)] public string Driver { get; set; } = "";  // Name zum Zeitpunkt der Stimme
    public DateTime VotedAt { get; set; }
}
```

- Unique-Index `(VoteId, VoterKey)` — eine Stimme pro Nutzer, Umentscheiden per Update.
- **Kein `VoterLogin`**: Der Admin braucht keine Auswertung pro Nutzer. Gespeichert wird
  nur die pseudonyme Twitch-User-Id für die Ein-Stimme-Regel.
- **Ballots werden beim Schließen gelöscht.** Vorher werden `WinnerDriver`,
  `WinnerVotes`, `TotalVotes` und `FinalSharesJson` auf der Vote-Zeile gespeichert.
  Danach bleibt keine dauerhafte Zuordnung „Twitch-Nutzer → Fahrer" übrig.
- Fahrerpool: `DriverStandings.Where(s => s.LeagueId == vote.LeagueId)` — inklusive
  Reservisten. Kein Saisonfilter, weil die Standings-Zeilen bereits den aktiven Kader
  der Liga abbilden.
- **Der Kader wird beim Öffnen der Wahl eingefroren** (in-memory). Wird ein Fahrer
  während der Wahl umbenannt, ändert das weder Kandidatenliste noch bereits abgegebene
  Stimmen. Nach einem Neustart mitten in der Wahl wird die Liste aus der DB neu
  aufgebaut — dann gilt der aktuelle Stand.

## Chat-Befehl und Namensabgleich

**Syntax:** `!vote <name>` (Alias `!dotd <name>`). Groß-/Kleinschreibung egal.

**Normalisierung** (beide Seiten):

1. Trimmen, Kleinschreibung.
2. Deutsches Transkribieren: `ä→ae, ö→oe, ü→ue, ß→ss`.
3. Diakritika entfernen (`é→e`), Satzzeichen und Unterstriche zu Leerzeichen,
   Mehrfach-Leerzeichen zusammenfassen.
4. Führendes `erc |` entfernen (`DriverNameHelper`), danach trimmen.

**Kandidaten** je Fahrer: normalisierter voller Name und normalisierter Anzeigename
(ohne Clantag).

**Treffer in Stufen**, erste nicht-leere Stufe gewinnt:

1. **Exakt** auf vollen Namen oder Anzeigenamen → sicher.
2. **Exakt auf einen Namensbestandteil** (z. B. `wiener` trifft `ERC | Max Wiener`) →
   sicher, wenn genau ein Fahrer passt; sonst mehrdeutig.
3. **Teilwort** (Anzeigename enthält die Eingabe oder umgekehrt, Eingabe ≥ 3 Zeichen)
   → sicher, wenn genau ein Fahrer passt.
4. **Levenshtein** ≤ 2 auf dem Anzeigenamen oder ≤ 1 auf einem Namensbestandteil →
   sicher, wenn genau ein Fahrer passt.

Mehrdeutige oder unbekannte Namen werden **still verworfen** (kein Chat-Feedback ohne
Token). Im Admin zeigt eine kleine Liste der letzten 20 verworfenen Versuche, welcher
Chat-Text nicht zugeordnet werden konnte — das ist die einzige Diagnose.

Beispiele:

| Chat | Fahrer | Ergebnis |
|---|---|---|
| `!vote wiener` | `ERC \| Wiener` | ✅ |
| `!vote Wiener` | `ERC \| Wiener` | ✅ |
| `!vote wienr` | `ERC \| Wiener` | ✅ (Levenshtein 1) |
| `!vote müller` | `ERC \| Mueller` | ✅ (Transkription) |
| `!vote max` | `ERC \| Max Wiener`, `ERC \| Max Berger` | ❌ mehrdeutig |

## Overlay-Seite

**Route:** `GET https://erdi-erc.de/overlay/dotd` — öffentlich, `noindex`, ohne Layout,
eigener schlanker HTML-Rahmen, Einbindung der bestehenden Design-Tokens. Kein Login,
kein Cookie-Banner. Diese URL wird in Streamlabs als Browserquelle eingetragen.

**Ablauf:**

- **Keine offene Wahl:** dezenter Hinweis „Keine Abstimmung aktiv" (oder komplett
  transparent, per Query `?leer=aus`).
- **Wahl offen:** Liga-Name, Überschrift „Driver of the Day", Zeile
  `Vote im Chat: !vote <name>`, darunter Balken.
- **Wahl geschlossen:** Balken laufen auf die echten Endwerte, Sieger wird
  hervorgehoben (Name + Prozent). Diese Auflösung bleibt stehen, auch nach einem
  Neuladen oder App-Neustart, bis der Streamer die nächste Wahl öffnet.
- **Geschlossen ohne gültige Stimme:** „Keine Stimmen abgegeben".

**Balken-Verhalten:**

- Höchstens **8 Balken**: die Fahrer mit den meisten echten Stimmen. Die Auswahl ist
  dynamisch — ein Fahrer, der in die Top 8 rutscht, blendet sich ein. Bei Gleichstand
  an der Grenze entscheidet der Anzeigename alphabetisch.
- Feste Reihenfolge innerhalb der Anzeige (alphabetisch nach Anzeigename), damit nur
  die Längen wandern und das Layout nicht springt.
- Angezeigte Länge = echter Anteil + zufälliges Rauschen (± 4 Prozentpunkte), alle
  ~400 ms neu gezogen und weich nachgeführt. **Keine Zahl, kein Prozenttext, keine
  Platzierung.**
- Der Server liefert ebenfalls nur **gerundete Anteile in 5-%-Schritten** plus
  leichtes Rauschen — die exakten Stimmenzahlen verlassen den Server nicht.
- Nach dem Schließen: kein Rauschen mehr, Balken laufen auf die echten Endanteile.

**Daten-Endpunkt:** `GET /overlay/dotd/data` (JSON, `Cache-Control: no-store`):

```json
{
  "state": "open",
  "stale": false,
  "league": "Pro",
  "bars": [
    { "name": "Wiener", "share": 35 },
    { "name": "Mueller", "share": 20 }
  ],
  "winner": null
}
```

- `state`: `"open"` | `"closed"` | `"none"`.
- `stale: true`, wenn die DB gerade nicht erreichbar ist und der letzte bekannte
  Stand aus dem Speicher kommt.
- Bei `state: "closed"` zusätzlich `winner: { "name": "Wiener", "percent": 34 }`
  oder `winner: null` (keine gültige Stimme).
- Bei `state: "none"` leere `bars`.

## Admin-Fläche

**Route:** `/admin/driver-of-day`, Policy `Admin.Community.Hub` (kein neuer
Berechtigungsschlüssel — es ist eine Community-/Stream-Funktion).

**Inhalt:**

1. **Status-Karte**: offene Wahl mit Liga, Startzeit, Stimmenzahl; Buttons
   „Wahl schließen" und „Wahl verwerfen".
2. **Neue Wahl**: Liga-Dropdown (nicht archivierte Ligen) + „Wahl öffnen". Ist bereits
   eine Wahl offen, wird abgelehnt und die offene Wahl genannt.
3. **Live-Zahlen**: Tabelle aller Fahrer mit Stimmen (exakt, nur Admin), sortiert.
4. **Overlay-Link**: vollständige URL zum Kopieren:
   `https://erdi-erc.de/overlay/dotd` (Produktionsdomain, fest im Admin-View).
5. **Historie**: letzte 10 Wahlen mit Liga, Datum, Sieger, Stimmen.
6. **Nicht zugeordnet**: letzte 20 Chat-Versuche, die keinem Fahrer zugeordnet werden
   konnten (nur Diagnose, In-Memory).

Zusätzlich eine Karte im bestehenden Dashboard (`Views/Admin/Index.cshtml`) mit Link
auf die Seite und Anzeige, ob gerade eine Wahl offen ist.

**„Wahl verwerfen"** für eine versehentlich geöffnete Wahl: löscht Vote-Zeile und
Ballots hart, kein Historien-Eintrag. Wird als `DotdVoteVoid` auditiert.

**Aktionen** sind POST mit `ValidateAntiForgeryToken` und werden im bestehenden
`IAdminAuditService` protokolliert (`DotdVoteOpen`, `DotdVoteClose`, `DotdVoteVoid`).

## Fehlerfälle

| Fall | Verhalten |
|---|---|
| Twitch-Verbindung bricht ab | 5 s warten, neu verbinden; offene Wahl bleibt unberührt |
| App-Neustart während offener Wahl | offene Wahl wird beim Start aus DB geladen |
| Zwei Wahlen gleichzeitig öffnen | zweite Anfrage wird abgelehnt |
| Unbekannter/mehrdeutiger Name | still verworfen, im Admin-Diagnoselog sichtbar |
| Doppelter Vote desselben Nutzers | bestehende Stimme wird umgeschrieben |
| Overlay lädt, aber keine Wahl | „Keine Abstimmung aktiv" |
| Datenbank nicht erreichbar | Endpunkt liefert den letzten bekannten Stand aus dem Speicher mit `"stale": true`; Overlay bleibt stehen, Fehler wird geloggt |
| Gleiche Stimmenzahl bei Auflösung | Sieger alphabetisch nach Anzeigename (deterministisch) |
| Schließen ohne gültige Stimme | Vote wird geschlossen, `WinnerDriver = null`, Overlay zeigt „Keine Stimmen" |

## Tests

Neue Tests in `tests/Erdi-ERC.Tests`:

- **Normalisierung**: Clantag, Umlaute, Satzzeichen, Leerzeichen.
- **Matcher**: exakt, Bestandteil, Teilwort, Levenshtein, mehrdeutig, unbekannt.
- **Command-Parser**: `!vote`, `!dotd`, Groß-/Kleinschreibung, fehlender Name,
  normale Chat-Zeile ohne Befehl.
- **`DotdVoteService`**: öffnen, doppelt öffnen ablehnen, Vote schreiben, Umentscheiden,
  Vote auf geschlossene Wahl ablehnen, Sieger bei Gleichstand.
- **Share-Bucketing**: keine exakten Zahlen im JSON, 5-%-Schritte, max. 8 Balken.
- **IRC-Zeilen-Parser**: Tags (`user-id`, `display-name`), Login-Fallback, `PING`.

Die Overlay-Route wird **nicht** automatisiert gerendert: `SmokeTests.cs` hält
ausdrücklich fest, dass die App wegen der Migrationen in `Program.cs` nicht per
`WebApplicationFactory` startbar ist. Geprüft wird sie manuell im Browser und in
Streamlabs. Controller-/Service-Logik läuft über den vorhandenen
`SqliteTestContext`-Aufbau.

## Deploy

1. Migration lokal erzeugen, **nur auf `erditest`** anwenden.
2. Tests und Build grün.
3. Production: `systemctl stop erdi-erc` → Publish kopieren → `systemctl start erdi-erc`
   (nicht über den laufenden Dienst kopieren, siehe `Docs/Features/production-deploy.md`).
4. Migration auf `erdierc` erst nach Freigabe und Sicherung; Schema muss vor dem
   Restart stehen.
5. Der Chat-Leser verbindet sich **nur in Production**. Außerhalb von Production
   bleibt er aus, damit lokale Läufe und Staging (`erditest`) nicht am echten
   `#erdi10`-Chat hängen. Für einen lokalen End-to-End-Test gibt es einen
   Schalter `Dotd:ConnectOutsideProduction` (Default `false`). Der Kanal selbst
   kommt aus `ApplicationOptions.TwitchChannel`.

## Getroffene Detailentscheidungen (überprüfbar, leicht änderbar)

- **Kader wird beim Öffnen eingefroren.** Kein Snapshot in der DB; nach Neustart
  mitten in der Wahl gilt der aktuelle DB-Stand.
- **Ballots werden beim Schließen gelöscht**, nachdem die Endverteilung als JSON auf
  der Vote-Zeile gespeichert wurde. Keine dauerhaften Twitch-IDs.
- **Letzte geschlossene Wahl bleibt sichtbar**, bis die nächste geöffnet wird. Auch
  über Neuladen und Neustart hinweg. Kein automatisches Ausblenden nach Zeit.
- **Top 8 sind dynamisch**; bei Gleichstand an der Grenze entscheidet der Anzeigename.
- **„Wahl verwerfen"** löscht hart und ohne Historien-Eintrag.
- **Schließen ohne Stimmen** ist erlaubt und erzeugt einen Historien-Eintrag ohne Sieger.
- **Chat-Leser nur in Production**, Schalter `Dotd:ConnectOutsideProduction` für Tests.
- **Auflösung mit Prozent** beim Schließen. Falls keine Zahlen gewünscht sind, entfällt
  nur die Prozentzeile.
- **Reservisten** sind wählbar. Falls nicht gewünscht: eine Zeile im Kader-Filter.
- **Overlay-Optik**: nutzt die vorhandene Race-Control-Sprache, keine neue Marke.
  Feinlayout (Abstände, Typografie, Animation) wird bei der Umsetzung nach
  `Docs/design-state.md` festgelegt.
- **Bot-Antworten** sind bewusst nicht enthalten; dafür wäre ein Twitch-OAuth-Token
  samt Refresh-Logik nötig.
