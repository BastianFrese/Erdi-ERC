using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models.Troll;
using <OWNER_HANDLE>_ERC.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace <OWNER_HANDLE>_ERC.Services;

/// <summary>
/// Standard-Implementierung von <see cref="ITrollService"/>. Hält den fest eingebauten
/// Gag-Katalog (Code) und mischt zur Laufzeit – falls eine DB vorhanden ist – admin-verwaltete
/// Overrides (an/aus, Gewicht), eigene Inhalts-Gags und globale Settings dazu. Der gemischte
/// „effektive" Stand wird kurz gecacht und ist <b>fail-open</b>: jeder DB-Fehler fällt auf den
/// Code-/appsettings-Stand zurück, sodass der Login nie blockiert wird.
///
/// Ohne DB (Unit-Tests: <c>db == null</c>) verhält sich der Service exakt wie der reine
/// Code-Katalog + appsettings-Gewichte – die pure Auswahl-Logik bleibt deterministisch testbar.
/// </summary>
public sealed class TrollService : ITrollService
{
    private readonly TrollOptions _options;
    private readonly Random _rng;
    private readonly AppDbContext? _db;
    private readonly IMemoryCache? _cache;

    private const string CacheKey = "troll:effective:v1";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Fest eingebauter Gag-Katalog. Neuer eingebauter Gag = hier ein Eintrag + im passenden
    /// Kategorie-Partial (<c>Views/Troll/Gags/_*.cshtml</c>) ein <c>case</c> auf den Key.
    /// Admin-erstellte Inhalts-Gags kommen NICHT hierher, sondern aus der DB.
    /// </summary>
    private static readonly TrollGagDefinition[] BuiltIns =
    {
        // ── Mathe & Köpfchen (Pflicht-Gags, serverseitig validiert) ──
        new() { Key = "math",          PartialName = "Gags/_MatheKoepfchen",   Category = TrollGagCategory.MatheKoepfchen,   IsBlocking = true, DefaultWeight = 16 },
        new() { Key = "best-driver",   PartialName = "Gags/_MatheKoepfchen",   Category = TrollGagCategory.MatheKoepfchen,   IsBlocking = true, DefaultWeight = 8  },
        new() { Key = "f1-trivia",     PartialName = "Gags/_MatheKoepfchen",   Category = TrollGagCategory.MatheKoepfchen,   IsBlocking = true, DefaultWeight = 10 },
        new() { Key = "sequence",      PartialName = "Gags/_MatheKoepfchen",   Category = TrollGagCategory.MatheKoepfchen,   IsBlocking = true, DefaultWeight = 8  },
        new() { Key = "oath-type",     PartialName = "Gags/_MatheKoepfchen",   Category = TrollGagCategory.MatheKoepfchen,   IsBlocking = true, DefaultWeight = 6  },
        // ── Fake-System-Pranks (wegklickbar) ──
        new() { Key = "fake-loading",  PartialName = "Gags/_FakeSystem",       Category = TrollGagCategory.FakeSystem,       DefaultWeight = 10 },
        new() { Key = "fake-demotion", PartialName = "Gags/_FakeSystem",       Category = TrollGagCategory.FakeSystem,       DefaultWeight = 8  },
        new() { Key = "fake-terms",    PartialName = "Gags/_FakeSystem",       Category = TrollGagCategory.FakeSystem,       DefaultWeight = 8  },
        new() { Key = "bluescreen",    PartialName = "Gags/_FakeSystem",       Category = TrollGagCategory.FakeSystem,       DefaultWeight = 8  },
        new() { Key = "fake-update",   PartialName = "Gags/_FakeSystem",       Category = TrollGagCategory.FakeSystem,       DefaultWeight = 8  },
        new() { Key = "fake-ban",      PartialName = "Gags/_FakeSystem",       Category = TrollGagCategory.FakeSystem,       DefaultWeight = 8  },
        // ── Reaktion & Geschick ──
        new() { Key = "reaction",       PartialName = "Gags/_ReaktionGeschick", Category = TrollGagCategory.ReaktionGeschick, DefaultWeight = 10 },
        new() { Key = "not-a-bot",      PartialName = "Gags/_ReaktionGeschick", Category = TrollGagCategory.ReaktionGeschick, IsBlocking = true, DefaultWeight = 8 },
        new() { Key = "runaway-button", PartialName = "Gags/_ReaktionGeschick", Category = TrollGagCategory.ReaktionGeschick, DefaultWeight = 8  },
        new() { Key = "hold-button",    PartialName = "Gags/_ReaktionGeschick", Category = TrollGagCategory.ReaktionGeschick, DefaultWeight = 8  },
        new() { Key = "whack-erdi",     PartialName = "Gags/_ReaktionGeschick", Category = TrollGagCategory.ReaktionGeschick, DefaultWeight = 8  },
        new() { Key = "red-button",     PartialName = "Gags/_ReaktionGeschick", Category = TrollGagCategory.ReaktionGeschick, DefaultWeight = 8  },
        // ── Glück & Sprüche ──
        new() { Key = "wheel",       PartialName = "Gags/_GlueckSprueche", Category = TrollGagCategory.GlueckSprueche, DefaultWeight = 10 },
        new() { Key = "fortune",     PartialName = "Gags/_GlueckSprueche", Category = TrollGagCategory.GlueckSprueche, DefaultWeight = 10 },
        new() { Key = "dice",        PartialName = "Gags/_GlueckSprueche", Category = TrollGagCategory.GlueckSprueche, DefaultWeight = 8  },
        new() { Key = "magic-8ball", PartialName = "Gags/_GlueckSprueche", Category = TrollGagCategory.GlueckSprueche, DefaultWeight = 9  },
        new() { Key = "horoscope",   PartialName = "Gags/_GlueckSprueche", Category = TrollGagCategory.GlueckSprueche, DefaultWeight = 8  },
        new() { Key = "coinflip",    PartialName = "Gags/_GlueckSprueche", Category = TrollGagCategory.GlueckSprueche, DefaultWeight = 8  },
    };

