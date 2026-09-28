---
datum: 2026-09-25
tags: [entwicklung, bugfix, zeitzone, datetime, serverzeit, admin]
status: erledigt
aktualisiert: 2026-09-25
---

# Zeitzonen-Konvention (DateTime-Spalten)

Regelwerk dafür, wann ein `DateTime`-Wert aus der Datenbank roh angezeigt und wann er
mit `.ToLocalTime()` umgerechnet wird. Entstanden aus dem Bugreport „Server zeit
überprüfen“ — die App verglich Wanduhrzeiten mit `DateTime.UtcNow` und zeigte Zeiten
2 h verschoben an.

## Das eigentliche Problem: MySQL kennt kein `Kind`

Die Spalten sind `datetime(6)` — ein Format **ohne Zeitzonen-Offset**. Beim Lesen liefert
Pomelo/MySqlConnector deshalb **immer `DateTimeKind.Unspecified`**, egal ob der Wert als
`UtcNow` oder als Wanduhrzeit hineingeschrieben wurde. Der `Kind` überlebt den
Datenbank-Roundtrip nicht.

Entscheidend ist, was .NET dann mit einem `Unspecified`-Wert macht — empirisch geprüft
(Probe gegen die echte Runtime und den Dev-MySQL), **nicht** aus dem Gedächtnis:

| Aufruf auf `Unspecified` | Verhalten |
|---|---|
| `.ToLocalTime()` | interpretiert den Wert **als UTC** und rechnet um → in MESZ **+2 h** |
| `.ToUniversalTime()` | interpretiert den Wert **als Lokalzeit** → asymmetrisch zu oben |
| `.ToString("O")` | schreibt **keinen** Offset → JS `new Date(...)` liest ihn als Lokalzeit |

Der dritte Punkt ist der Grund, warum die Countdown-Widgets (`data-target`) korrekt
waren: der Wert kommt unverändert im Browser an und wird dort als Lokalzeit gelesen.

## Die Konvention

Es gibt genau zwei Sorten Spalten. Welche vorliegt, entscheidet **die Schreibstelle**,
nicht der Spaltenname:

| Sorte | Geschrieben mit | Anzeige | Vergleich |
|---|---|---|---|
| **Echter Zeitpunkt** („UTC-Spalte“) | `DateTime.UtcNow` | `.ToLocalTime()` | gegen `DateTime.UtcNow` |
| **Wanduhrzeit** („Lokal-Spalte“) | Formularfeld / `DateTime.Now` | **roh**, kein `ToLocalTime()` | gegen `DateTime.Now` / `DateTime.Today` |

Kurzform: **`Unspecified` == Wanduhrzeit, `Utc` == echter Zeitpunkt.**

### UTC-Spalten (Beispiele)

`AdminAuditLog.CreatedAt`, `AdminUser.AddedAt`, `DriverApplication.CreatedAt/DecidedAt`,
`CommunityNewsPost.PublishedAt`, `PendingRaceResult.ReceivedAt`, `TrackSetup.UpdatedAt`,
`RegelwerkDocument.UploadedAt`, `TelemetrySenderKey.RevokedAt/CreatedAt`,
`WebhookRule.LastRunAt`, `WebhookRegistration.LastUsedAt`, `Achievement.AwardedAt`,
`PendingRaceResult.SourceDate` (Parser nutzt `AdjustToUniversal`) — hier ist
`.ToLocalTime()` **richtig** und muss bleiben.

### Wanduhrzeit-Spalten (Beispiele)

`StreamSchedule.StartAt`, `Giveaway.StartAt/EndAt`, `RaceResult.Date`,
`RealLifeEvent.Date`, `RaceWeekendLeg.Date` — aus `datetime-local`-Formularfeldern
bzw. `DateTime.Now` befüllt. Hier verschiebt `.ToLocalTime()` die Anzeige um +2 h.

`BackgroundMusicFile.UploadedAt` ist ein Sonderfall: der Wert kommt aus
`File.GetCreationTime()` (`Kind = Local`) und ist damit ebenfalls **nicht** UTC,
`ToLocalTime()` wäre aber wirkungslos (Local → ToLocalTime ist die Identität). Bewusst
unverändert gelassen.

## Warum nicht global umstellen?

Zwei naheliegende Alternativen wurden verworfen:

- **Value-Converter im DbContext** (`alle DateTime → UTC`): verschiebt auch reine
  Datumsspalten wie `RaceWeekendLeg.Date`, die als `00:00` gespeichert sind und dann auf
  den Vortag rutschen. Außerdem ist ein Converter bereits ein Schema-Eingriff.
- **Umstellung der Speicherung auf UTC**: braucht eine Migration, und
  `MIGRATION-SAFETY.md` verbietet `dotnet ef database update` auf Prod. Die bestehenden
  Wanduhrzeit-Werte müssten zudem einmalig konvertiert werden.

Deshalb die Konvention als **Lese-/Vergleichsregel an der jeweiligen Stelle** — ohne
Schema-Änderung.

## Gefundene und gefixte Fehler

