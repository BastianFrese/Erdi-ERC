# Driver of the Day — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Chat-Voting zum Driver of the Day pro Liga, gestartet im Admin, live angezeigt als Streamlabs-Browserquelle auf `https://erdi-erc.de/overlay/dotd`.

**Architecture:** Zwei neue EF-Tabellen (`DotdVotes`, `DotdBallots`). Ein Scoped-Service verarbeitet Stimmen aus einem anonymen Twitch-IRC-Leser und liefert Snapshots an eine öffentliche Overlay-Seite, die alle 2 s pollt. Der Admin öffnet/schließt die Wahl; die Overlay-Seite zeigt während der Wahl nur zufällig wackelnde Balken ohne Zahlen.

**Tech Stack:** ASP.NET Core MVC (`net10.0`), EF Core/Pomelo MySQL, Razor, handgeschriebenes CSS, xUnit + SQLite-Testkontext. Keine neuen NuGet-Pakete.

**Spec:** `Docs/superpowers/specs/2026-10-20-driver-of-the-day-design.md`

## Global Constraints

- Ziel-Framework `net10.0`; **keine neuen NuGet-Pakete** (IRC über `System.Net.WebSockets.ClientWebSocket`).
- UI-Texte und Code-Kommentare auf Deutsch.
- `StartedAt`/`ClosedAt` sind **UTC-Spalten**: schreiben mit `DateTime.UtcNow`, anzeigen mit `.ToLocalTime()` (siehe `Docs/Features/Zeitzonen-Konvention.md`).
- Migration **nur auf `erditest`** anwenden; `erdierc` (Prod) bleibt unangetastet, bis der Deploy freigegeben ist.
- Overlay-URL exakt `https://erdi-erc.de/overlay/dotd`.
- Während einer offenen Wahl zeigt das Overlay **keine Stimmenzahlen** und keine exakten Anteile.
- Der Chat-Leser verbindet sich **nur in Production**; Ausnahme ist `Dotd:ConnectOutsideProduction=true`.
- Test-Befehl: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj`.
- Commit-Format: `feat: …`, `test: …`, `docs: …` (deutsche Beschreibung).

## Review Focus

Diese Fälle sind in der Spec nicht durch Tests abgedeckt und müssen in den genannten Tasks gepinnt werden:

1. **Mehrdeutiger Chat-Name** (`!vote max` bei zwei „Max") → keine Stimme, niemals ein zufälliger Fahrer (Task 2 + 5).
2. **Vote trifft auf schließende Wahl** → keine Stimme landet auf einer geschlossenen Wahl (Task 5, Semaphore).
3. **App-Neustart mitten in der Wahl** → offene Wahl wird aus der DB geladen, Voting läuft weiter (Task 5).
4. **DB-Ausfall beim Overlay-Poll** → letzter Snapshot mit `stale: true`, Overlay wird nicht leer (Task 5).
5. **Fahrer wird während der Wahl umbenannt** → eingefrorene Kandidaten akzeptieren weiter den alten Namen (Task 5).
6. **Overlay-JSON enthält keine Stimmenzahlen** → nur `name` + gerundeter `share` (Task 4).

---

### Task 1: Persistenz

**Files:**
- Create: `Erdi-ERC/Models/DriverOfDay.cs`
- Modify: `Erdi-ERC/Data/AppDbContext.cs`
- Create: `Erdi-ERC/Migrations/<timestamp>_AddDriverOfDay.cs` (per `dotnet ef`)

**Interfaces:**
- Produces: `DotdVote`, `DotdBallot`, `DotdBar`, `DotdWinner`, `DotdSnapshot`, `DotdActiveVote`, `DotdCountRow`, `DotdRejectedAttempt`, `DotdAdminViewModel` (alle in `Erdi_ERC.Models`); `AppDbContext.DotdVotes`, `AppDbContext.DotdBallots`.

- [ ] **Step 1: Modelle anlegen**

`Models/DriverOfDay.cs` mit genau diesen Typen:

```csharp
public class DotdVote
{
    public int Id { get; set; }
    [MaxLength(64)]  public string LeagueId { get; set; } = "";
    [MaxLength(128)] public string LeagueName { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    [MaxLength(160)] public string? WinnerDriver { get; set; }
    public int? WinnerVotes { get; set; }
    public int? TotalVotes { get; set; }
    [MaxLength(2000)] public string? FinalSharesJson { get; set; }
    [MaxLength(32)] public string? StartedByDiscordId { get; set; }
}

public class DotdBallot
{
    public int Id { get; set; }
    public int VoteId { get; set; }
    [MaxLength(32)]  public string VoterKey { get; set; } = "";
    [MaxLength(160)] public string Driver { get; set; } = "";
    public DateTime VotedAt { get; set; }
}

public sealed record DotdBar(string Name, int Share);
public sealed record DotdWinner(string Name, int Percent);
public sealed record DotdSnapshot(string State, bool Stale, string League,
    IReadOnlyList<DotdBar> Bars, DotdWinner? Winner);
public sealed record DotdCountRow(string Driver, int Votes);
public sealed record DotdRejectedAttempt(string ChatText, DateTime At);

public sealed class DotdActiveVote
{
    public int Id { get; init; }
    public string LeagueName { get; init; } = "";
    public DateTime StartedAt { get; init; }
    public IReadOnlyList<DotdCountRow> Counts { get; init; } = Array.Empty<DotdCountRow>();
    public int TotalVotes { get; init; }
}

public sealed class DotdAdminViewModel
{
    public const string OverlayUrl = "https://erdi-erc.de/overlay/dotd";
    public IReadOnlyList<League> Leagues { get; set; } = Array.Empty<League>();
    public DotdActiveVote? Active { get; set; }
    public IReadOnlyList<DotdVote> History { get; set; } = Array.Empty<DotdVote>();
    public IReadOnlyList<DotdRejectedAttempt> Rejections { get; set; } = Array.Empty<DotdRejectedAttempt>();
}
```

- [ ] **Step 2: DbSets und Indizes ergänzen**

In `AppDbContext`:

```csharp
public DbSet<DotdVote> DotdVotes => Set<DotdVote>();
public DbSet<DotdBallot> DotdBallots => Set<DotdBallot>();
```

In `OnModelCreating`:

```csharp
modelBuilder.Entity<DotdBallot>(b =>
{
    b.HasIndex(x => new { x.VoteId, x.VoterKey }).IsUnique();
});
modelBuilder.Entity<DotdVote>(b =>
{
    b.HasIndex(x => x.ClosedAt);
});
```

- [ ] **Step 3: Build prüfen**

Run: `dotnet build Erdi-ERC.slnx`
Expected: Build succeeded, 0 Errors.

- [ ] **Step 4: Migration erzeugen**

Run: `dotnet tool restore && dotnet ef migrations add AddDriverOfDay --project Erdi-ERC --startup-project Erdi-ERC`
Expected: neue Migrationsdatei; sie legt `DotdVotes` und `DotdBallots` an und enthält den Unique-Index auf `(VoteId, VoterKey)`.

**Nicht** `dotnet ef database update` ausführen.

- [ ] **Step 5: Commit**

```bash
git add Erdi-ERC/Models/DriverOfDay.cs Erdi-ERC/Data/AppDbContext.cs Erdi-ERC/Migrations/
git commit -m "feat: Datenmodell für Driver-of-the-Day-Voting"
```

---

### Task 2: Namensabgleich

**Files:**
- Create: `Erdi-ERC/Helpers/DriverNameMatcher.cs`
- Modify: `Erdi-ERC/Services/DriverProfileService.cs` (privaten Levenshtein durch den Helper ersetzen)
- Test: `tests/Erdi-ERC.Tests/Helpers/DriverNameMatcherTests.cs`

**Interfaces:**
- Produces:
  - `DriverNameMatcher.Normalize(string? value) -> string`
  - `DriverNameMatcher.Levenshtein(string a, string b) -> int`
  - `DriverNameMatcher.Match(string query, IReadOnlyList<string> candidateFullNames) -> DriverNameMatcher.MatchResult`
  - `record MatchResult(string? Driver, bool Ambiguous)`

- [ ] **Step 1: Failing Tests schreiben**

```csharp
[Theory]
[InlineData("ERC | Müller", "mueller")]
[InlineData("  Max   Wiener ", "max wiener")]
[InlineData("ERC |", "")]
[InlineData("Élodie", "elodie")]
public void Normalize_entfernt_clantag_umlaute_und_leerzeichen(string input, string expected)
    => Assert.Equal(expected, DriverNameMatcher.Normalize(input));

[Fact]
public void Match_findet_exakten_anzeigenamen()
{
    var r = DriverNameMatcher.Match("Wiener", new[] { "ERC | Wiener", "ERC | Müller" });
    Assert.Equal("ERC | Wiener", r.Driver);
    Assert.False(r.Ambiguous);
}

[Fact]
public void Match_toleriert_einen_tippfehler()
{
    var r = DriverNameMatcher.Match("wienr", new[] { "ERC | Wiener" });
    Assert.Equal("ERC | Wiener", r.Driver);
}

[Fact]
public void Match_verwirft_mehrdeutige_eingabe()
{
    var r = DriverNameMatcher.Match("max", new[] { "ERC | Max Wiener", "ERC | Max Berger" });
    Assert.Null(r.Driver);
    Assert.True(r.Ambiguous);
}

[Fact]
public void Match_verwirft_unbekannten_namen()
{
    var r = DriverNameMatcher.Match("xyz", new[] { "ERC | Wiener" });
    Assert.Null(r.Driver);
    Assert.False(r.Ambiguous);
}
```

- [ ] **Step 2: Tests laufen lassen (rot)**

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter DriverNameMatcherTests`
Expected: FAIL, weil `DriverNameMatcher` noch nicht existiert.

- [ ] **Step 3: Implementieren**

Normalisierung in dieser Reihenfolge: trimmen → `ToLowerInvariant` → `ä→ae, ö→oe, ü→ue, ß→ss` → Unicode `FormD`, non-spacing marks entfernen → alles außer `[a-z0-9]` zu Leerzeichen → Mehrfach-Leerzeichen zusammenfassen → führendes `erc ` entfernen → trimmen.

`Match` bildet pro Kandidat den normalisierten vollen Namen und den normalisierten Anzeigenamen (`DriverNameHelper.Display`), dazu die Wörter des Anzeigenamens. Stufen, erste nicht-leere gewinnt:

1. Query == voller Name oder Anzeigename.
2. Query == ein Wort des Anzeigenamens (Wortlänge ≥ 2).
3. Query-Länge ≥ 3 und (Alias enthält Query oder Query enthält Alias mit Alias-Länge ≥ 3).
4. `Levenshtein(alias, query) ≤ 2` oder `Levenshtein(wort, query) ≤ 1` bei Wortlänge ≥ 3.

Pro Stufe: genau ein distinkter voller Name → Treffer; mehrere → `Ambiguous = true`; keiner → nächste Stufe.

Danach in `Services/DriverProfileService.cs` den privaten `Levenshtein`-Aufruf durch `DriverNameMatcher.Levenshtein` ersetzen und die private Methode entfernen. Es bleibt **eine** Implementierung.

- [ ] **Step 4: Tests laufen lassen (grün)**

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter DriverNameMatcherTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Erdi-ERC/Helpers/DriverNameMatcher.cs tests/Erdi-ERC.Tests/Helpers/DriverNameMatcherTests.cs
git commit -m "feat: toleranter Fahrernamen-Abgleich für Chat-Votes"
```

---

### Task 3: Twitch-IRC-Parser und Befehlserkennung

**Files:**
- Create: `Erdi-ERC/Helpers/TwitchIrcParser.cs`
- Test: `tests/Erdi-ERC.Tests/Helpers/TwitchIrcParserTests.cs`

**Interfaces:**
- Produces:
  - `TwitchIrcParser.ParsePrivMsg(string line) -> ChatMessage?`
  - `TwitchIrcParser.IsPing(string line) -> bool`
  - `TwitchIrcParser.ExtractVoteTarget(string? message) -> string?`
  - `record ChatMessage(string Login, string DisplayName, string UserId, string Text)`

- [ ] **Step 1: Failing Tests schreiben**

```csharp
const string Line =
    "@badge-info=;badges=;color=#1E90FF;display-name=WienerFan;emotes=;id=abc;mod=0;" +
    "room-id=1;subscriber=0;tmi-sent-ts=1;turbot=0;user-id=12345;user-type= " +
    ":wienerfan!wienerfan@wienerfan.tmi.twitch.tv PRIVMSG #erdi10 :!vote wiener";

[Fact]
public void ParsePrivMsg_liest_tags_login_und_text()
{
    var m = TwitchIrcParser.ParsePrivMsg(Line);
    Assert.NotNull(m);
    Assert.Equal("wienerfan", m!.Login);
    Assert.Equal("WienerFan", m.DisplayName);
    Assert.Equal("12345", m.UserId);
    Assert.Equal("!vote wiener", m.Text);
}

[Theory]
[InlineData("!vote wiener", "wiener")]
[InlineData("!VOTE  Wiener", "Wiener")]
[InlineData("!dotd max berger", "max berger")]
[InlineData("!vote", null)]
[InlineData("hallo wiener", null)]
public void ExtractVoteTarget_erkennt_nur_befehle(string text, string? expected)
    => Assert.Equal(expected, TwitchIrcParser.ExtractVoteTarget(text));

[Fact]
public void IsPing_erkennt_keepalive()
    => Assert.True(TwitchIrcParser.IsPing("PING :tmi.twitch.tv"));
```

- [ ] **Step 2: Tests laufen lassen (rot)**

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter TwitchIrcParserTests`
Expected: FAIL (Typ fehlt).

- [ ] **Step 3: Implementieren**

`ParsePrivMsg`: Zeile mit `PRIVMSG` prüfen, Tags vor `@` in `key=value` zerlegen, Prefix zwischen `:` und `!` als Login, Text hinter `" :"` nach dem Kanal. Fehlen Tags, `DisplayName = Login`, `UserId = ""`.

`ExtractVoteTarget`: Regex `^!(?:vote|dotd)\s+(.+)$`, `IgnoreCase`, Gruppe 1 trimmen.

`IsPing`: Zeile beginnt mit `PING `.

- [ ] **Step 4: Tests laufen lassen (grün)**

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter TwitchIrcParserTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Erdi-ERC/Helpers/TwitchIrcParser.cs tests/Erdi-ERC.Tests/Helpers/TwitchIrcParserTests.cs
git commit -m "feat: Twitch-IRC-Zeilen und Vote-Befehl parsen"
```

---

### Task 4: Balken-Anteile ohne Zahlen

**Files:**
- Create: `Erdi-ERC/Helpers/DotdShareBuilder.cs`
- Test: `tests/Erdi-ERC.Tests/Helpers/DotdShareBuilderTests.cs`

**Interfaces:**
- Produces:
  - `DotdShareBuilder.MaxBars = 8`
  - `DotdShareBuilder.BucketPercent = 5`
  - `DotdShareBuilder.BuildOpenBars(IReadOnlyDictionary<string,int> counts, Func<int>? noise = null) -> IReadOnlyList<DotdBar>`
  - `DotdShareBuilder.BuildFinalBars(IReadOnlyDictionary<string,int> counts) -> IReadOnlyList<DotdBar>`

- [ ] **Step 1: Failing Tests schreiben**

```csharp
[Fact]
public void Open_bars_runden_auf_fuenf_prozent_ohne_rauschen()
{
    var counts = new Dictionary<string, int> { ["A"] = 10, ["B"] = 5, ["C"] = 5 };
    var bars = DotdShareBuilder.BuildOpenBars(counts, () => 0);
    Assert.Equal(new[] { ("A", 50), ("B", 25), ("C", 25) },
        bars.Select(b => (b.Name, b.Share)));
}

[Fact]
public void Open_bars_zeigen_hoechstens_acht_fahrer()
{
    var counts = Enumerable.Range(0, 12).ToDictionary(i => $"F{i:00}", i => 12 - i);
    var bars = DotdShareBuilder.BuildOpenBars(counts, () => 0);
    Assert.Equal(8, bars.Count);
}

[Fact]
public void Final_bars_zeigen_exakte_prozente()
{
    var counts = new Dictionary<string, int> { ["A"] = 2, ["B"] = 1 };
    var bars = DotdShareBuilder.BuildFinalBars(counts);
    Assert.Equal(new[] { ("A", 67), ("B", 33) },
        bars.Select(b => (b.Name, b.Share)));
}

[Fact]
public void Bars_stehen_alphabetisch_und_bleiben_im_bereich()
{
    var counts = new Dictionary<string, int> { ["Zulu"] = 1, ["Alpha"] = 1 };
    var bars = DotdShareBuilder.BuildOpenBars(counts);
    Assert.Equal(new[] { "Alpha", "Zulu" }, bars.Select(b => b.Name));
    Assert.All(bars, b => Assert.InRange(b.Share, 0, 100));
}

[Fact]
public void Open_bars_enthalten_keine_Stimmenzahl()
{
    var counts = new Dictionary<string, int> { ["A"] = 10, ["B"] = 5 };
    var json = System.Text.Json.JsonSerializer.Serialize(
        DotdShareBuilder.BuildOpenBars(counts, () => 0));
    Assert.DoesNotContain("votes", json, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("count", json, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Tests laufen lassen (rot)**

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter DotdShareBuilderTests`
Expected: FAIL.

- [ ] **Step 3: Implementieren**

Auswahl: Top 8 nach Stimmen absteigend, bei Gleichstand Anzeigename alphabetisch. Ausgabe alphabetisch nach Name.

Offene Wahl: Anteil = `Math.Round(votes * 100.0 / total / 5) * 5`, mindestens 5; plus `noise()` (Default `Random.Shared.Next(-3, 4)`), auf 0–100 begrenzen. Keine Stimmenzahl im Ergebnis.

Endstand: exakter Prozentwert mit `MidpointRounding.AwayFromZero`, kein Rauschen.

- [ ] **Step 4: Tests laufen lassen (grün)**

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter DotdShareBuilderTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Erdi-ERC/Helpers/DotdShareBuilder.cs tests/Erdi-ERC.Tests/Helpers/DotdShareBuilderTests.cs
git commit -m "feat: gerundete, verrauschte Voting-Anteile ohne Stimmenzahlen"
```

---

### Task 5: Wahl-Zustand und Vote-Service

**Files:**
- Create: `Erdi-ERC/Services/DotdRuntimeState.cs`
- Create: `Erdi-ERC/Services/IDotdVoteService.cs`
- Create: `Erdi-ERC/Services/DotdVoteService.cs`
- Modify: `Erdi-ERC/Program.cs` (Registrierung)
- Test: `tests/Erdi-ERC.Tests/Services/DotdVoteServiceTests.cs`

**Interfaces:**
- Consumes: `DriverNameMatcher.Match`, `DotdShareBuilder.BuildOpenBars/BuildFinalBars`, `AppDbContext`.
- Produces:
  - `DotdRuntimeState` mit `SemaphoreSlim Gate`, `bool Loaded`, `int? ActiveVoteId`, `string? ActiveLeagueName`, `IReadOnlyList<string> Candidates`, `DotdSnapshot? LastSnapshot`, `void AddRejection(string text)`, `IReadOnlyList<DotdRejectedAttempt> RecentRejections`
  - `IDotdVoteService`:
    ```csharp
    Task<bool> HasOpenVoteAsync(CancellationToken ct = default);
    Task<DotdActionResult> OpenAsync(string leagueId, string? startedByDiscordId, CancellationToken ct = default);
    Task<DotdActionResult> CloseAsync(CancellationToken ct = default);
    Task<DotdActionResult> DiscardAsync(CancellationToken ct = default);
    Task<bool> RegisterVoteAsync(string voterKey, string chatText, CancellationToken ct = default);
    Task<DotdSnapshot> GetSnapshotAsync(CancellationToken ct = default);
    Task<DotdActiveVote?> GetActiveAsync(CancellationToken ct = default);
    IReadOnlyList<DotdRejectedAttempt> RecentRejections { get; }
    ```
  - `sealed record DotdActionResult(bool Ok, string? Error, int? VoteId, string? Details)`

- [ ] **Step 1: Failing Tests schreiben**

Testhilfe im Testprojekt:

```csharp
private static void SeedLeague(SqliteTestContext ctx, string id, params string[] drivers)
{
    ctx.Db.Leagues.Add(new League { Id = id, Name = "Pro", CurrentSeason = "2026" });
    var pos = 1;
    foreach (var d in drivers)
        ctx.Db.DriverStandings.Add(new DriverStanding { LeagueId = id, Driver = d, Team = "ERC", Position = pos++ });
    ctx.Db.SaveChanges();
}

private static (DotdVoteService Service, DotdRuntimeState State) Create(SqliteTestContext ctx)
{
    var state = new DotdRuntimeState();
    return (new DotdVoteService(ctx.Db, state, NullLogger<DotdVoteService>.Instance), state);
}
```

Tests:

```csharp
[Fact]
public async Task Open_und_gueltige_Stimme_zaehlen()
{
    using var ctx = new SqliteTestContext();
    SeedLeague(ctx, "pro", "ERC | Wiener", "ERC | Müller");
    var (service, _) = Create(ctx);

    Assert.True((await service.OpenAsync("pro", "admin-1")).Ok);
    Assert.True(await service.RegisterVoteAsync("tw-1", "wiener"));

    var active = await service.GetActiveAsync();
    Assert.NotNull(active);
    Assert.Equal(1, active!.TotalVotes);
    Assert.Equal("ERC | Wiener", active.Counts.Single().Driver);
}

[Fact]
public async Task Umentscheiden_ersetzt_die_alte_Stimme()
{
    using var ctx = new SqliteTestContext();
    SeedLeague(ctx, "pro", "ERC | Wiener", "ERC | Müller");
    var (service, _) = Create(ctx);
    await service.OpenAsync("pro", null);

    await service.RegisterVoteAsync("tw-1", "wiener");
    await service.RegisterVoteAsync("tw-1", "müller");

    var active = await service.GetActiveAsync();
    Assert.Equal(1, active!.TotalVotes);
    Assert.Equal("ERC | Müller", active.Counts.Single().Driver);
}

[Fact]
public async Task Mehrdeutiger_Name_zaehlt_nicht_und_wird_protokolliert()
{
    using var ctx = new SqliteTestContext();
    SeedLeague(ctx, "pro", "ERC | Max Wiener", "ERC | Max Berger");
    var (service, _) = Create(ctx);
    await service.OpenAsync("pro", null);

    Assert.False(await service.RegisterVoteAsync("tw-1", "max"));
    Assert.Empty(ctx.Db.DotdBallots);
    Assert.Contains(service.RecentRejections, r => r.ChatText.Contains("max"));
}

[Fact]
public async Task Schliessen_setzt_Sieger_und_loescht_Ballots()
{
    using var ctx = new SqliteTestContext();
    SeedLeague(ctx, "pro", "ERC | Wiener", "ERC | Müller");
    var (service, _) = Create(ctx);
    await service.OpenAsync("pro", null);
    await service.RegisterVoteAsync("tw-1", "wiener");
    await service.RegisterVoteAsync("tw-2", "wiener");
    await service.RegisterVoteAsync("tw-3", "müller");

    var result = await service.CloseAsync();

    Assert.True(result.Ok);
    var snapshot = await service.GetSnapshotAsync();
    Assert.Equal("closed", snapshot.State);
    Assert.Equal("ERC | Wiener", snapshot.Winner!.Name);
    Assert.Equal(67, snapshot.Winner.Percent);
    Assert.Equal(0, ctx.Db.DotdBallots.Count());
}

[Fact]
public async Task Stimme_nach_dem_Schliessen_zaehlt_nicht()
{
    using var ctx = new SqliteTestContext();
    SeedLeague(ctx, "pro", "ERC | Wiener");
    var (service, _) = Create(ctx);
    await service.OpenAsync("pro", null);
    await service.CloseAsync();

    Assert.False(await service.RegisterVoteAsync("tw-1", "wiener"));
    Assert.Empty(ctx.Db.DotdBallots);
}

[Fact]
public async Task Nach_Neustart_laeuft_die_offene_Wahl_weiter()
{
    using var ctx = new SqliteTestContext();
    SeedLeague(ctx, "pro", "ERC | Wiener", "ERC | Müller");
    var (first, _) = Create(ctx);
    await first.OpenAsync("pro", null);
    await first.RegisterVoteAsync("tw-1", "wiener");

    var secondState = new DotdRuntimeState();
    var second = new DotdVoteService(ctx.NewContext(), secondState, NullLogger<DotdVoteService>.Instance);

    Assert.True(await second.RegisterVoteAsync("tw-2", "müller"));
    var active = await second.GetActiveAsync();
    Assert.Equal(2, active!.TotalVotes);
}

[Fact]
public async Task Umbenannter_Fahrer_bleibt_ueber_den_alten_Namen_waehlbar()
{
    using var ctx = new SqliteTestContext();
    SeedLeague(ctx, "pro", "ERC | Wiener");
    var (service, _) = Create(ctx);
    await service.OpenAsync("pro", null);

    var standing = ctx.Db.DriverStandings.AsTracking().Single(s => s.Driver == "ERC | Wiener");
    standing.Driver = "ERC | Wiener2";
    ctx.Db.SaveChanges();

    Assert.True(await service.RegisterVoteAsync("tw-1", "wiener"));
}

[Fact]
public async Task Db_Ausfall_liefert_letzten_Stand_mit_stale()
{
    using var ctx = new SqliteTestContext();
    SeedLeague(ctx, "pro", "ERC | Wiener");
    var (service, _) = Create(ctx);
    await service.OpenAsync("pro", null);
    await service.GetSnapshotAsync();

    ctx.Db.Dispose();

    var snapshot = await service.GetSnapshotAsync();
    Assert.True(snapshot.Stale);
    Assert.Equal("open", snapshot.State);
}

[Fact]
public async Task Verwerfen_loescht_Wahl_und_Ballots()
{
    using var ctx = new SqliteTestContext();
    SeedLeague(ctx, "pro", "ERC | Wiener");
    var (service, _) = Create(ctx);
    await service.OpenAsync("pro", null);
    await service.RegisterVoteAsync("tw-1", "wiener");

    Assert.True((await service.DiscardAsync()).Ok);
    Assert.Empty(ctx.Db.DotdVotes);
    Assert.Empty(ctx.Db.DotdBallots);
    Assert.False(await service.HasOpenVoteAsync());
}
```

- [ ] **Step 2: Tests laufen lassen (rot)**

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter DotdVoteServiceTests`
Expected: FAIL.

- [ ] **Step 3: Implementieren**

`DotdRuntimeState` hält den Zustand thread-sicher; `AddRejection` hält die letzten 20 Einträge (Queue, älteste verwerfen).

`DotdVoteService` nimmt `AppDbContext db`, `DotdRuntimeState state`, `ILogger<DotdVoteService>`. Jede öffentliche Methode nimmt `state.Gate`, ruft einmalig `EnsureLoadedAsync` (offene Vote aus der DB laden, Kandidaten aus `DriverStandings` der Liga, `Loaded = true`) und gibt das Gate im `finally` frei.

- `OpenAsync`: Liga existiert und ist nicht archiviert; keine offene Wahl; Kandidaten nicht leer; `DotdVote` mit `StartedAt = DateTime.UtcNow` speichern; Zustand setzen. Fehler als `DotdActionResult(false, "…", null, null)`.
- `RegisterVoteAsync`: ohne offene Wahl `false`; `DriverNameMatcher.Match`; kein Treffer → `AddRejection`, `false`; sonst Ballot per `(VoteId, VoterKey)` mit `.AsTracking()` suchen, anlegen oder überschreiben, `VotedAt = DateTime.UtcNow`.
- `CloseAsync`: Ballots gruppieren, Sieger (Stimmen absteigend, dann Anzeigename), `FinalSharesJson = JsonSerializer.Serialize(DotdShareBuilder.BuildFinalBars(counts))`, `ClosedAt`, `WinnerVotes`, `TotalVotes`; Ballots mit `ExecuteDeleteAsync` löschen; Zustand leeren; geschlossenen Snapshot in `LastSnapshot` cachen.
- `DiscardAsync`: Ballots und Vote löschen, Zustand leeren.
- `GetSnapshotAsync`: offene Wahl → `BuildOpenBars`; sonst neueste geschlossene Vote (`OrderByDescending(ClosedAt)`) → `FinalSharesJson` deserialisieren; keine Vote → `state = "none"`. Jede Exception außer `OperationCanceledException` → `LastSnapshot` mit `Stale = true` (oder `none` + `Stale = true`, wenn nichts gecacht ist).
- `GetActiveAsync`: Vote-Zeile für `StartedAt` laden, exakte Counts, sortiert nach Stimmen absteigend, dann Anzeigename.
- `HasOpenVoteAsync`: `ActiveVoteId != null`.

- [ ] **Step 4: Tests laufen lassen (grün)**

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter DotdVoteServiceTests`
Expected: PASS.

- [ ] **Step 5: In `Program.cs` registrieren**

```csharp
builder.Services.AddSingleton<Erdi_ERC.Services.DotdRuntimeState>();
builder.Services.AddScoped<Erdi_ERC.Services.IDotdVoteService, Erdi_ERC.Services.DotdVoteService>();
```

- [ ] **Step 6: Commit**

```bash
git add Erdi-ERC/Services/DotdRuntimeState.cs Erdi-ERC/Services/IDotdVoteService.cs \
        Erdi-ERC/Services/DotdVoteService.cs Erdi-ERC/Program.cs \
        tests/Erdi-ERC.Tests/Services/DotdVoteServiceTests.cs
git commit -m "feat: Vote-Service mit Zustand, Siegerberechnung und Stale-Fallback"
```

---

### Task 6: Chat-Leser

**Files:**
- Create: `Erdi-ERC/Options/DotdOptions.cs`
- Create: `Erdi-ERC/Services/TwitchChatReaderService.cs`
- Modify: `Erdi-ERC/Program.cs`
- Modify: `Erdi-ERC/appsettings.Development.example.json` (Schalter dokumentieren, Default `false`)

**Interfaces:**
- Consumes: `TwitchIrcParser`, `IDotdVoteService`, `ApplicationOptions.TwitchChannel`, `IServiceScopeFactory`.
- Produces: `DotdOptions { const string SectionName = "Dotd"; bool ConnectOutsideProduction { get; set; } }`

- [ ] **Step 1: Option und Hosted Service anlegen**

`DotdOptions` mit `SectionName = "Dotd"` und `ConnectOutsideProduction` (Default `false`).

`TwitchChatReaderService : BackgroundService`:
- In `ExecuteAsync`: außerhalb Production und ohne Schalter → Logzeile und `return`.
- Schleife: `ClientWebSocket` gegen `wss://irc-ws.chat.twitch.tv:443`; senden:
  `CAP REQ :twitch.tv/tags`, `PASS SCHMOOPIIE`, `NICK justinfan<Zufallszahl>`, `JOIN #<TwitchChannel>`.
- Empfangen in 8-KB-Puffer, an `StringBuilder` hängen, bei `\n` Zeile verarbeiten.
- `IsPing` → `PONG :tmi.twitch.tv` senden.
- `ParsePrivMsg` → pro Nachricht einen Scope öffnen, `IDotdVoteService` auflösen, `RegisterVoteAsync(userId.Length > 0 ? userId : login, text)`.
- Jede Exception loggen, 5 s warten, neu verbinden; `OperationCanceledException` beendet die Schleife.

- [ ] **Step 2: Registrieren**

```csharp
builder.Services.Configure<Erdi_ERC.Options.DotdOptions>(
    builder.Configuration.GetSection(Erdi_ERC.Options.DotdOptions.SectionName));
builder.Services.AddHostedService<Erdi_ERC.Services.TwitchChatReaderService>();
```

- [ ] **Step 3: Build und Tests**

Run: `dotnet build Erdi-ERC.slnx && dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj`
Expected: Build succeeded; alle Tests grün (der Hosted Service startet in Tests nicht).

- [ ] **Step 4: Sichtprüfung des Schalters**

Run: `dotnet run --project Erdi-ERC` (Development, Schalter aus)
Expected: Logzeile „Twitch-Chat-Leser ist außerhalb von Production deaktiviert." und keine Verbindung zu `#erdi10`.

- [ ] **Step 5: Commit**

```bash
git add Erdi-ERC/Options/DotdOptions.cs Erdi-ERC/Services/TwitchChatReaderService.cs \
        Erdi-ERC/Program.cs Erdi-ERC/appsettings.Development.example.json
git commit -m "feat: anonymer Twitch-Chat-Leser für Vote-Befehle"
```

---

### Task 7: Admin-Fläche

**Files:**
- Create: `Erdi-ERC/Controllers/AdminDriverOfDayController.cs`
- Create: `Erdi-ERC/Views/Admin/DriverOfDay.cshtml`
- Modify: `Erdi-ERC/Views/Admin/_AdminLayout.cshtml`
- Modify: `Erdi-ERC/Views/Admin/Index.cshtml`
- Modify: `Erdi-ERC/Controllers/AdminController.cs`

**Interfaces:**
- Consumes: `IDotdVoteService`, `AppDbContext`, `IAdminAuditService`, `DotdAdminViewModel`.
- Produces: Routen `GET /admin/driver-of-day`, `POST /admin/driver-of-day/open`, `POST /admin/driver-of-day/close`, `POST /admin/driver-of-day/discard`.

- [ ] **Step 1: Controller anlegen**

```csharp
[Authorize(Policy = "Admin.Community.Hub")]
[Route("admin/driver-of-day")]
public class AdminDriverOfDayController : Controller
{
    [HttpGet("")]                      public async Task<IActionResult> Index();
    [HttpPost("open")]                public async Task<IActionResult> Open(string leagueId);
    [HttpPost("close")]               public async Task<IActionResult> Close();
    [HttpPost("discard")]             public async Task<IActionResult> Discard();
}
```

`Index` lädt Ligen (`OrderBy(SortOrder).ThenBy(Name)`), Historie (`ClosedAt != null`, neueste 10) und `GetActiveAsync()`; baut `DotdAdminViewModel`.

`Open`/`Close`/`Discard` rufen den Service, schreiben `TempData["AdminMessage"]` mit Fehler oder Erfolg und auditieren mit `IAdminAuditService.LogAndSaveAsync`:
`DotdVoteOpen`, `DotdVoteClose`, `DotdVoteVoid`, Entity `DotdVote`, Details aus `DotdActionResult.Details`. Danach `RedirectToAction(nameof(Index))`.

- [ ] **Step 2: View anlegen**

`Views/Admin/DriverOfDay.cshtml` mit `@model DotdAdminViewModel`, bestehenden Admin-Klassen (`adm-dashboard`, `adm-panel`, `adm-kpi-grid`) und:

- Statuskarte: offene Wahl (Liga, `StartedAt.ToLocalTime()`, exakte Stimmenzahl) mit „Wahl schließen" und „Wahl verwerfen" (Bestätigungsdialog per `onsubmit="return confirm(...)"`).
- Formular „Wahl öffnen": `<select name="leagueId">` aus den nicht archivierten Ligen, Button.
- Tabelle der exakten Counts (`DriverNameHelper.Display`, Stimmen).
- Overlay-Link als Text plus „Kopieren"-Button (`navigator.clipboard.writeText`), Wert `DotdAdminViewModel.OverlayUrl`.
- Historie: Liga, Datum (`ClosedAt.Value.ToLocalTime()`), Sieger (Anzeigename), Stimmen; bei `WinnerDriver == null` „Keine Stimmen".
- Diagnose: letzte verworfene Chat-Texte.

Alle Formulare nutzen `asp-action` und `@Html.AntiForgeryToken()` (globaler Filter erzwingt es ohnehin).

- [ ] **Step 3: Navigation und Dashboard-Karte ergänzen**

In `_AdminLayout.cshtml` unter „Community", sichtbar bei `canComHub`:

```html
<a class="@Nav("AdminDriverOfDay","Index")" asp-controller="AdminDriverOfDay" asp-action="Index">
    <i class="bi bi-mic-fill"></i> Driver of the Day
</a>
```

In `AdminController.Index` `ViewBag.DotdOpen = await _dotd.HasOpenVoteAsync();` setzen (Konstruktor um `IDotdVoteService _dotd` erweitern) und in `Views/Admin/Index.cshtml` eine KPI-Karte mit Wert `ViewBag.DotdOpen == true ? "LIVE" : "—"` und Link auf die neue Seite einfügen.

- [ ] **Step 4: Build**

Run: `dotnet build Erdi-ERC.slnx`
Expected: 0 Errors.

- [ ] **Step 5: Manuell prüfen**

Run: `dotnet run --project Erdi-ERC`
Expected: `/admin/driver-of-day` rendert, Liga öffnen/schließen/verwerfen funktioniert, Audit-Einträge erscheinen in „Audit Logs". Produktionsdaten unberührt (Dev-DB).

- [ ] **Step 6: Commit**

```bash
git add Erdi-ERC/Controllers/AdminDriverOfDayController.cs Erdi-ERC/Views/Admin/DriverOfDay.cshtml \
        Erdi-ERC/Views/Admin/_AdminLayout.cshtml Erdi-ERC/Views/Admin/Index.cshtml \
        Erdi-ERC/Controllers/AdminController.cs
git commit -m "feat: Admin-Fläche zum Öffnen, Schließen und Verwerfen der DOTD-Wahl"
```

---

### Task 8: Overlay-Seite und Daten-Endpunkt

**Files:**
- Create: `Erdi-ERC/Controllers/OverlayController.cs`
- Create: `Erdi-ERC/Views/Overlay/DriverOfDay.cshtml`
- Create: `Erdi-ERC/wwwroot/css/pages/dotd-overlay.css`
- Modify: `Erdi-ERC/Erdi-ERC.csproj` (CSS-Bundle-Liste)

**Interfaces:**
- Consumes: `IDotdVoteService.GetSnapshotAsync`.
- Produces: `GET /overlay/dotd` (HTML), `GET /overlay/dotd/data` (JSON `DotdSnapshot`).

- [ ] **Step 1: Controller anlegen**

Konstruktor nimmt `IDotdVoteService _dotd`.

```csharp
[AllowAnonymous]
[Route("overlay/dotd")]
public class OverlayController : Controller
{
    [HttpGet("")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult DriverOfDay() => View("~/Views/Overlay/DriverOfDay.cshtml");

    [HttpGet("data")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Data(CancellationToken ct)
        => Json(await _dotd.GetSnapshotAsync(ct));
}
```

- [ ] **Step 2: View anlegen**

`Layout = null`, `noindex`, `body` mit Klasse `dotd-overlay`, Link auf `~/css/site.bundled.min.css`.

Struktur:

```html
<main class="dotd" id="dotd">
  <div class="dotd__league" id="league"></div>
  <h1 class="dotd__title">Driver of the Day</h1>
  <p class="dotd__hint" id="hint">Vote im Chat: <strong>!vote name</strong></p>
  <div class="dotd__bars" id="bars"></div>
  <div class="dotd__winner" id="winner" hidden></div>
  <div class="dotd__idle" id="idle" hidden>Keine Abstimmung aktiv</div>
</main>
```

- [ ] **Step 3: Polling und Balken-Animation implementieren**

Inline-JS im View (CSP erlaubt `unsafe-inline`). Verhalten:

- Alle 2000 ms `fetch('/overlay/dotd/data', { cache: 'no-store' })`.
- `state === 'none'`: `#bars` ausblenden, `#idle` zeigen.
- `state === 'open'`: `#idle` ausblenden, `#hint` zeigen, `#winner` ausblenden; für jeden Eintrag Zeile mit Name und Füll-Div anlegen/aktualisieren; entfernte Zeilen ausblenden.
- `state === 'closed'`: `#hint` ausblenden; Balken auf exakte `share`-Werte; `#winner` zeigt „🏆" + Siegername + Prozent; ohne Sieger „Keine Stimmen".
- Animation über `requestAnimationFrame`: alle 400 ms pro Balken neues Rauschen ±4 Prozentpunkte (nur bei `open`), angezeigter Wert bewegt sich mit Faktor 0,08 auf das Ziel. Kein Text mit Zahlen an den Balken.

Kernschleife:

```js
let mode = 'none', noiseAt = 0;
const bars = new Map(); // name -> { row, fill, target, shown, jitter }
function tick(ts) {
  if (ts - noiseAt > 400) {
    noiseAt = ts;
    for (const b of bars.values()) b.jitter = (Math.random() * 2 - 1) * 4;
  }
  for (const b of bars.values()) {
    const goal = mode === 'open' ? b.target + b.jitter : b.target;
    b.shown += (goal - b.shown) * 0.08;
    b.fill.style.width = Math.max(0, Math.min(100, b.shown)) + '%';
  }
  requestAnimationFrame(tick);
}
requestAnimationFrame(tick);
```

- [ ] **Step 4: CSS anlegen und einbinden**

`wwwroot/css/pages/dotd-overlay.css`: transparenter Hintergrund, kein Scrollbalken, race-control-typische Typografie, Balken auf voller Breite, Farben aus den vorhandenen Tokens (`var(--accent)`, `var(--bg-0)` usw.). Keine neuen Hex-Farben, außer als Fallback.

Die Datei in `Erdi-ERC.csproj` im `BundleSiteCss`-`Inputs`-Attribut ergänzen: `;wwwroot/css/pages/dotd-overlay.css`.

- [ ] **Step 5: Manuell prüfen**

Run: `dotnet run --project Erdi-ERC`
Expected:
1. Ohne offene Wahl: „Keine Abstimmung aktiv".
2. Wahl im Admin öffnen: Überschrift, Hinweis, keine Balken.
3. Eine Stimme lokal erzeugen (Chat-Leser mit `Dotd:ConnectOutsideProduction=true` gegen `#erdi10`, oder `INSERT` in `DotdBallots` der Dev-DB): ein Balken erscheint und wackelt, **keine Zahl**.
4. Wahl schließen: Balken beruhigen sich, Sieger erscheint, `curl https://localhost:<port>/overlay/dotd/data` zeigt `state: "closed"` und `stale: false`.

- [ ] **Step 6: Commit**

```bash
git add Erdi-ERC/Controllers/OverlayController.cs Erdi-ERC/Views/Overlay/DriverOfDay.cshtml \
        Erdi-ERC/wwwroot/css/pages/dotd-overlay.css Erdi-ERC/Erdi-ERC.csproj
git commit -m "feat: Browser-Source-Overlay mit wackelnden Balken ohne Zahlen"
```

---

### Task 9: Dokumentation und Gesamtprüfung

**Files:**
- Create: `Docs/Features/driver-of-the-day.md`
- Modify: `Docs/superpowers/specs/2026-10-20-driver-of-the-day-design.md` (Status)

**Interfaces:** keine.

- [ ] **Step 1: Feature-Doku schreiben**

`Docs/Features/driver-of-the-day.md` mit Pflicht-Frontmatter (`datum: 2026-10-20`, `tags: [entwicklung, feature, streaming]`, `status: erledigt`) und: Overlay-URL, Admin-Weg, Chat-Syntax, Namensabgleich, wo die Daten liegen, was beim Schließen passiert (Ballots gelöscht), Production-Schalter des Chat-Lesers, Deploy-Hinweis (Migration nur `erditest`, Prod erst nach Freigabe).

- [ ] **Step 2: Spec-Status setzen**

Im Frontmatter der Spec `status: entwurf` → `status: erledigt`.

- [ ] **Step 3: Gesamtprüfung**

Run: `dotnet build Erdi-ERC.slnx && dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj`
Expected: Build succeeded; alle Tests grün, einschließlich der neuen Matcher-, Parser-, Share- und Service-Tests.

- [ ] **Step 4: Commit**

```bash
git add Docs/Features/driver-of-the-day.md Docs/superpowers/specs/2026-10-20-driver-of-the-day-design.md
git commit -m "docs: Driver-of-the-Day-Voting dokumentiert"
```

---

## Deploy (nach Freigabe, nicht Teil der Tasks)

1. Migration auf `erditest` anwenden und dort durchklicken.
2. Production: `systemctl stop erdi-erc` → Publish kopieren → `systemctl start erdi-erc`.
3. Migration auf `erdierc` erst nach Sicherung und ausdrücklicher Freigabe; Schema muss vor dem Restart stehen.
4. In Streamlabs eine Browserquelle mit `https://erdi-erc.de/overlay/dotd` anlegen (transparenter Hintergrund, Größe z. B. 1920×1080).