    public TrollService(IOptions<TrollOptions> options, AppDbContext? db = null, IMemoryCache? cache = null)
    {
        _options = options.Value;
        // Random.Shared ist thread-safe; ausreichend für einen reinen Gag-Würfel.
        _rng = Random.Shared;
        _db = db;
        _cache = cache;
    }

    public IReadOnlyList<TrollGagDefinition> BuiltInCatalog => BuiltIns;

    // ───────────────────────── Effektiver Stand (gecacht, fail-open) ─────────────────────────

    /// <summary>Gemischter Stand aus Settings + aktivem Katalog + Custom-Gags + aufgelösten Gewichten.</summary>
    private sealed record Effective(
        bool Enabled,
        double TriggerChance,
        int CooldownMinutes,
        bool ApplyToAdmins,
        int MercyAfterAttempts,
        int MathMaxOperand,
        IReadOnlyList<TrollGagDefinition> Catalog,
        IReadOnlyDictionary<string, int> Weights,
        IReadOnlyDictionary<string, TrollCustomGag> Custom);

    private Effective GetEffective()
    {
        if (_db is null) return OptionsOnly();
        if (_cache is null) return LoadSafe();

        return _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            entry.Size = 1;
            return LoadSafe();
        })!;
    }

    /// <summary>Reiner Code-/appsettings-Stand (Tests &amp; Fallback): voller eingebauter Katalog, Gewichte aus appsettings.</summary>
    private Effective OptionsOnly()
        => new(_options.Enabled, _options.TriggerChance, _options.CooldownMinutes, _options.ApplyToAdmins,
               _options.MercyAfterAttempts, _options.MathMaxOperand,
               BuiltIns, new Dictionary<string, int>(), new Dictionary<string, TrollCustomGag>());

    private Effective LoadSafe()
    {
        try
        {
            var s = _db!.TrollSettings.AsNoTracking().FirstOrDefault();
            var overrides = _db.TrollGagOverrides.AsNoTracking().ToList()
                               .GroupBy(o => o.Key).ToDictionary(g => g.Key, g => g.First());
            var customs = _db.TrollCustomGags.AsNoTracking().ToList();

            var weights = new Dictionary<string, int>();
            var catalog = new List<TrollGagDefinition>();

            // Eingebaute Gags + Overrides (deaktivierte fliegen raus, sonst Override- bzw. Default-Gewicht).
            foreach (var g in BuiltIns)
            {
                var ov = overrides.TryGetValue(g.Key, out var o) ? o : null;
                if (ov is not null && !ov.IsEnabled) continue;
                weights[g.Key] = Math.Max(0, ov?.Weight
                    ?? (_options.Weights.TryGetValue(g.Key, out var ow) ? ow : g.DefaultWeight));
                catalog.Add(g);
            }

            // Admin-erstellte Inhalts-Gags (nur aktive, mit gültigem Key).
            var customByKey = new Dictionary<string, TrollCustomGag>(StringComparer.Ordinal);
            foreach (var c in customs)
            {
                if (!c.IsEnabled || string.IsNullOrWhiteSpace(c.Key)) continue;
                if (customByKey.ContainsKey(c.Key)) continue;
                catalog.Add(new TrollGagDefinition
                {
                    Key = c.Key,
                    PartialName = "Gags/_Custom",
                    Category = c.Category,
                    IsBlocking = c.Kind == TrollCustomGagKind.Quiz,
                    DefaultWeight = c.Weight
                });
                weights[c.Key] = Math.Max(0, c.Weight);
                customByKey[c.Key] = c;
            }

            return new Effective(
                s?.Enabled ?? _options.Enabled,
                s?.TriggerChance ?? _options.TriggerChance,
                s?.CooldownMinutes ?? _options.CooldownMinutes,
                s?.ApplyToAdmins ?? _options.ApplyToAdmins,
                s?.MercyAfterAttempts ?? _options.MercyAfterAttempts,
                s?.MathMaxOperand ?? _options.MathMaxOperand,
                catalog, weights, customByKey);
        }
        catch
        {
            // Fail-open: DB-Problem darf den Login NIE blockieren.
            return OptionsOnly();
        }
    }

    public void InvalidateCache() => _cache?.Remove(CacheKey);

    // ───────────────────────── Konfig-Spiegelung ─────────────────────────

    public bool IsEnabled => GetEffective().Enabled;
    public TimeSpan Cooldown => TimeSpan.FromMinutes(Math.Max(0, GetEffective().CooldownMinutes));
    public int MercyAfterAttempts => Math.Max(1, GetEffective().MercyAfterAttempts);
    public bool AppliesToAdmins => GetEffective().ApplyToAdmins;

    // ───────────────────────── Auslöse-Würfel ─────────────────────────

    public bool RollShouldTrigger() => ShouldTriggerForRoll(_rng.NextDouble());

    /// <summary>Pure Auslöse-Entscheidung für einen gegebenen Wurf <paramref name="roll"/> ∈ [0,1).</summary>
    public bool ShouldTriggerForRoll(double roll)
    {
        var e = GetEffective();
        return e.Enabled && roll < e.TriggerChance;
    }

    // ───────────────────────── Gewichtete Auswahl ─────────────────────────

    public TrollGagDefinition PickGag()
    {
        var e = GetEffective();
        var total = 0;
        foreach (var g in e.Catalog) total += WeightOf(g, e);
        if (total <= 0)
        {
            // Alles deaktiviert → Fallback, damit der Aufrufer nie null bekommt.
            return FindGag("math") ?? BuiltIns[0];
        }
        return PickFrom(e, _rng.Next(total));
    }

    /// <summary>
    /// Pure gewichtete Auswahl über den <b>effektiven</b> Katalog:
    /// <paramref name="roll"/> ∈ [0, Summe aktiver Gewichte). Gags mit Gewicht 0 sind ausgeschlossen.
    /// </summary>
    public TrollGagDefinition PickGagForRoll(int roll) => PickFrom(GetEffective(), roll);

    private static TrollGagDefinition Fallback(Effective e)
        => e.Catalog.Count > 0 ? e.Catalog[0] : BuiltIns[0];

    private TrollGagDefinition PickFrom(Effective e, int roll)
    {
        var cursor = 0;
        TrollGagDefinition? last = null;
        foreach (var gag in e.Catalog)
        {
            var weight = WeightOf(gag, e);
            if (weight <= 0) continue;
            last = gag;
            cursor += weight;
            if (roll < cursor) return gag;
        }
        return last ?? Fallback(e);
    }

    public TrollGagDefinition? FindGag(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        // Erst im effektiven Katalog (inkl. aktiver Custom-Gags), dann Fallback auf alle
        // eingebauten – damit auch deaktivierte eingebaute Gags noch per Vorschau auffindbar sind.
        return GetEffective().Catalog.FirstOrDefault(g => g.Key == key)
               ?? Array.Find(BuiltIns, g => g.Key == key);
    }

    public TrollCustomGag? FindCustomGag(string? key)
        => string.IsNullOrWhiteSpace(key)
            ? null
            : (GetEffective().Custom.TryGetValue(key, out var c) ? c : null);

    // ───────────────────────── Challenge-Erzeugung ─────────────────────────

    public TrollChallenge BuildChallenge(TrollGagDefinition gag)
    {
        var e = GetEffective();

        // Admin-erstellte Gags: Quiz liefert Frage/Antwort, Nachricht hat keine Challenge.
        if (e.Custom.TryGetValue(gag.Key, out var custom))
        {
            return custom.Kind == TrollCustomGagKind.Quiz
                ? new TrollChallenge { GagKey = gag.Key, Prompt = custom.Question, ExpectedAnswer = custom.Answer }
                : new TrollChallenge { GagKey = gag.Key };
        }

        switch (gag.Key)
        {
            case "math":
            {
                // Nur einstellige Additionsaufgaben: a + b mit a,b ∈ 1..MathMaxOperand (max. 9).
                var max = Math.Clamp(e.MathMaxOperand, 1, 9);
                var a = _rng.Next(1, max + 1);
                var b = _rng.Next(1, max + 1);
                return new TrollChallenge
                {
                    GagKey = gag.Key,
                    Prompt = $"{a} + {b}",
                    ExpectedAnswer = (a + b).ToString()
                };
            }
            case "best-driver":
                return new TrollChallenge { GagKey = gag.Key, ExpectedAnswer = "<OWNER_HANDLE>" };
            case "not-a-bot":
                return new TrollChallenge { GagKey = gag.Key, ExpectedAnswer = "car" };
            case "f1-trivia":
            {
                // Zufällige Frage aus der statischen Bank; das Partial rendert die Optionen
                // anhand des gespeicherten Prompts (Fragetext) wieder auf.
                var q = TrollTrivia.Questions[_rng.Next(TrollTrivia.Questions.Count)];
                return new TrollChallenge
                {
                    GagKey = gag.Key,
                    Prompt = q.Question,
                    ExpectedAnswer = q.Answer
                };
            }
            case "sequence":
            {
                // Arithmetische Folge: 4 sichtbare Glieder a, a+d, a+2d, a+3d → gesucht ist a+4d.
                var a = _rng.Next(1, 7);
                var d = _rng.Next(2, 7);
                var shown = new[] { a, a + d, a + 2 * d, a + 3 * d };
                return new TrollChallenge
                {
                    GagKey = gag.Key,
                    Prompt = string.Join(", ", shown),
                    ExpectedAnswer = (a + 4 * d).ToString()
                };
            }
            case "oath-type":
            {
                // Treueschwur muss exakt abgetippt werden (IsAnswerCorrect trimmt + ignoriert Groß-/Kleinschreibung).
                const string oath = "<OWNER_HANDLE> ist der schnellste Fahrer der Welt";
                return new TrollChallenge { GagKey = gag.Key, Prompt = oath, ExpectedAnswer = oath };
            }
            default:
                return new TrollChallenge { GagKey = gag.Key };
        }
    }

    public bool IsAnswerCorrect(string? expected, string? userAnswer)
    {
        if (string.IsNullOrWhiteSpace(expected)) return false;
        return string.Equals(expected.Trim(), userAnswer?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private int WeightOf(TrollGagDefinition gag, Effective e)
    {
        if (e.Weights.TryGetValue(gag.Key, out var w)) return Math.Max(0, w);
        if (_options.Weights.TryGetValue(gag.Key, out var ow)) return Math.Max(0, ow);
        return gag.DefaultWeight;
    }
}
