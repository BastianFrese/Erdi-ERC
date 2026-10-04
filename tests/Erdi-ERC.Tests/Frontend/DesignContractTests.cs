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
    /// Ein Einblende-Zustand (opacity &lt; 1) ohne das JS, das ihn wieder aufhebt,
    /// macht den Inhalt DAUERHAFT unsichtbar. Genau das ist beim Umbau passiert:
    /// der IntersectionObserver flog raus, die .reveal-Regel blieb. Der Fehler ist
    /// auf keiner Seite sichtbar, auf der man nur das Markup prueft — deshalb
    /// braucht er einen Test.
    /// </summary>
    [Fact]
    public void Reveal_Versteckt_Keinen_Inhalt()
    {
        var beanstandet = new List<string>();

        foreach (var datei in AlleCssDateien())
        {
            // Kommentare raus: sonst liest der Selektor-Ausdruck den Kommentartext
            // der vorigen Regel mit und meldet Regeln, die es nicht gibt.
            var text = Regex.Replace(Lies(datei), @"/\*.*?\*/", "", RegexOptions.Singleline);
            foreach (Match m in Regex.Matches(text, @"(?<sel>[^{}]*)\{(?<body>[^{}]*)\}"))
            {
                var selektor = m.Groups["sel"].Value;
                if (!selektor.Contains(".reveal", StringComparison.Ordinal)) continue;
                if (selektor.Contains("is-visible", StringComparison.Ordinal)) continue;

                var op = Regex.Match(m.Groups["body"].Value, @"opacity\s*:\s*(?<v>[\d.]+)");
                if (!op.Success) continue;

                var wert = double.Parse(op.Groups["v"].Value, System.Globalization.CultureInfo.InvariantCulture);
                if (wert < 1.0)
                    beanstandet.Add($"{Path.GetFileName(datei)}: '{selektor.Trim()}' setzt opacity:{op.Groups["v"].Value}");
            }
        }

        Assert.True(beanstandet.Count == 0,
            "Einblende-Zustand ohne JS, das ihn aufhebt — der Inhalt bleibt unsichtbar:\n  " +
            string.Join("\n  ", beanstandet));
    }
}
