---
datum: 2026-11-10
tags: [entwicklung, feature, community, awards, voting, admin]
status: gebaut
aktualisiert: 2026-11-11
---

# Season Awards

Admins legen Awards an, die Community nominiert je Award und Liga einen Fahrer,
nach dem Schließen werden die Gewinner auf der öffentlichen Seite bekannt gegeben.

## Entscheidungen (mit Bastian geklärt)

| Frage | Entscheidung |
|---|---|
| Zwei Phasen (nominieren → abstimmen)? | **Nein.** Nominieren ist Abstimmen. Jeder User wählt pro Award und Liga genau einen Fahrer. |
| Wann ist das Voting vorbei? | Wenn ein **Admin es schließt**. Kein automatisches Ende, kein Datum. |
| Gelten Awards pro Liga? | **Ja.** Ein Award gilt für alle Ligen; jede Liga bekommt automatisch ihr eigenes Voting mit ihrem eigenen Kader. |
| Stimmenzahlen sichtbar? | **Erst nach dem Schließen.** Während des Votings bewusst versteckt. |
| Wer darf wählen? | Jeder eingeloggte Discord-User, in jeder Liga — nicht nur Fahrer der Liga. |
| Gleichstand? | Bleibt Gleichstand: mehrere Gewinner, ausgewiesen mit „Gleichstand". Keine stille alphabetische Entscheidung. |

## Datenmodell

`Erdi-ERC/Models/SeasonAwards.cs`

- `SeasonAward` — Name, Beschreibung, Saison, `IsClosed`, `ClosedAt`, `ClosedByDiscordId`.
- `SeasonAwardNomination` — Award + Liga + Fahrername + Discord-Id des Wählers.
  Unique-Index `(AwardId, LeagueId, DiscordId)`: **eine Stimme pro User und Liga**,
  auch gegen zwei gleichzeitige Klicks. Umentscheiden überschreibt die Zeile.
- `SeasonAwardTally.Winners(...)` — reine Auswertung (getestet in
  `tests/Erdi-ERC.Tests/SeasonAwardTallyTests.cs`).

Migration: `20261110151947_AddSeasonAwards` (zwei neue Tabellen).

## Oberfläche

| Seite | Route | Datei |
|---|---|---|
| Öffentlich | `/awards` | `Views/Awards/Index.cshtml` |
| Admin | `AdminCommunity/SeasonAwards` | `Views/Admin/SeasonAwards.cshtml` |

- **Sammel-Abstimmung (11.11.2026):** Alle offenen Award-Karten sitzen in **einem**
  `<form id="award-batch-form">`. Man wählt je Karte seinen Fahrer und drückt unten
  einmal „Alle Stimmen speichern". Ein leeres Select bedeutet **unverändert** — so ist
  Teilabstimmen in Etappen möglich, und eine offene Karte löscht keine alte Stimme.
  Die Karten senden drei gleich lange Listen (`awardId`, `leagueId`, `driver`);
  ein einzelnes Tripel bindet genauso (alte Clients/Links bleiben gültig).
- Navigationslink „Season Awards" neben „Community-Votings".
- Neue Admin-Berechtigung `community.awards` (`AdminPermissions.CommunityAwards`),
  Policy `Admin.Community.Awards`. Superadmins haben sie automatisch; andere Admins
  müssen sie unter Admin → Berechtigungen bekommen.
- Admin sieht die Stimmenverteilung live; die öffentliche Seite nicht.
- Geschlossene Awards zeigen die Fahrerkarte des Gewinners (`/Profile/Card`,
  erst beim Scrollen geladen — die Abfrage ist teuer). Gleichstand = mehrere Karten.

## Stimme abgeben — Regeln im Controller

`Controllers/AwardsController.cs`, `POST /awards/nominate`:

1. `[Authorize]`, Antiforgery, Rate-Limit `forms`.
2. Leere `driver`-Felder werden übersprungen (kein Löschen einer bestehenden Stimme).
3. Award muss existieren und offen sein.
4. Liga muss existieren und darf nicht archiviert sein.
5. Fahrer muss im **Kader der Liga** stehen (`DriverStandings`, aktuelle Saison).
   Ein manipulierter POST mit fremdem Namen wird abgelehnt.
6. Upsert pro `(Award, Liga, User)` — Umentscheiden überschreibt.

Die Zeilen werden **einzeln** geprüft: eine ungültige Zeile fällt raus, die gültigen
werden trotzdem in **einem** `SaveChanges` gespeichert. Die Meldung nennt die Zahl
(„3 Stimmen gespeichert.") und bei Übersprungenen den ersten Grund.
Tests: `tests/Erdi-ERC.Tests/Controllers/AwardsControllerBatchVoteTests.cs`.

## Kader

Der Kader kommt aus `DriverStandings` der Liga (Stamm- und Ersatzfahrer, so wie die
Tabelle sie führt). Ligen ohne Standings bekommen keine Award-Karte — es gibt dann
niemanden zu nominieren.

## Deploy-Hinweis

Die Migration ist **auf der Prod-DB `erdierc` angewendet** (10.11.2026, auf Bastians
Wort). Ausgeführt wurde nur die DDL dieses einen Schritts (zwei `CREATE TABLE` +
Unique-Index + `__EFMigrationsHistory`-Zeile) — keine Sammel-`database update` gegen
Prod. Vorher Stand der History: `20261022173742_AddApplicationAge`; `AddSeasonAwards`
war die einzige offene Migration. Rollback-Referenz: die beiden Tabellen sind neu,
Rückweg = `DROP TABLE SeasonAwardNominations, SeasonAwards;` (enthält damals keine
Bestandsdaten).

Der **Code ist deployt** (10.11.2026, 16:31): Release-Publish, `systemctl stop`,
robocopy nach `T:\`, `systemctl start` — Reihenfolge laut
`Docs/Features/production-deploy.md` (ein Copy über den laufenden Dienst beendet ihn).
`app.env`, `appsettings.json` und `appsettings.Production.json` waren vorher/nachher
byte-identisch (SHA-1 verglichen). Live-Check: `/awards` 200, `/`, `/Races/AllRaces`,
`/Stats/EwigeListe`, `/Community/CommunityVotes` 200; null `[ERR]`-Zeilen im Log nach
dem Restart. Admin-Route leitet ohne Login auf Discord (302).

Noch offen: anderen Admins das Recht `community.awards` geben (Superadmin hat es).
`database update` gegen `erdierc` bleibt verboten (`Docs/MIGRATION-SAFETY.md`).

## Deploy der Sammel-Abstimmung (12.11.2026, 02:22)

Publish ohne Profil (`dotnet publish -c Release -r linux-x64 --self-contained false`),
dann `systemctl stop` → `robocopy /E` nach `T:\` (mit `/XF app.env appsettings.json
appsettings.Production.json`) → `systemctl start`. Keine Migration, kein DB-Eingriff.
`app.env` und beide `appsettings` vorher/nachher SHA-1-gleich. Live: `/awards`, `/`,
`/Races/AllRaces`, `/Community/CommunityVotes` je 200; null `[ERR]`-Zeilen; das neue
`<form id="award-batch-form">` steht im ausgelieferten HTML. Das Sammel-Formular selbst
ist nur für eingeloggte User sichtbar — **im Browser mit Login nicht gegengeprüft**.
Der Stand ist **nicht committet**.
