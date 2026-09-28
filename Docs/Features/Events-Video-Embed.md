---
datum: 2026-09-25
tags: [entwicklung, feature, events, highlights, video, twitch, youtube]
status: erledigt
aktualisiert: 2026-09-25
---

# Video-Einbettung in Events und Highlights (YouTube + Twitch)

Ausgangsfrage: „kriegen wir bei den events eig auch ein twitch clip oder youtube videos
mit rein als video?" — YouTube konnten Events schon, Twitch war **aktiv abgewiesen**.

## Vorher

- `RealLifeEvent.YouTubeUrl` (512) existierte samt Admin-Feld und wurde auf der Detailseite
  als 16:9-iframe gezeigt.
- `AdminCommunityController.SaveRealLifeEvent` wies alles außer YouTube ab
  („Bitte nur YouTube-Links eintragen."), Twitch-Clips waren also nicht eintragbar.
- Die URL-Erkennung lag **doppelt** vor: inline in `Views/Community/EventDetail.cshtml` und
  als lokale Funktion `ToEmbedUrl` in `Views/Community/Events.cshtml` — beide kannten nur
  `youtu.be`, `youtube.com/watch?v=` und `/embed/`.
- `RaceHighlightClip.Url` (Community-Einsendungen, Platzhalter im Formular war schon
  „Twitch Clip / YouTube URL") wurde nur als Link „Clip öffnen" ausgeliefert — nichts
  wurde eingebettet.

## Jetzt

Eine Quelle für die Regel: `Helpers/VideoEmbedHelper.cs`.

| Funktion | Zweck |
| --- | --- |
| `Resolve(url, requestHost)` | fertige iframe-URL oder `null`, wenn nicht einbettbar |
| `DetectPlatform(url)` | Anbieter erkannt? (auch für nicht einbettbare Links — Admin-Validierung) |
| `DisplayName(platform)` | „YouTube" / „Twitch" für Labels und `title` |
| `SafeLinkOrNull(url)` | der Link, wenn er ein http(s)-Link ist — für alles, was ihn nur **verlinkt** |
| `FrameSrcDirective()` | Wert der CSP-Direktive `frame-src` (Quelle für `Program.cs`) |
| `NormalizedHostOrNull(host)` | Host-Header auf einen Hostnamen prüfen/normalisieren |

Erkannte Formen:

| Eingabe | Embed |
| --- | --- |
| `youtube.com/watch?v=<id>` | `youtube.com/embed/<id>` |
| `youtu.be/<id>` | `youtube.com/embed/<id>` |
| `youtube.com/shorts/<id>`, `/live/<id>`, `/embed/<id>` | `youtube.com/embed/<id>` |
| `clips.twitch.tv/<slug>` | `clips.twitch.tv/embed?clip=<slug>&parent=…` |
| `twitch.tv/<kanal>/clip/<slug>` | `clips.twitch.tv/embed?clip=<slug>&parent=…` |
| `twitch.tv/videos/<nummer>` | `player.twitch.tv/?video=<nummer>&parent=…` |
| `player.twitch.tv/?clip=…` / `?video=…` | normalisiert auf die Formen oben |

**Drei Regeln, die die Tests festhalten:**

1. **Nicht einbettbar heißt nicht unsichtbar.** Kanal-URLs, Playlists, Vimeo und Freitext
   liefern `null`; die Views zeigen dann einen normalen Link statt eines leeren Players
   (Detailseite **und** Timeline: „Video auf YouTube ansehen"). Eingebettet wird nur, was als
   Video erkannt wurde — der Link kommt aus einer Admin-Eingabe bzw. aus einer
   Community-Einsendung.
2. **Twitch braucht `parent`.** Der Player zeigt sonst nur „Whoops, something went wrong".
   Der Wert ist der Host der laufenden Anfrage (`Context.Request.Host.Host`, zusätzlich
   `localhost` für die Entwicklung) und wird deshalb pro Request gebildet, nicht gespeichert.
   Er stammt aus dem Host-Header und ist damit angreifbar: alles außer einem Hostnamen
   (Buchstaben, Ziffern, `.`, `-`) wird verworfen, Schreibweise und abschließender Punkt
   werden normalisiert.
3. **Nur echte IDs werden eingebettet.** In Pfad-Position (`youtu.be/<id>`,
   `/embed|shorts|live/<id>`) muss die YouTube-ID die echte Form haben (genau 11 Zeichen);
   `youtu.be/watch` und `/embed/videoseries` liefern `null` — sonst zeigte die View einen
   Player mit „Video unavailable". `DetectPlatform` erkennt sie weiterhin, sie werden also
   als Link ausgeliefert.

In der DB steht **immer die Original-URL**, nie die Embed-URL — sonst wäre später nicht mehr
erkennbar, was eingetragen wurde.

## Entscheidung: kein Schema-Umbau

Die Spalte heißt weiter `YouTubeUrl`, enthält aber jeden Video-Link. Das war eine bewusste
Entscheidung des Betreibers: eine Umbenennung (oder eine neue Spalte) braucht eine Migration
auf Prod, und die darf hier niemand automatisch einspielen ([[project-migration-safety-aug2026]]).
Der Name ist damit historisch; im Code trägt ein Kommentar an jeder Fundstelle die Erklärung
(`Views/Admin/Events.cshtml`, `AdminCommunityController.SaveRealLifeEvent`,
`Views/Community/EventDetail.cshtml`).

**Formular-Feld und Controller-Parameter heißen dagegen `videoUrl`** — sie sind Vertrag
innerhalb der Anwendung, nicht Schema, und `youTubeUrl` wäre mit Twitch-Inhalt irreführend.

Falls der Name später doch weg soll:
```sql
-- Rollback-Punkt: Spaltenname vorher/nachher notieren
ALTER TABLE RealLifeEvents CHANGE COLUMN YouTubeUrl VideoUrl VARCHAR(512) NULL;
```
danach die Property in `Models/RealLifeEvent.cs` umbenennen und die Migration einspielen —
**nur durch den Betreiber** (MIGRATION-SAFETY.md).

## Betroffene Stellen

| Datei | Änderung |
| --- | --- |
| `Helpers/VideoEmbedHelper.cs` | neu — die einzige Quelle |
| `Views/Community/EventDetail.cshtml` | inline-Parser ersetzt, Link-Fallback ergänzt |
| `Views/Community/Events.cshtml` | lokale Funktion `ToEmbedUrl` entfernt, Link-Fallback ergänzt |
| `Views/Community/Highlights.cshtml` | Clips werden eingebettet statt nur verlinkt |
| `Controllers/AdminCommunityController.cs` | Validierung über `DetectPlatform`, Parameter `videoUrl`, Längenguard |
| `Controllers/CommunityController.cs` | `SubmitHighlight` nimmt nur noch http(s)-Links an |
| `Views/Admin/Events.cshtml` | Feld „Video-Link", Anbieter-Icon (YouTube rot / Twitch violett) |
| `Program.cs` | `frame-src` kommt jetzt aus `VideoEmbedHelper.FrameSrcDirective()` |
| `Views/Stats/Erdi10.cshtml` | Twitch-`parent` über `NormalizedHostOrNull` statt rohem Host-Header |

## Die CSP ist die zweite Hälfte des Features

`clips.twitch.tv` fehlte in der `frame-src`-Direktive: der Helper baut **jedes** Twitch-Clip-Embed
auf genau diesem Host, der Browser verwarf jedes davon still — leerer Player, kein Fehler im
App-Log. Die Direktive steht jetzt in `VideoEmbedHelper.FrameSrcDirective()` (statt direkt in
`Program.cs`), und `Resolve_embedHostIsAllowedByTheFrameSrcCsp` prüft jeden Host, den der Helper
erzeugen kann, gegen genau diese Liste. Helper und CSP können damit nicht mehr auseinanderlaufen.

## Sicherheitsfunde aus dem Review

Beide waren **vorbestehend**, nicht durch dieses Feature entstanden — der Review hat sie aber
an genau dieser Vertrauensgrenze gefunden, deshalb hier dokumentiert:

- **Stored XSS über `RaceHighlightClip.Url`** (hoch): eingesendete Links wurden ungeprüft als
  `href` ausgegeben („Clip öffnen"), und Einträge sind sofort öffentlich (`IsApproved = true`).
  Razor encodiert Attributwerte, blockt aber kein `javascript:`-Schema. Behoben an der Quelle
  (`SubmitHighlight`, `SaveHighlightClip` → nur http(s)) **und** am Sink
  (`Highlights.cshtml` → `SafeLinkOrNull`), damit auch Alteinträge in der DB unschädlich sind.
- **Roher `Location`-Wert im Event-Hero** (mittel): der Lead wird mit `Html.Raw` ausgegeben
  (Icons), der Ortsname floss unkodiert hinein → `HtmlEncode`.
- **Überlanger Video-Link** (mittel): ein POST > 512 Zeichen lief in einen MySQL-Fehler (500)
  statt in die freundliche Meldung; das `maxlength` im Formular gilt nur im Browser. Jetzt
  `MaxVideoUrlLength` analog `MaxPenaltyReasonLength`.

## Tests

`tests/Erdi-ERC.Tests/Helpers/VideoEmbedHelperTests.cs` — 85 Fälle, darunter bewusst die
unangenehmen: Lookalike-Hosts (`evilyoutube.com`, `twitch.tv.evil.example`),
`javascript:`/`ftp:`/schema-lose Eingaben, Attribut-Injection im Host-Header, leere IDs,
Playlist-Platzhalter `videoseries` (11 Zeichen — sieht wie eine ID aus) und der Abgleich
Helper ↔ CSP. Eine echte Subdomain (`m.twitch.tv`, `clips.twitch.tv`) bleibt erlaubt — sie
liegt unter Kontrolle des Anbieters.

Dazu `tests/Erdi-ERC.Tests/Controllers/CommunityHighlightSubmissionTests.cs` (4 Fälle):
`SubmitHighlight` speichert bei `javascript:`/`data:`/Freitext nichts und setzt die
TempData-Meldung; ein gültiger Clip-Link landet in der DB.

Gesamtlauf: 543/543 grün (vorher 454, ohne das Feature 505).

## Offen

- Events, die vorher einen YouTube-Link hatten, funktionieren unverändert weiter.
- Für Twitch-Embeds gilt dieselbe Datenschutz-Lage wie bisher für YouTube: der Player lädt
  beim Seitenaufruf direkt und kann Dritt-Cookies setzen (`Views/Home/Privacy.cshtml` nennt
  Twitch schon). Eine Klick-zum-Laden-Lösung ist bewusst nicht eingebaut — sie wäre eine
  eigene Entscheidung und würde auch die bestehenden YouTube-Einbettungen betreffen.
- Der Längenguard (`MaxVideoUrlLength`) und die neue Timeline-Link-Ausgabe sind **nicht**
  durch Controller-Tests abgedeckt; geprüft ist die Validierungslogik selbst
  (`DetectPlatform`) und die Views kompilieren als Teil des Testbuilds.
