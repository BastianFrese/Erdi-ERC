using System.Text.RegularExpressions;

namespace Erdi_ERC.Tests.Frontend;

/// <summary>
/// Prueft den technischen Vertrag der neuen Gestalt. Diese Tests sind absichtlich
/// ueber Dateien und nicht ueber gerenderte Seiten formuliert: Program.cs fuehrt beim
/// Start Migrationen aus, ein WebApplicationFactory&lt;Program&gt; ist daher nicht moeglich
/// (siehe SmokeTests.cs). Die visuelle Pruefung bleibt manuell im Browser.
/// </summary>
public class DesignContractTests
{
    public static string RepoRoot { get; } = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Erdi-ERC.slnx")))
            dir = dir.Parent;
        if (dir is null) throw new InvalidOperationException("Repo-Wurzel (Erdi-ERC.slnx) nicht gefunden.");
        return dir.FullName;
    }

    private static string WwwRoot => Path.Combine(RepoRoot, "Erdi-ERC", "wwwroot");
    private static string Csproj => Path.Combine(RepoRoot, "Erdi-ERC", "Erdi-ERC.csproj");

    /// <summary>
    /// Quelldateien unter wwwroot/css. Generierte Artefakte sind ausgenommen:
    /// .min.css sind Minify-Ausgaben, site.bundled.css ist die Konkatenation
    /// derselben Partials — beide wuerden jede Abweichung doppelt melden.
    /// </summary>
    private static IEnumerable<string> AlleCssDateien() =>
        Directory.EnumerateFiles(Path.Combine(WwwRoot, "css"), "*.css", SearchOption.AllDirectories)
                 .Where(p => !p.EndsWith(".min.css", StringComparison.OrdinalIgnoreCase))
                 .Where(p => Path.GetFileName(p) != "site.bundled.css");

    private static string Lies(string pfad) => File.ReadAllText(pfad);

    // --- Bundling ---------------------------------------------------------

    [Fact]
    public void Jede_Css_Partial_Steht_Im_Bundle()
    {
        var csproj = Lies(Csproj);

        // Beide Listen im Target muessen existieren und identisch sein.
        // Das Target heisst <Target Name="BundleSiteCss" ...>, nicht <BundleSiteCss ...>.
        var inputs = Regex.Matches(csproj, @"<Target\s+Name=""BundleSiteCss""[^>]*Inputs=""([^""]+)""")
                          .SelectMany(m => m.Groups[1].Value.Split(';'))
                          .Select(s => s.Trim().Replace('\\', '/'))
                          .Where(s => s.Length > 0).ToArray();
        var includes = Regex.Matches(csproj, @"<_CssBundle Include=""([^""]+)""")
                            .Select(m => m.Groups[1].Value.Trim().Replace('\\', '/')).ToArray();

        Assert.NotEmpty(inputs);
        Assert.NotEmpty(includes);
        Assert.Equal(inputs.OrderBy(p => p), includes.OrderBy(p => p));

        // Jede Datei unter css/site/ muss in der Liste stehen, sonst wird sie
        // stillschweigend nicht gebuendelt. Das Bundle enthaelt daneben auch
        // Dateien ausserhalb von css/site/ — die werden hier nicht verglichen.
        var aufPlatte = Directory.EnumerateFiles(Path.Combine(WwwRoot, "css", "site"), "*.css")
                                 .Select(Path.GetFileName)
                                 .Where(n => n is not null && !n.EndsWith(".min.css", StringComparison.OrdinalIgnoreCase))
                                 .Select(n => n!)
                                 .OrderBy(n => n)
                                 .ToArray();
        var imBundleSite = inputs.Where(p => p.StartsWith("wwwroot/css/site/"))
                                 .Select(p => Path.GetFileName(p))
                                 .OrderBy(n => n)
                                 .ToArray();

        Assert.Equal(aufPlatte, imBundleSite);
    }

    [Fact]
    public void Bundle_Liste_Enthaelt_Keine_Fehlenden_Dateien()
    {
        var csproj = Lies(Csproj);
        var includes = Regex.Matches(csproj, @"<_CssBundle Include=""([^""]+)""")
                            .Select(m => m.Groups[1].Value.Trim());
        foreach (var rel in includes)
        {
            var voll = Path.Combine(RepoRoot, "Erdi-ERC", rel.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(voll), $"Im Bundle referenziert, aber nicht vorhanden: {rel}");
        }
    }

    // --- Verbotene Eigenschaften und Selektoren ---------------------------

    /// <summary>
    /// Kommentare muessen aufgehen.
    ///
    /// Ein "*/" IM Kommentartext beendet ihn vorzeitig. Der Rest des Textes wird
    /// dann als CSS gelesen, und alle Deklarationen bis zum naechsten "*/" fallen
    /// still weg — kein Fehler, keine Warnung, die Regel wirkt einfach nicht.
    ///
    /// Genau das ist passiert: in einem Kommentar stand "--sig-*/--flag/--best".
    /// Von den fünf --on-*-Tokens kam nur der erste nicht beim Browser an; die
    /// anderen vier ueberlebten zufaellig, weil der Parser sich danach wieder
    /// fing. Der Dateitest hat es nicht gesehen, weil er den Text liest: fuer ihn
    /// stand die Deklaration da. Deshalb hier ein Test auf den Kommentarzaehler.
    /// </summary>
    [Fact]
    public void Css_Kommentare_Gehen_Auf()
    {
        var treffer = new List<string>();
        foreach (var datei in AlleCssDateien())
        {
            var text = Lies(datei);
            var auf = Regex.Matches(text, @"/\*").Count;
            var zu = Regex.Matches(text, @"\*/").Count;
            if (auf != zu)
                treffer.Add($"{Path.GetFileName(datei)}: {auf}x /* aber {zu}x */");
            else if (Regex.IsMatch(Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline), @"\*/"))
                treffer.Add($"{Path.GetFileName(datei)}: ein Kommentar endet zu frueh (\"*/\" im Kommentartext)");
        }
        Assert.True(treffer.Count == 0,
            "Kommentare in CSS gehen nicht auf — alles dahinter wirkt nicht:\n  " +
            string.Join("\n  ", treffer));
    }

    /// <summary>
    /// Genau ein Stylesheet-Request pro Seite. Der Layout laedt das Bundle;
    /// jede weitere ~/css-Datei aus einer View waere ein zweiter, render-blockierender
    /// Request fuer Regeln, die schon im Bundle stehen.
    /// </summary>
    [Fact]
    public void Keine_View_Laedt_Ein_Eigenes_Stylesheet()
    {
        var views = Path.Combine(RepoRoot, "Erdi-ERC", "Views");
        var uebrig = new List<string>();

        foreach (var datei in Directory.EnumerateFiles(views, "*.cshtml", SearchOption.AllDirectories))
        {
            var treffer = Regex.Matches(Lies(datei), @"<link[^>]+rel=""stylesheet""[^>]+href=""([^""]+)""");
            foreach (Match m in treffer)
            {
                var href = m.Groups[1].Value;
                if (!href.StartsWith("~/css/site.bundled", StringComparison.OrdinalIgnoreCase))
                    uebrig.Add($"{Path.GetRelativePath(RepoRoot, datei)} -> {href}");
            }
        }

        Assert.True(uebrig.Count == 0,
            "Views laden ein eigenes Stylesheet statt nur des Bundles:\n" + string.Join("\n", uebrig));
    }

    [Fact]
    public void Kein_Backdrop_Filter_Mehr()
    {
        foreach (var datei in AlleCssDateien())
            Assert.DoesNotContain("backdrop-filter", Lies(datei), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("f1-speedline")]
    [InlineData("f1-spark")]
    [InlineData("chequered-sweep")]
    // race-lights__chequers, __scan und __speedlines standen hier auch.
    // Sie gehoeren zum Lade-Überlagerer (Karomuster, Scanlinie, Speedlines
    // hinter dem Formel-Auto) und sind auf Wunsch zurückgekommen: dieser eine
    // geplante Moment beim Laden ist genau der Fall, für den die Gestaltung
    // Bewegung und Effekte zulässt — gestreut über die Seite wären sie das
    // Problem, das die Regel meint. Die übrigen zehn bleiben verboten.
    [InlineData("f1-border-anim")]
    [InlineData("f1-holographic")]
    [InlineData("f1-lift")]
    [InlineData("f1-tiltable")]
    [InlineData("f1-home-backdrop")]
    [InlineData("page-transition")]
    [InlineData("scroll-progress")]
    public void Verbotene_Klasse_Ist_Verschwunden(string klasse)
    {
        foreach (var datei in AlleCssDateien())
            Assert.DoesNotMatch($@"\.{Regex.Escape(klasse)}(?![\w-])", Lies(datei));

        foreach (var view in Directory.EnumerateFiles(Path.Combine(RepoRoot, "Erdi-ERC", "Views"),
                                                      "*.cshtml", SearchOption.AllDirectories))
            Assert.DoesNotContain(klasse, Lies(view), StringComparison.Ordinal);
    }

    // --- Fremdanfragen ----------------------------------------------------

    [Theory]
    [InlineData("fonts.googleapis.com")]
    [InlineData("fonts.gstatic.com")]
    [InlineData("cdn.jsdelivr.net")]
    public void Keine_Anfrage_An_Externe_Domain(string domain)
    {
        foreach (var layout in new[]
                 {
                     Path.Combine(RepoRoot, "Erdi-ERC", "Views", "Shared", "_Layout.cshtml"),
                     Path.Combine(RepoRoot, "Erdi-ERC", "Views", "Admin", "_AdminLayout.cshtml"),
                 })
            Assert.DoesNotContain(domain, Lies(layout), StringComparison.OrdinalIgnoreCase);

        foreach (var datei in AlleCssDateien())
            Assert.DoesNotContain(domain, Lies(datei), StringComparison.OrdinalIgnoreCase);
    }

    // --- Der Ueberlagerer darf den Inhalt nicht verstecken ---------------

    [Fact]
    public void AppMain_Wird_Nicht_Versteckt()
    {
        var layout = Lies(Path.Combine(RepoRoot, "Erdi-ERC", "Views", "Shared", "_Layout.cshtml"));
        Assert.DoesNotMatch(@"appMain'\)\s*\.style\.opacity", layout);
        Assert.DoesNotMatch(@"main\.style\.opacity\s*=\s*'0'", layout);
    }

    // --- Nichts darf dauerhaft unsichtbar sein ----------------------------

    /// <summary>
    /// Verlaufs-Textfuellung: Schrift wird durchsichtig gestellt und der Verlauf
    /// scheint durch sie hindurch. Faellt der Verlauf weg, bleibt durchsichtige
    /// Schrift ohne Inhalt — unsichtbarer Text, und nichts meldet es.
    ///
    /// Genau das ist beim Umbau passiert: die Gradient-Tokens wurden auf `none`
    /// gesetzt (die Spec verbietet Verlaeufe als Dekoration), aber die
    /// background-clip-Regeln blieben stehen. Die Kachelwerte auf drei Seiten
    /// standen danach im DOM und waren durchsichtig.
    ///
    /// Die Spec schliesst Verlaufs-Dekoration aus, also ist die Technik selbst
    /// verboten — nicht nur ihr fehlender Verlauf.
    /// </summary>
    [Fact]
    public void Keine_Verlaufs_Textfuellung()
    {
        var treffer = new List<string>();
        foreach (var datei in AlleCssDateien())
        {
            var text = Regex.Replace(Lies(datei), @"/\*.*?\*/", "", RegexOptions.Singleline);
            foreach (Match m in Regex.Matches(text, @"(?<sel>[^{}]*)\{(?<body>[^{}]*)\}"))
            {
                var rumpf = m.Groups["body"].Value;
                var clip = Regex.IsMatch(rumpf, @"(-webkit-)?background-clip\s*:\s*text", RegexOptions.IgnoreCase);
                var durchsichtig = Regex.IsMatch(rumpf, @"(-webkit-)?text-fill-color\s*:\s*transparent", RegexOptions.IgnoreCase);
                if (clip || durchsichtig)
                    treffer.Add($"{Path.GetFileName(datei)}: '{m.Groups["sel"].Value.Trim()}'");
            }
        }
        Assert.True(treffer.Count == 0,
            "Verlaufs-Textfuellung — ohne Verlauf bleibt der Text unsichtbar:\n  " +
            string.Join("\n  ", treffer));
    }

    /// <summary>
    /// Der Layout-Test oben sieht nur die beiden Hüllen. Eine Fremdanfrage kann aber
    /// aus jeder View kommen — genau das war der Fall: die Ergebnisseite lud ihr
    /// Bahrain-Fähnchen von flagcdn.com, obwohl 45 Flaggen lokal liegen. Deshalb
    /// hier ALLE Views.
    /// </summary>
    /// <summary>
    /// Schriftfarbe, die auf ihrer eigenen Hintergrundfarbe steht.
    ///
    /// Der Fall, der das ausgeloest hat: die Constructors-Kopfzeile setzt
    ///   background: linear-gradient(90deg, var(--accent-1, var(--slow)), var(--accent-2, var(--wet)));
    ///   color: var(--ink);
    /// --accent-1 gibt es im neuen Token-Satz nicht mehr, also griff der Rueckfall
    /// auf --slow, und --accent-2 zeigt auf --ink (dieselbe Farbe wie der Text).
    /// Der Text stand damit auf seiner eigenen Farbe — unlesbar, ohne Fehlermeldung.
    /// Die beiden Namen sind verschieden, die aufgeloesten Werte nicht; deshalb
    /// loest der Test die var()-Kette auf, statt Namen zu vergleichen.
    /// </summary>
    [Fact]
    public void Textfarbe_Steht_Nicht_Auf_Ihrer_Eigenen_Hintergrundfarbe()
    {
        var token = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var datei in AlleCssDateien())
            foreach (Match m in Regex.Matches(Lies(datei), @"(--[a-zA-Z0-9-]+)\s*:\s*([^;{}]+);"))
                token.TryAdd(m.Groups[1].Value, m.Groups[2].Value.Trim());

        string Aufloesen(string wert)
        {
            for (var i = 0; i < 8 && wert.Contains("var(", StringComparison.OrdinalIgnoreCase); i++)
            {
                wert = Regex.Replace(wert,
                    @"var\(\s*(--[a-zA-Z0-9-]+)\s*(?:,\s*([^()]*(?:\([^()]*\)[^()]*)*))?\)",
                    m =>
                    {
                        var name = m.Groups[1].Value;
                        if (token.TryGetValue(name, out var ersetzt)) return ersetzt;
                        return m.Groups[2].Success ? m.Groups[2].Value.Trim() : m.Value;
                    });
            }
            return wert.Trim().ToUpperInvariant();
        }

        // Farbstopps am Komma auf oberster Ebene trennen — in den Klammern stecken
        // selbst wieder Klammern (var(--x, var(--y)), color-mix(...)), an denen
        // ein einfacher Ausdruck zerbricht. Der letzte Stopp darf die schliessende
        // Klammer des gradient()-Aufrufs nicht mitnehmen: sonst loest er sich zu
        // "#F2F4F7)" auf und der Vergleich schlaegt still fehl.
        static IEnumerable<string> Stopps(string verlauf)
        {
            var tiefe = 0;
            var start = 0;
            var ende = verlauf.Length;
            if (verlauf.TrimStart().StartsWith("gradient(", StringComparison.Ordinal))
            {
                var auf = verlauf.IndexOf('(') + 1;
                var t = 1;
                for (var i = auf; i < verlauf.Length && t > 0; i++)
                {
                    if (verlauf[i] == '(') t++;
                    else if (verlauf[i] == ')') t--;
                    if (t == 0) { ende = i; break; }
                }
                start = auf;
            }
            for (var i = start; i < ende; i++)
            {
                if (verlauf[i] == '(') tiefe++;
                else if (verlauf[i] == ')') tiefe--;
                else if (verlauf[i] == ',' && tiefe == 0)
                {
                    yield return verlauf[start..i];
                    start = i + 1;
                }
            }
            yield return verlauf[start..ende];
        }

        var treffer = new List<string>();
        foreach (var datei in AlleCssDateien())
        {
            var text = Regex.Replace(Lies(datei), @"/\*.*?\*/", "", RegexOptions.Singleline);
            foreach (Match m in Regex.Matches(text, @"(?<sel>[^{}]*)\{(?<body>[^{}]*)\}"))
            {
                var rumpf = m.Groups["body"].Value;
                var farbe = Regex.Match(rumpf, @"(?<!-)color\s*:\s*([^;]+);");
                var verlauf = Regex.Match(rumpf, @"background[^:;]*:\s*[^;]*?(?<stops>gradient\([^;]+);");
                if (!farbe.Success || !verlauf.Success) continue;

                var textfarbe = Aufloesen(farbe.Groups[1].Value);
                // Nur reine Farbangaben vergleichen. Ein color-mix(...)-Stopp hat
                // keine einzelne Entsprechung — er kann nicht gleich sein.
                var halte = Stopps(verlauf.Groups["stops"].Value)
                            .Select(s => Regex.Replace(s, @"^\s*[-0-9.]+(deg|rad|turn|%|px)\s*", "").Trim())
                            .Where(s => Regex.IsMatch(s, @"^(#[0-9a-fA-F]{3,8}|var\(.*\))$"))
                            .Select(Aufloesen);
                if (halte.Any(h => h == textfarbe))
                    treffer.Add($"{Path.GetFileName(datei)}: '{m.Groups["sel"].Value.Trim()}' — {textfarbe}");
            }
        }
        Assert.True(treffer.Count == 0,
            "Text steht auf seiner eigenen Farbe:\n  " + string.Join("\n  ", treffer));
    }

    /// <summary>
    /// Schrift auf einer Flaeche braucht 4,5:1 (grosse Schrift 3:1).
    ///
    /// Der Gleichheitstest oben findet nur "genau dieselbe Farbe". Hier steht der
    /// haeufigere Fall: Schrift und Flaeche sind verschieden, liegen aber zu nah
    /// beieinander. Beim Umbau stand "Schrift auf einer Signalfarbe" ueberall als
    /// color:var(--track) oder var(--ink) in den Regeln — beide Tokens kippen mit
    /// dem Design, und das Signal kippt ebenfalls (gelb wird senf, hellgruen wird
    /// dunkelgruen). Zweimal kippen ergibt zweimal falsch: im dunklen Design
    /// unlesbar, im hellen gerade noch — oder umgekehrt.
    ///
    /// Geprueft werden beide Token-Saetze; der Gleichheitstest kennt nur einen
    /// und ist deshalb in der Haelfte der Faelle blind.
    /// </summary>
    [Fact]
    public void Schrift_Und_Flaeche_Haben_Genug_Kontrast()
    {
        var tokenDatei = Lies(Path.Combine(WwwRoot, "css", "site", "01-tokens.css"));

        // Token-Saetze wie der Browser sie aufloest: [data-theme="light"] und die
        // :root-Bloecke haben dieselbe Spezifitaet, also entscheidet allein die
        // Reihenfolge im Quelltext. Ein Block wird deshalb in der Reihenfolge
        // abgespielt, in der er dasteht: :root schreibt in beide Saetze, der
        // helle Block nur in den hellen. "Erst alle :root, dann alle hell" waere
        // falsch herum — genau daran war ein toter heller Override nicht zu sehen.
        var dunkel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var hell = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match block in Regex.Matches(tokenDatei, @"(?<sel>[^{}]+)\{(?<body>[^{}]*)\}"))
        {
            var selektor = block.Groups["sel"].Value;
            var istRoot = Regex.IsMatch(selektor, @":root(?![\w-])");
            var istHell = selektor.Contains("data-theme=\"light\"", StringComparison.Ordinal);
            if (!istRoot && !istHell) continue;

            foreach (var kv in Tokens(block.Groups["body"].Value))
            {
                if (istRoot) dunkel[kv.Key] = kv.Value;
                if (istRoot || istHell) hell[kv.Key] = kv.Value;
            }
        }

        // Tokens stehen nicht nur in der Token-Datei: --team-primary etwa wird in
        // 11-unique-addons.css auf --accent gesetzt. Ohne diese Definition haette
        // der Test den Rueckfall (--flag) bewertet statt der echten Farbe.
        foreach (var datei in AlleCssDateien())
            foreach (var kv in Tokens(Lies(datei)))
            {
                dunkel.TryAdd(kv.Key, kv.Value);
                hell.TryAdd(kv.Key, kv.Value);
            }

        Assert.True(dunkel.ContainsKey("--ink") && hell.ContainsKey("--ink")
                    && dunkel["--ink"] != hell["--ink"],
            "Die beiden Token-Saetze liessen sich nicht auseinanderhalten — der Test prueft sonst zweimal dasselbe.");

        // Und je Team: body.team-* setzt --team/--team-fill/--on-team. Ohne diese
        // Runde wuerde jede Regel mit var(--accent) nur gegen den Rueckfall
        // (--flag) geprueft, und die elf echten Teamfarben nie.
        var teams = new List<(string Name, Dictionary<string, string> Tokens)>();
        foreach (Match m in Regex.Matches(tokenDatei, @"body\.team-([a-z]+)[^{}]*\{(?<body>[^{}]*)\}"))
        {
            var zusatz = Tokens(m.Groups["body"].Value);
            if (!zusatz.ContainsKey("--team")) continue;
            teams.Add((m.Groups[1].Value, zusatz));
        }
        Assert.True(teams.Count >= 10, $"Nur {teams.Count} Teamfarben gefunden — body.team-* fehlt?");

        // Schrift auf der Teamfarbe muss je Team messbar lesbar sein. Das ist die
        // Stelle, an der eine neue Teamfarbe sonst still unlesbar dazukommt.
        var teamPaare = new List<string>();
        foreach (var (name, zusatz) in teams)
        {
            var fill = Farbe(Aufloesen(zusatz["--team-fill"], dunkel));
            var schrift = Farbe(Aufloesen(zusatz["--on-team"], dunkel));
            if (fill is null || schrift is null)
            {
                teamPaare.Add($"{name}: --team-fill/--on-team nicht aufloesbar (durchsichtig?)");
                continue;
            }
            var wert = Kontrast(schrift.Value, fill.Value);
            if (wert < 4.5) teamPaare.Add($"{name}: {wert:F2}:1 (--on-team auf --team-fill)");
        }
        Assert.True(teamPaare.Count == 0,
            "Schrift auf der Teamfarbe zu schwach:\n  " + string.Join("\n  ", teamPaare));

        var saetze = new List<(string Name, Dictionary<string, string> Tokens)>();
        foreach (var (themaName, basis) in new[] { ("dunkel", dunkel), ("hell", hell) })
        {
            saetze.Add((themaName, basis));
            foreach (var (teamName, zusatz) in teams)
            {
                var gemischt = new Dictionary<string, string>(basis, StringComparer.OrdinalIgnoreCase);
                foreach (var kv in zusatz) gemischt[kv.Key] = kv.Value;
                saetze.Add(($"{themaName}/{teamName}", gemischt));
            }
        }

        // Dateien einmal lesen statt je Durchlauf: mit den Teams sind es 24 Durchlaeufe.
        var quellen = AlleCssDateien()
            .Select(p => (Name: Path.GetFileName(p), Text: Regex.Replace(Lies(p), @"/\*.*?\*/", "", RegexOptions.Singleline)))
            .ToList();

        var treffer = new List<string>();
        foreach (var (datei, text) in quellen)
        {
            foreach (Match m in Regex.Matches(text, @"(?<sel>[^{}]*)\{(?<body>[^{}]*)\}"))
            {
                var rumpf = m.Groups["body"].Value;
                var farbMatch = Regex.Match(rumpf, @"(?<!-)color\s*:\s*([^;]+);");
                var flaecheMatch = Regex.Match(rumpf, @"background(?:-color|-image)?\s*:\s*([^;]+);");
                if (!farbMatch.Success || !flaecheMatch.Success) continue;

                foreach (var (name, tokens) in saetze)
                {
                    if (Farbe(Aufloesen(farbMatch.Groups[1].Value, tokens)) is not { } textfarbe) continue;

                    // Das schlechteste Ende eines Verlaufs zaehlt: dort ist der Text
                    // am wenigsten zu sehen. Durchsichtige Flaechen fallen heraus —
                    // ihre Wirkung haengt an der Unterlage, die hier nicht steht.
                    var flaechen = Stopps(flaecheMatch.Groups[1].Value)
                        .Select(s => Farbe(Aufloesen(s, tokens)))
                        .Where(f => f is not null).Select(f => f!.Value).ToList();
                    if (flaechen.Count == 0) continue;

                    var schlecht = flaechen.Min(f => Kontrast(textfarbe, f));
                    if (schlecht < Schwelle(rumpf))
                    {
                        var flaeche = flaecheMatch.Groups[1].Value.Trim();
                        treffer.Add($"{datei} [{name}] {schlecht:F2}:1 — " +
                                    $"'{m.Groups["sel"].Value.Trim()}': {farbMatch.Groups[1].Value.Trim()} auf " +
                                    flaeche[..Math.Min(46, flaeche.Length)]);
                    }
                }
            }
        }
        Assert.True(treffer.Count == 0,
            "Zu wenig Kontrast zwischen Schrift und Flaeche:\n  " + string.Join("\n  ", treffer.Distinct()));
    }

    private static Dictionary<string, string> Tokens(string block)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(block, @"(--[a-zA-Z0-9-]+)\s*:\s*([^;{}]+);"))
            map.TryAdd(m.Groups[1].Value, m.Groups[2].Value.Trim());
        return map;
    }

    private static string Aufloesen(string wert, Dictionary<string, string> tokens)
    {
        for (var i = 0; i < 8 && wert.Contains("var(", StringComparison.OrdinalIgnoreCase); i++)
        {
            var vorher = wert;
            wert = Regex.Replace(wert,
                @"var\(\s*(--[a-zA-Z0-9-]+)\s*(?:,\s*([^()]*(?:\([^()]*\)[^()]*)*))?\)",
                m => tokens.TryGetValue(m.Groups[1].Value, out var ersetzt)
                     ? ersetzt
                     : m.Groups[2].Success ? m.Groups[2].Value.Trim() : m.Value);
            if (wert == vorher) break;
        }
        return wert.Trim();
    }

    /// <summary>Farbwert, oder null bei durchsichtigen und unaufloesbaren Angaben.</summary>
    private static (double R, double G, double B)? Farbe(string wert)
    {
        wert = wert.Trim();
        if (Regex.Match(wert, @"^#([0-9a-fA-F]{3})$") is { Success: true } kurz)
        {
            var s = kurz.Groups[1].Value;
            return (Convert.ToInt32($"{s[0]}{s[0]}", 16),
                    Convert.ToInt32($"{s[1]}{s[1]}", 16),
                    Convert.ToInt32($"{s[2]}{s[2]}", 16));
        }
        if (Regex.Match(wert, @"^#([0-9a-fA-F]{6})$") is { Success: true } lang)
        {
            var s = lang.Groups[1].Value;
            return (Convert.ToInt32(s[..2], 16), Convert.ToInt32(s.Substring(2, 2), 16), Convert.ToInt32(s.Substring(4, 2), 16));
        }
        if (Regex.Match(wert, @"^rgba?\(\s*([\d.]+)[,\s]+([\d.]+)[,\s]+([\d.]+)\s*(?:[/,]\s*([\d.]+%?))?\s*\)$")
            is not { Success: true } rgb) return null;

        if (rgb.Groups[4].Success)
        {
            var roh = rgb.Groups[4].Value;
            var prozent = roh.EndsWith('%');
            if (!double.TryParse(prozent ? roh.TrimEnd('%') : roh,
                                 System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out var a)) return null;
            if ((prozent ? a / 100 : a) < 1) return null;   // durchsichtig -> Unterlage unbekannt
        }
        return (Zahl(rgb.Groups[1].Value), Zahl(rgb.Groups[2].Value), Zahl(rgb.Groups[3].Value));
    }

    private static double Zahl(string s) =>
        double.Parse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);

    private static double Luminanz((double R, double G, double B) c)
    {
        static double Kanal(double v)
        {
            v /= 255;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Kanal(c.R) + 0.7152 * Kanal(c.G) + 0.0722 * Kanal(c.B);
    }

    private static double Kontrast((double R, double G, double B) a, (double R, double G, double B) b)
    {
        var hell = Math.Max(Luminanz(a), Luminanz(b));
        var dunkel = Math.Min(Luminanz(a), Luminanz(b));
        return (hell + 0.05) / (dunkel + 0.05);
    }

    /// <summary>WCAG: ab 24 px, oder ab 18,66 px fett, genuegen 3:1. Gilt auch fuer die font-Kurzform.</summary>
    private static double Schwelle(string rumpf)
    {
        var groesse = Regex.Match(rumpf, @"(?:font-size\s*:|font\s*:\s*(?:\d{3}\s+)?)\s*([\d.]+)(rem|px)");
        if (!groesse.Success) return 4.5;
        var px = Zahl(groesse.Groups[1].Value) * (groesse.Groups[2].Value == "rem" ? 16 : 1);
        var gewicht = Regex.Match(rumpf, @"(?:font-weight\s*:|font\s*:\s*)(\d{3})");
        var fett = gewicht.Success && int.Parse(gewicht.Groups[1].Value) >= 700;
        return px >= 24 || (fett && px >= 18.66) ? 3.0 : 4.5;
    }

    /// <summary>Farbangaben einer Flaeche: feste Farbe oder die Enden eines Verlaufs.</summary>
    private static IEnumerable<string> Stopps(string wert)
    {
        var i = wert.IndexOf("gradient(", StringComparison.Ordinal);
        if (i < 0) { yield return wert; yield break; }

        var auf = wert.IndexOf('(', i);
        var tiefe = 1;
        var ende = wert.Length;
        for (var j = auf + 1; j < wert.Length; j++)
        {
            if (wert[j] == '(') tiefe++;
            else if (wert[j] == ')' && --tiefe == 0) { ende = j; break; }
        }

        var rumpf = wert[(auf + 1)..ende];
        var teile = new List<string>();
        var t = 0;
        var start = 0;
        for (var j = 0; j < rumpf.Length; j++)
        {
            if (rumpf[j] == '(') t++;
            else if (rumpf[j] == ')') t--;
            else if (rumpf[j] == ',' && t == 0) { teile.Add(rumpf[start..j]); start = j + 1; }
        }
        teile.Add(rumpf[start..]);

        foreach (var teil in teile)
        {
            // Positionsangaben stehen vor UND hinter der Farbe ("135deg, ... 0%").
            var s = Regex.Replace(teil, @"^\s*[-0-9.]+(deg|rad|turn|grad)\s*", "");
            s = Regex.Replace(s, @"\s+[-0-9.]+(%|px|ch|em|rem)?\s*$", "");
            yield return s.Trim();
        }
    }

    /// <summary>
    /// Keine View darf eine Bootstrap-Klasse benutzen, die beim Kuerzen weggefallen ist.
    ///
    /// 00-bootstrap.css enthaelt nur die Klassen, die im Markup vorkamen, plus die
    /// Zustandsklassen aus dem Purge-Skript. Schreibt jemand spaeter eine weitere
    /// Bootstrap-Klasse ins Markup, fehlt die Regel — und zwar lautlos: das Element
    /// steht da, sieht nur falsch aus. Kein Fehler, keine Warnung.
    ///
    /// Genau das ist passiert: die neue Infotafel-Reihe trug "d-none d-lg-grid".
    /// d-lg-grid stand beim Kuerzen in keiner View, war also nicht in der gekuerzten
    /// Datei; "d-none" gewann, und die ganze Reihe blieb unsichtbar.
    /// </summary>
    [Fact]
    public void Keine_View_Benutzt_Eine_Weggekuerzte_Bootstrap_Klasse()
    {
        var voll = Path.Combine(WwwRoot, "lib", "bootstrap", "dist", "css", "bootstrap.min.css");
        if (!File.Exists(voll)) return;   // Bootstrap-Quelle entfernt — nichts zu vergleichen

        var vollKlassen = CssKlassen(Lies(voll));
        var gekuerzt = CssKlassen(Lies(Path.Combine(WwwRoot, "css", "site", "00-bootstrap.css")));

        // Was wir selbst gestalten, ist erlaubt, auch wenn der Name zufaellig auch
        // in Bootstrap vorkommt.
        var eigene = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var datei in AlleCssDateien())
            if (!Path.GetFileName(datei).Equals("00-bootstrap.css", StringComparison.OrdinalIgnoreCase))
                eigene.UnionWith(CssKlassen(Lies(datei)));

        var treffer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var view in Directory.EnumerateFiles(Path.Combine(RepoRoot, "Erdi-ERC", "Views"),
                                                     "*.cshtml", SearchOption.AllDirectories))
            foreach (var token in MarkupKlassen(Lies(view)))
                if (vollKlassen.Contains(token) && !gekuerzt.Contains(token) && !eigene.Contains(token))
                    treffer.TryAdd(token, Path.GetRelativePath(RepoRoot, view));

        Assert.True(treffer.Count == 0,
            "Bootstrap-Klasse benutzt, aber beim Kuerzen entfernt — das Element bleibt ungestaltet:\n  " +
            string.Join("\n  ", treffer.Select(kv => $"{kv.Key}  (z. B. {kv.Value})")));
    }

    private static HashSet<string> CssKlassen(string css) =>
        Regex.Matches(css, @"\.(-?[_a-zA-Z][_a-zA-Z0-9-]*)")
             .Select(m => m.Groups[1].Value)
             .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Klassennamen aus class="…" im Markup, ohne Razor-Ausdruecke.</summary>
    private static IEnumerable<string> MarkupKlassen(string markup) =>
        Regex.Matches(markup, @"class=""([^""]*)""")
             .SelectMany(m => m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
             .Where(t => !t.Contains('@') && !t.Contains('<') && !t.Contains('{'))
             .Distinct(StringComparer.OrdinalIgnoreCase);

    [Theory]
    [InlineData("flagcdn.com")]
    [InlineData("cdn.jsdelivr.net")]
    [InlineData("unpkg.com")]
    [InlineData("fonts.googleapis.com")]
    [InlineData("fonts.gstatic.com")]
    public void Keine_Fremdanfrage_Aus_Einer_View(string host)
    {
        var treffer = new List<string>();
        foreach (var view in Directory.EnumerateFiles(Path.Combine(RepoRoot, "Erdi-ERC", "Views"),
                                                      "*.cshtml", SearchOption.AllDirectories))
        {
            if (Lies(view).Contains(host, StringComparison.OrdinalIgnoreCase))
                treffer.Add(Path.GetFileName(view));
        }
        Assert.True(treffer.Count == 0, $"{host} wird aus diesen Views geladen: {string.Join(", ", treffer)}");
    }

    /// <summary>
    /// Bilder gehoeren ins eigene wwwroot. Ein <img> mit fremder Quelle ist eine
    /// Anfrage an Dritte, die im Netzwerk-Panel als Fremd-Origin auftaucht — und
    /// damit genau das, was das Performance-Budget ausschliesst.
    /// </summary>
    [Fact]
    public void Bilder_Kommen_Nicht_Von_Fremden_Hosts()
    {
        var treffer = new List<string>();
        foreach (var view in Directory.EnumerateFiles(Path.Combine(RepoRoot, "Erdi-ERC", "Views"),
                                                      "*.cshtml", SearchOption.AllDirectories))
        {
            foreach (Match m in Regex.Matches(Lies(view), @"<img\b[^>]*?\bsrc\s*=\s*""(?<u>https?://[^""]+)""",
                                              RegexOptions.IgnoreCase))
                treffer.Add($"{Path.GetFileName(view)}: {m.Groups["u"].Value}");
        }
        Assert.True(treffer.Count == 0,
            "Bilder von fremden Hosts:\n  " + string.Join("\n  ", treffer));
    }

    /// <summary>
    /// Ein Einblende-Zustand (opacity &lt; 1) ohne das JS, das ihn wieder aufhebt,
    /// macht den Inhalt DAUERHAFT unsichtbar. Genau das ist beim Umbau passiert:
    /// der IntersectionObserver flog raus, die .reveal-Regel blieb. Der Fehler ist
    /// auf keiner Seite sichtbar, auf der man nur das Markup prueft — deshalb
    /// braucht er einen Test.
    /// </summary>
    [Fact]
    public void Kein_Verstecktes_Element_Ohne_Aufheber()
    {
        // Alle CSS-Regeln einsammeln, die einen Inhalt unsichtbar schalten.
        var verstecker = new List<(string Klasse, string Zustand, string Datei)>();
        var aufheber = new List<(string Klasse, string Zustand)>();

        foreach (var datei in AlleCssDateien())
        {
            // Kommentare raus: sonst liest der Selektor-Ausdruck den Kommentartext
            // der vorigen Regel mit und meldet Regeln, die es nicht gibt.
            var text = Regex.Replace(Lies(datei), @"/\*.*?\*/", "", RegexOptions.Singleline);
            foreach (Match m in Regex.Matches(text, @"(?<sel>[^{}]*)\{(?<body>[^{}]*)\}"))
            {
                var selektor = m.Groups["sel"].Value;
                var op = Regex.Match(m.Groups["body"].Value, @"opacity\s*:\s*(?<v>[\d.]+)");
                if (!op.Success) continue;
                var wert = double.Parse(op.Groups["v"].Value, System.Globalization.CultureInfo.InvariantCulture);

                // Zustandsklasse im Selektor, z. B. is-visible
                var zustand = Regex.Match(selektor, @"[.](is|has)-[a-z-]+");
                // Die Klasse, die den Zustand traegt, z. B. f1-stagger oder reveal
                var klassen = Regex.Matches(selektor, @"[.]([a-z0-9][a-z0-9_-]*)")
                                   .Select(k => k.Groups[1].Value)
                                   .Where(k => !k.StartsWith("is-") && !k.StartsWith("has-"))
                                   .ToList();
                if (klassen.Count == 0) continue;
                var traeger = klassen[^1];

                if (wert < 1.0) verstecker.Add((traeger, zustand.Success ? zustand.Value[1..] : "", Path.GetFileName(datei)));
                else if (zustand.Success) aufheber.Add((traeger, zustand.Value[1..]));
            }
        }

        // Fuer jede versteckende Regel: welcher Zustand hebt sie auf?
        var erwartet = new Dictionary<string, string>();
        foreach (var (klasse, _, _) in verstecker)
        {
            var passender = aufheber.FirstOrDefault(a => a.Klasse == klasse);
            if (passender.Klasse is not null) erwartet[klasse] = passender.Zustand;
        }

        // Jede View, die eine solche Klasse benutzt, muss den Aufheber selbst mitbringen.
        // (Der fruehre Test nagelte das auf ".reveal" fest und uebersah .f1-stagger.)
        var beanstandet = new List<string>();
        foreach (var view in Directory.EnumerateFiles(Path.Combine(RepoRoot, "Erdi-ERC", "Views"),
                                                      "*.cshtml", SearchOption.AllDirectories))
        {
            var inhalt = Lies(view);
            foreach (var (klasse, zustand) in erwartet)
            {
                if (!Regex.IsMatch(inhalt, $@"\b{Regex.Escape(klasse)}\b")) continue;
                if (inhalt.Contains(zustand, StringComparison.Ordinal)) continue;
                beanstandet.Add($"{Path.GetFileName(view)} benutzt '{klasse}', setzt aber nie '{zustand}' — " +
                                $"{klasse} startet unsichtbar und bleibt es.");
            }
        }

        Assert.True(beanstandet.Count == 0,
            "Einblende-Zustand ohne Aufheber — der Inhalt bleibt unsichtbar:\n  " +
            string.Join("\n  ", beanstandet.Distinct()));
    }
}
