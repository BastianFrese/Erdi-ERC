---
datum: 2026-09-14
tags: [entwicklung, feature, giveaway, gewinnspiel, startseite, admin]
status: erledigt
---

# Giveaway-Infotafel auf der Startseite

## Kurzbeschreibung

Die Startseite zeigt eine **eigene Infotafel-Sektion** mit allen **aktuell stattfindenden Giveaways (Gewinnspielen)**. Verwaltet werden sie im **Admin-Dashboard (Community → Giveaways)** mit vollem CRUD (Anlegen, Bearbeiten via In-Page-Collapse, Löschen mit Confirm). Es existierte vorher kein Giveaway-Code — Tabelle komplett neu.

## Sichtbarkeits-Logik (User-Vorgabe: „stattfindend")

- **Zeitraum-basiert:** Ein Giveaway hat `StartAt` + `EndAt` (Deadline). Es erscheint auf der Startseite, sobald `StartAt ≤ jetzt ≤ EndAt` gilt, und verschwindet nach dem Ende **automatisch**.
- Filter in `HomeIndexDataService.BuildAsync` (`StartAt <= DateTime.UtcNow && EndAt >= DateTime.UtcNow`, sortiert nach `EndAt` asc = nächste Deadline zuerst).
- Kein manueller Schalter, kein Backfill — rein datumsgetrieben.
- Datenfluss über den bestehenden **60-s-`home-index:v1`-Cache**: neue/geänderte Giveaways erscheinen auf der Startseite ≤60 s nach dem Speichern (genau wie Admin-News).

## Felder

| Feld | Pflicht | Anmerkung |
|------|---------|-----------|
| `Title` | ja | max 160 |
| `Description` | nein | max 480 |
| `Prize` | nein | „Was verlost wird" — Kopfzeile der Karte |
| `StartAt` / `EndAt` | ja | `EndAt` muss &gt; `StartAt` (validiert) |
| `Link` | nein | nur absolute http(s)-URIs (validiert); Karte zeigt Link, sonst „Teilnahme über Discord" |

## Änderungen

| Datei | Änderung |
|-------|----------|
| `Erdi-ERC/Models/Giveaway.cs` | neue Entität `Giveaway` |
| `Erdi-ERC/Data/AppDbContext.cs` | DbSet `Giveaways` + Fluent-Config + Index `(StartAt, EndAt)` |
| Migration `20260914105405_AddGiveaway` | CreateTable (`Giveaways`) |
| `Erdi-ERC/Services/HomeIndexDataService.cs` | `HomeIndexData.ActiveGiveaways` + Aktiv-Zeitraum-Query |
| `Erdi-ERC/Controllers/HomeController.cs` | `ViewBag.ActiveGiveaways` |
| `Erdi-ERC/Views/Home/Index.cshtml` | neue Sektion „Giveaways" (nur wenn aktiv), wiederverwendete `f1-newscard`-Styles |
| `Erdi-ERC/Controllers/AdminCommunityController.cs` | `Giveaways`, `SaveGiveaway`, `DeleteGiveaway` |
| `Erdi-ERC/Views/Admin/Giveaways.cshtml` | Admin-CRUD-View (Vorbild `Events.cshtml`, ohne Bilder) |
| `Erdi-ERC/Models/AdminPermissions.cs` | Permission `community.giveaways` + Gruppen-Eintrag |
| `Erdi-ERC/Program.cs` | Policy `Admin.Community.Giveaways` + in `Admin.Community`-Gruppe |
| `Erdi-ERC/Views/Admin/_AdminLayout.cshtml` | Nav-Link „Giveaways" in der Community-Gruppe |

## Tests

`tests/Erdi-ERC.Tests/Controllers/AdminCommunityGiveawayTests.cs` (7 Tests):
Create persistiert alle Felder · Create ohne Link → null · ohne Titel abgelehnt · EndAt vor StartAt abgelehnt · ungültiger Link abgelehnt · Edit aktualisiert (kein Doppel-Eintrag) · Delete entfernt.

`tests/Erdi-ERC.Tests/Services/HomeIndexDataServiceTests.cs` (3 Tests):
Nur aktuell aktive Giveaways · leere Tabelle → leere Liste · Sortierung nach nächster Deadline.

Gesamt: **366 Tests grün** (356 vorher + 10 neu), Build grün.

## Deployment

Migration **nur auf erditest** anwenden (Migration-Safety), Prod (erdierc) nicht anfassen — siehe [[project_migration_safety_aug2026]].
