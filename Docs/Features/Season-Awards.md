---
datum: 2026-11-10
tags: [entwicklung, feature, community, awards, voting, admin]
status: gebaut
aktualisiert: 2026-11-10
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
2. Award muss existieren und offen sein.
3. Liga muss existieren und darf nicht archiviert sein.
4. Fahrer muss im **Kader der Liga** stehen (`DriverStandings`, aktuelle Saison).
   Ein manipulierte POST mit fremdem Namen wird abgelehnt.
5. Upsert pro `(Award, Liga, User)`.

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
