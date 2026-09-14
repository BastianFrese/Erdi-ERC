---
datum: 2026-09-14
tags: [entwicklung, feature, stewarding, strafpunkte, admin]
status: erledigt
---

# Stewarding: Strafpunkte-Gesamtstand unter Reason

## Kurzbeschreibung

In Admin-Stewarding-Kartenliste und öffentlicher FIA-Dokument-Seite (`/Stewarding/Doc/{id}`) erscheint unter der Reason/Begründung eine Zeile, die zeigt, wie viele **Strafpunkte der Fahrer insgesamt** hat — als **Snapshot zum Zeitpunkt des Dokuments** (inkl. der Punkte des eigenen Dokuments).

## Wert-Logik (User-Vorgabe: „alte Berichte dürfen den Wert nicht aktualisieren")

- `LeaguePenalty.DriverPointsTotal` (`int?`) wird **nur beim ANLEGEN** gesetzt.
- Berechnung: `SUM(Points)` aller bestehenden `LeaguePenalty` des **gleichen Fahrers in der gleichen Liga** + die Punkte des neuen Dokuments.
- Bei Bearbeitung eines Alt-Dokuments bleibt der Wert **eingefroren**. Später angelegte Berichte des Fahrers ändern den Wert älterer Dokumente **nicht**.
- Bestandsdaten haben `NULL` → keine Zeile/Zeile wird ausgeblendet (kein Backfill, bewusst).
- Scoping **pro Fahrer + Liga** (jede Liga eigener Strafpunkte-Verlauf).

## Änderungen

| Datei | Änderung |
|-------|----------|
| `Erdi-ERC/Models/Erdi10ViewModel.cs` | Property `DriverPointsTotal` an `LeaguePenalty` |
| `Erdi-ERC/Data/AppDbContext.cs` | `b.Property(x => x.DriverPointsTotal);` |
| Migration `20260914093529_AddStewardingDriverPointsTotal` | `AddColumn<DriverPointsTotal>` (nur erditest deployen) |
| `Erdi-ERC/Controllers/AdminCommunityController.cs` | `SavePenalty`: Snapshot-Berechnung nur im `isNew`-Zweig |
| `Erdi-ERC/Views/Admin/Stewarding.cshtml` | „Strafpunkte gesamt: X" unter der Reason-Zeile |
| `Erdi-ERC/Views/Home/StewardingDoc.cshtml` | FIA-Zeile „Penalty Points — X (current total)" |

## Tests

`tests/Erdi-ERC.Tests/Controllers/AdminCommunityStewardingPenaltyTests.cs` (6 Tests):
Snapshot inkl. eigener Punkte · Scoping pro Liga · Scoping pro Fahrer · Edit friert Snapshot ein · späterer Bericht ändert alten nicht · 0-Punkte-Doku hält Bestand.

## Deployment

Migration **nur auf erditest** anwenden (Migration-Safety), Prod (erdierc) nicht anfassen. Build grün, 356 Tests grün.