| Stelle | Fehler | Wirkung |
|---|---|---|
| `Services/StreamScheduleQueryService.cs` | `ComputeNextOccurrence` rechnete mit `UtcNow`, Eingabe war lokale Wanduhrzeit | Termin 2 h falsch; zwischen 00:00–02:00 lokal kippte der **Wochentag** → Termin rutschte eine ganze Woche |
| `Services/LayoutDataService.cs` | „Live jetzt“-Banner verglich `StartAt` mit `UtcNow` | Banner erschien erst 2 h nach Streamstart und blieb 2 h zu lang |
| `Services/HomeIndexDataService.cs` | widersprüchlich: ein Feld `UtcNow`, dasselbe Datum `DateTime.Today` | bevorstehende Termine/Giveaways 2 h falsch |
| `Views/Admin/Giveaways.cshtml` | Status-Badges + Zähler gegen `UtcNow`, Anzeige mit `ToLocalTime()` | „Läuft“/„Startet noch“/„Beendet“ 2 h falsch, Zeitraum +2 h angezeigt |
| `Controllers/AdminCommunityController.cs` | Stream-Start: `SpecifyKind(..., Utc)` auf einen Wanduhrzeit-Wert | Wert als Zeitpunkt deklariert, obwohl er Wanduhrzeit ist — Grundlage der 2-h-Verschiebung |
| Edit-Formulare (`StreamSchedules`, `Giveaways`) | Wert beim Laden mit `ToLocalTime()` ins `datetime-local`-Feld geschrieben und beim Speichern roh übernommen | **+2 h bei jedem Speichern** — der Termin wanderte mit jedem Edit weiter |
| `Controllers/AdminTelemetryController.cs` | leeres `datetime-local`-Feld fiel auf `UtcNow` zurück | Fallback trug eine zweite Semantik in dieselbe Spalte |
| `Views/Admin/StreamSchedules.cshtml`, `Giveaways.cshtml`, `Index.cshtml`, `Home/Index.cshtml` | Anzeige/Edit-Inputs mit `ToLocalTime()` auf Wanduhrzeit | +2 h in der Anzeige, Rückwanderung beim Speichern |

Beim Fix mitgefunden (gleiche Bauart, anderer Bug — siehe
[Silent-Write-Problem](#verwandt-silent-writes)): drei `FindAsync`-Schreibpfade ohne
`.AsTracking()` und 11 verlorene Audit-Log-Zeilen (`LogAsync` statt `LogAndSaveAsync`).

Der frühere Code hatte das Muster **doppelt**: `ComputeNextOccurrence` existierte
zweimal identisch (im Controller und im Query-Service). Beide Kopien waren falsch.
Jetzt gibt es **eine** Implementierung in `Helpers/StreamScheduleMath.cs`.

## Regeln für neuen Code

1. **Beim Anlegen einer `DateTime`-Spalte festlegen, welche Sorte sie ist**, und das im
   Model oder am Schreibpfad kommentieren.
2. `Kind` **niemals** aus der Datenbank erwarten — es ist immer `Unspecified`.
3. Wanduhrzeit-Spalten: roh anzeigen, gegen `DateTime.Now`/`DateTime.Today` vergleichen,
   für `datetime-local` **nie** `ToLocalTime()` (auch nicht beim Vorbelegen des Feldes —
   sonst wandert der Wert bei jedem Speichern weiter).
4. `DateTime.Now` erzeugen heißt Wanduhrzeit, `DateTime.UtcNow` heißt Zeitpunkt. Beides
   gemischt in einer Spalte ist der Bug.
5. Bei „Zeit stimmt nicht“-Reports **zuerst** prüfen, ob die verglichenen Werte dieselbe
   Sorte sind — nicht die Serverzeitzone. Die Serverzeit selbst war korrekt; falsch war
   die Interpretation in der App.

## Verwandt: Silent-Writes

Die Fixes in `AdminCommunityController` (`.AsTracking()`) und der Wechsel von
`LogAsync` auf `LogAndSaveAsync` gehören zur Klasse der **stillen Schreibfehler**:
`Program.cs` setzt global `NoTrackingWithIdentityResolution`, dadurch liefert
`FindAsync` ein *detached* Entity und `SaveChanges` schreibt nichts — ohne Fehler.
`SqliteTestContext` spiegelt dieses Default absichtlich, und Tests greifen den Fehler nur,
wenn sie **in einem fremden Context seeden** (`ctx.NewContext()`); Seeden über `ctx.Db`
versteckt ihn, weil das Entity im Change Tracker bleibt.

## Testabdeckung

Abgedeckt: `Helpers/StreamScheduleMathTests.cs`,
`Services/StreamScheduleQueryServiceTests.cs` (inkl. „Live jetzt“-Banner),
`Controllers/AdminCommunitySilentWriteTests.cs`, `AdminCommunityGiveawayTests.cs`.

**Nicht** automatisch abgedeckt: die Razor-View-Änderungen. `SmokeTests.cs` stellt fest,
dass es ohne Umbau von `Program.cs` (Migrationen optional machen) keine
`WebApplicationFactory`-Umgebung gibt — Views lassen sich hier also nicht rendern. Für
die View-Fixes bleibt nur die Sichtprüfung im Browser.

Siehe auch [[giveaway-infotafel]] und [[production-deploy]].
