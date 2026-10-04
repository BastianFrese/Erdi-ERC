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
    [InlineData("race-lights__chequers")]
    [InlineData("race-lights__scan")]
    [InlineData("race-lights__speedlines")]
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
