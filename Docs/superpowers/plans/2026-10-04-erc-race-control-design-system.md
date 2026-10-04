# „Race Control" Design-System — Umsetzungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Die ERC-Site bekommt eine neue visuelle Identität („Race Control / Broadcast"), umgesetzt als Design-System-Schnitt, den die 101 bestehenden Razor-Views erben — flach, hartkantig, performant.

**Architecture:** Die visuelle Sprache liegt vollständig in `wwwroot/css/site/*.css` (13 Partials, per MSBuild zu `site.bundled.min.css` konkateniert) plus `wwwroot/css/pages/*.css` (14 Seitendateien). Die Ebenen werden von unten nach oben ersetzt: Tokens → Base → Layout → Komponenten → Shell → Partials → Seiten. Parallel wird die Zahl der Fremdanfragen und die Menge an CSS reduziert, um die Performance-Budgets zu erreichen.

**Tech Stack:** ASP.NET Core MVC (.NET 10), Razor, Bootstrap 5, Lucide, esbuild, MSBuild. Tests: xUnit (`tests/Erdi-ERC.Tests`).

**Spec:** `Docs/superpowers/specs/2026-10-04-erc-race-control-frontend-design.md`

---

## Hinweis zur Granularität dieses Plans

Die Tokens, die neuen Komponenten und alle technischen Schritte (Tests, Fonts, Bundling,
Turm, Start-Lights) stehen hier als **vollständiger Code**. Die vier Aufgaben, die
bestehende CSS-Ebenen mit zusammen ~5000 Zeilen umschreiben (Tasks 5–8), sind dagegen als
**Spezifikation mit Abnahmekriterien** beschrieben statt als vollständiger Text — diese
Dateien neu zu tippen wäre kein Plan, sondern ein Diktat, und der Inhalt ergibt sich
vollständig aus den Tokens und Regeln der Spec. Wo in diesen Aufgaben Code steht, ist er
verbindlich; die Abnahmelisten ebenso.

## Global Constraints

Werte wörtlich aus der Spec. Jede Aufgabe muss sie einhalten.

- **Budgets** (gedrosseltes Mobilprofil): LCP < 1.5 s · CLS < 0.05 · INP < 200 ms.
- **CSS gesamt < 60 KB gzip in genau einem Request.** Keine Seitendatei wird mehr aus
  Views verlinkt.
- **Null render-blockierende Fremdanfragen.** Keine URL an `fonts.googleapis.com`,
  `fonts.gstatic.com`, `cdn.jsdelivr.net`.
- **Webfonts: ≤ 2 Dateien, selbst gehostet**, `font-display: swap`.
- **Eine Schriftfamilie:** Saira (normal + Semi Condensed). Orbitron, Inter und
  JetBrains Mono entfallen vollständig.
- **Signalfarben, exakt:** `--fast #B24BF3` · `--best #00D26A` · `--slow #FFD100` ·
  `--flag #E10600` · `--wet #3B8BEA`.
- **Grundfläche, exakt:** `--track #0B0C0E` · `--tarmac #14171B` · `--kerb #202630` ·
  `--rule rgba(255,255,255,.10)`.
- **Textkontrast ≥ 4.5:1.** `--ink-2` ist `rgba(242,244,247,.68)`; wird der Kontrast
  gegen `--track` nicht erreicht, wird der Wert angehoben — nicht die Anforderung gesenkt.
- **Alle Zahlen in Tabellen:** `font-variant-numeric: tabular-nums`.
- **Verboten in `wwwroot/css/**`:** `backdrop-filter`, `-webkit-backdrop-filter`.
- **Verbotene Selektoren** (müssen verschwinden): `f1-speedline`, `f1-spark`,
  `chequered-sweep`, `race-lights__chequers`, `race-lights__scan`,
  `race-lights__speedlines`, `f1-border-anim`, `f1-holographic`, `f1-lift`,
  `f1-tiltable`, `f1-home-backdrop`, `page-transition`, `scroll-progress`.
- **Bundling:** Die Liste der CSS-Eingaben steht **zweimal** in
  `Erdi-ERC/Erdi-ERC.csproj` — in `Inputs=` von `BundleSiteCss` und in den
  `_CssBundle Include=`-Zeilen. Beide Stellen müssen zueinander und zum Dateisystem
  passen. Eine neue Datei, die nur an einer Stelle steht, wird stillschweigend nicht
  gebündelt; eine gelöschte Datei, die noch in einer Liste steht, bricht den Build.
- **Admin:** eigener Namespace (`adm-*`), eigene Shell. Er nimmt **nur** Tokens und
  Schriften mit, kein Layout. `data-bs-*`-Attribute werden nie angefasst.
- **Keine Änderung** an Controllern, EF-Modellen, Routing, Auth, Deploy-Weg.
  **Einzige Ausnahme:** `LayoutData`/`ILayoutDataService` um die Top-10-Wertung
  erweitern (Task 11) — der Turm braucht sie auf jeder Seite.
- **Commits:** Conventional Commits (`feat:`, `refactor:`, `chore:`, `test:`).

## Review Focus

Fünf Eingaben bzw. Zustände, die die Spec impliziert, die aber in keiner Aufgabe durch
einen automatischen Test abgedeckt sind. Für jede steht in der genannten Aufgabe eine
Prüfung im Abnahmetext.

1. **Deutsche Umlaute.** Die selbst gehostete Saira wird beschnitten; fehlt ä/ö/ü/ß im
   Subset, zeigt die ganze deutschsprachige Seite Kästchen. → Task 2.
2. **Cadillac (`#000000`).** Teamfarben-Verlauf und -Balken sind bei Schwarz unsichtbar;
   die Zuordnung Fahrer→Team verschwindet genau für ein Team. → Task 11.
3. **Leere Datenmengen.** Neue Liga, keine Ergebnisse, keine Setups: Turm ohne Zeilen,
   Tabelle ohne Zeilen, `sector` ohne Segmente dürfen nicht kollabieren oder
   Zeilenhöhen springen lassen. → Tasks 5 und 9.
4. **Sehr lange Fahrernamen** in der kondensierten Spalte des Turms. Überlauf oder
   umbrechende Zeilenhöhe erzeugt Layout-Sprünge (CLS-Budget). → Task 11.
5. **Bootstrap-Dropdowns mit neuen Tokens.** Das Menü wird über `.dropdown-menu`
   gestylt; ein zu dunkler Grund oder fehlender Kontrast macht die Navigation
   unbenutzbar — und die Navigation ist der Weg zu allem anderen. → Task 6.

---

## Dateistruktur

**Neu:**

| Datei | Verantwortung |
|---|---|
| `tests/Erdi-ERC.Tests/Frontend/DesignContractTests.cs` | Prüft den technischen Vertrag der neuen Gestalt (Bundling, verbotene Eigenschaften/Selektoren, Fremd-Origins, kein versteckter Inhalt) |
| `Erdi-ERC/wwwroot/fonts/*.woff2` | Selbst gehostete Saira-Schnitte |
| `Erdi-ERC/wwwroot/css/site/01-tokens.css` | Alle Tokens (Farbe, Schrift, Skala, Abstände, Turmbreite) — ersetzt `12-tokens-f1.css` und `13-tokens-light.css` |
| `Erdi-ERC/wwwroot/css/site/04f-tower.css` | Turm, Sektor, Compound, Delta, Flaggenstatus, Datentabelle, Panel, Streifen |
| `Erdi-ERC/Views/Shared/_Tower.cshtml` | Turm-Partial (Top 10) |

**Gelöscht:** `site/12-tokens-f1.css`, `site/13-tokens-light.css`, `pages/driver-detail.css`,
`site.community.css`, `site.track-setups.css`, `pages/home-index-fx.css`.

**Umgeschrieben:** `site/01-tokens.css`, `02-base.css`, `03-layout.css`,
`04-components.css`, `05-f1-modules.css`, `10-f1-quickwins.css`, `11-unique-addons.css`,
`06-pages-and-admin.css`, `09-responsive.css`, `pages/home-index.css`,
`Views/Shared/_Layout.cshtml`, `_AdminLayout.cshtml`, die Shared Partials,
`Views/Home/Index.cshtml`.

**Erweitert (Ausnahme):** `Erdi-ERC/Services/ILayoutDataService.cs`, `LayoutDataService.cs`.

Die Seiten-CSS unter `pages/` lösen sich am Ende auf: ihr Inhalt wandert in die Ebenen
oder wird gelöscht.

---

## Task 1: Den technischen Vertrag als Test festhalten (RED)

Diese Aufgabe schreibt zuerst die Prüfungen, die den Rest des Plans steuern. Sie müssen
**fehlschlagen** — das ist der Beweis, dass sie etwas prüfen.

**Files:**
- Create: `tests/Erdi-ERC.Tests/Frontend/DesignContractTests.cs`

**Interfaces:**
- Produces: `DesignContractTests.RepoRoot` (statische Hilfsmethode, die das
  Repository-Wurzelverzeichnis findet) — von allen späteren Testaufgaben mitbenutzt.

- [ ] **Step 1: Die Tests schreiben**

```csharp
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

    private static IEnumerable<string> AlleCssDateien() =>
        Directory.EnumerateFiles(Path.Combine(WwwRoot, "css"), "*.css", SearchOption.AllDirectories)
                 .Where(p => !p.EndsWith(".min.css", StringComparison.OrdinalIgnoreCase));

    private static string Lies(string pfad) => File.ReadAllText(pfad);

    // --- Bundling ---------------------------------------------------------

    [Fact]
    public void Jede_Css_Partial_Steht_Im_Bundle()
    {
        var csproj = Lies(Csproj);
        var aufPlatte = Directory.EnumerateFiles(Path.Combine(WwwRoot, "css", "site"), "*.css")
                                 .Select(Path.GetFileName)
                                 .Where(n => n is not null && !n.EndsWith(".min.css", StringComparison.OrdinalIgnoreCase))
                                 .Select(n => n!)
                                 .OrderBy(n => n)
                                 .ToArray();

        // Beide Listen im Target muessen existieren und identisch sein.
        var inputs = Regex.Matches(csproj, @"<BundleSiteCss[^>]*Inputs=""([^""]+)""")
                          .SelectMany(m => m.Groups[1].Value.Split(';'))
                          .Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        var includes = Regex.Matches(csproj, @"<_CssBundle Include=""([^""]+)""")
                            .Select(m => m.Groups[1].Value.Trim()).ToArray();

        Assert.NotEmpty(inputs);
        Assert.NotEmpty(includes);

        string Nur(string pfad) => Path.GetFileName(pfad);
        Assert.Equal(inputs.Select(Nur).OrderBy(n => n), includes.Select(Nur).OrderBy(n => n));

        var imBundle = inputs.Select(Nur).OrderBy(n => n).ToArray();
        Assert.Equal(aufPlatte, imBundle);
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
}
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlagen bestätigen**

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter "FullyQualifiedName~DesignContractTests"`
Expected: **FAIL.** Erwartete Fehlschläge: `Kein_Backdrop_Filter_Mehr`,
`Keine_Anfrage_An_Externe_Domain` (Google Fonts + jsDelivr), `AppMain_Wird_Nicht_Versteckt`,
und die meisten `Verbotene_Klasse_Ist_Verschwunden`-Fälle.
`Jede_Css_Partial_Steht_Im_Bundle` sollte **jetzt schon grün** sein — die Bundle-Liste ist
zum Start korrekt. Bleibt sie rot, ist der Parser falsch, nicht die Liste.

- [ ] **Step 3: Commit**

```bash
git add tests/Erdi-ERC.Tests/Frontend/DesignContractTests.cs
git commit -m "test(frontend): technischen Vertrag der neuen Gestalt als Tests festhalten"
```

---

## Task 2: Tokens und selbst gehostete Schriften

**Files:**
- Modify: `Erdi-ERC/wwwroot/css/site/01-tokens.css` (vollständig ersetzen)
- Delete: `Erdi-ERC/wwwroot/css/site/12-tokens-f1.css`, `Erdi-ERC/wwwroot/css/site/13-tokens-light.css`
- Modify: `Erdi-ERC/Erdi-ERC.csproj` (**beide** Bundle-Listen)
- Create: `Erdi-ERC/wwwroot/fonts/saira-latin.woff2`, `saira-semi-condensed-latin.woff2`
- Modify: `Erdi-ERC/Views/Shared/_Layout.cshtml` (Kopf), `Views/Admin/_AdminLayout.cshtml` (Kopf)

**Interfaces:**
- Produces: die Token-Namen `--track --tarmac --kerb --rule --ink --ink-2 --ink-3
  --fast --best --slow --flag --wet --team --team-2 --ff --ff-cond --rail`,
  die Schriftstapel `--ff` (Saira) und `--ff-cond` (Saira Semi Condensed),
  und die Regel `[data-theme="light"]` für die Tageslicht-Variante.

- [ ] **Step 1: Schriften holen**

Die Google-Fonts-CSS-API mit einem modernen User-Agent liefert WOFF2-Direktlinks.
Zuerst die Variablenschnitte finden, dann die Dateien laden:

```bash
cd /c/Users/basti/source/repos/Erdi-ERC/Erdi-ERC
mkdir -p wwwroot/fonts
UA="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36"

curl -s -A "$UA" \
  "https://fonts.googleapis.com/css2?family=Saira:wght@400;600;700&family=Saira+Semi+Condensed:wght@400;600;700&display=swap" \
  > /tmp/saira.css
grep -oE 'https://[^)]+\.woff2' /tmp/saira.css
```

Die beiden `latin`-URLs (nicht `latin-ext`, nicht die anderen Subsets) herunterladen und
umbenennen:

```bash
# Platzhalter durch die beiden tatsaechlichen latin-URLs ersetzen:
curl -s -o wwwroot/fonts/saira-latin.woff2 "<URL-saira-latin>"
curl -s -o wwwroot/fonts/saira-semi-condensed-latin.woff2 "<URL-saira-semi-condensed-latin>"
```

**Prüfen (Review-Focus 1, deutsche Umlaute):** Die `latin`-Subsets von Google enthalten
U+00E4 (ä), U+00F6 (ö), U+00FC (ü) und U+00DF (ß). Das wird überprüft, nicht angenommen:

```bash
python -c "
import sys
from fontTools.ttLib import TTFont
for p in ['wwwroot/fonts/saira-latin.woff2','wwwroot/fonts/saira-semi-condensed-latin.woff2']:
    f = TTFont(p)
    cmap = f.getBestCmap()
    fehlend = [c for c in 'äöüßÄÖÜ' if ord(c) not in cmap]
    print(p, 'FEHLT:', fehlend if fehlend else 'ok')
"
```

Ist `fontTools` nicht installiert: `python -m pip install fonttools brotli`.
Meldet das Skript fehlende Zeichen, ist das eine harte Sperre — dann die Datei nicht
verwenden, sondern die Variable-Font-Variante laden (deutlich größer, aber vollständig).

- [ ] **Step 2: Den Failing-Test für die Schriften prüfen (noch rot)**

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter "FullyQualifiedName~Keine_Anfrage_An_Externe_Domain"`
Expected: FAIL — `_Layout.cshtml` verweist noch auf Google Fonts und jsDelivr.

- [ ] **Step 3: `01-tokens.css` vollständig ersetzen**

```css
/* =====================================================================
   01 · TOKENS  —  „Race Control"
   Einzige Quelle fuer Farbe, Schrift, Skala und Abstaende.
   ===================================================================== */
:root {
    /* --- Grundflaeche: neutral-kuehl, kein Blaustich --- */
    --track: #0B0C0E;
    --tarmac: #14171B;
    --kerb: #202630;
    --rule: rgba(255, 255, 255, .10);
    --rule-strong: rgba(255, 255, 255, .22);

    --ink: #F2F4F7;
    --ink-2: rgba(242, 244, 247, .68);
    --ink-3: rgba(242, 244, 247, .38);

    /* --- Signalfarben: jede bedeutet genau eine Sache --- */
    --fast: #B24BF3;   /* schnellste Runde / schnellster Sektor */
    --best: #00D26A;   /* persoenliche Bestzeit, DRS offen      */
    --slow: #FFD100;   /* langsamer, Vorsicht, Medium            */
    --flag: #E10600;   /* Ligafarbe, rote Flagge, Soft           */
    --wet: #3B8BEA;    /* Inter, Wet, blaue Flagge               */

    /* --- Teamfarbe: Zuordnung Fahrer -> Team, nicht Stimmung --- */
    --team: var(--flag);
    --team-2: #FFFFFF;

    /* --- Schrift: eine Familie, zwei Schnitte --- */
    --ff: 'Saira', 'Segoe UI', system-ui, sans-serif;
    --ff-cond: 'Saira Semi Condensed', 'Saira', system-ui, sans-serif;
    --num: tabular-nums;

    /* --- Skala ~1.25 --- */
    --fs-xs: .75rem;
    --fs-s: .875rem;
    --fs-m: 1rem;
    --fs-l: 1.25rem;
    --fs-xl: 1.75rem;
    --fs-2xl: clamp(1.75rem, 3vw, 2.5rem);
    --fs-3xl: clamp(2.25rem, 5vw, 3.5rem);

    /* --- Abstaende --- */
    --sp-1: .25rem; --sp-2: .5rem;  --sp-3: .75rem; --sp-4: 1rem;
    --sp-5: 1.5rem; --sp-6: 2rem;   --sp-7: 3rem;   --sp-8: 4rem;

    /* --- Kanten: hart, kein Radius-Feuerwerk --- */
    --r-1: 2px;
    --r-2: 3px;

    /* --- Bewegung --- */
    --t-fast: 120ms cubic-bezier(.4, 0, .2, 1);
    --t-base: 220ms cubic-bezier(.22, 1, .36, 1);

    /* --- Layout --- */
    --rail: 288px;          /* Breite der Turm-Spalte */
    --gutter: clamp(1rem, 2.4vw, 2rem);

    /* --- Tageslicht-Variante (dieselben Namen, andere Werte) --- */
}

[data-theme="light"] {
    --track: #F4F5F7;
    --tarmac: #FFFFFF;
    --kerb: #E4E7EC;
    --rule: rgba(11, 12, 14, .12);
    --rule-strong: rgba(11, 12, 14, .28);

    --ink: #0B0C0E;
    --ink-2: rgba(11, 12, 14, .72);
    --ink-3: rgba(11, 12, 14, .42);

    --fast: #7A28B8;
    --best: #0A8F4B;
    --slow: #B07A00;
    --flag: #C00500;
    --wet: #1F63B0;
}
```

- [ ] **Step 4: Die Teamfarben aus `12-tokens-f1.css` übernehmen und den Cadillac-Fall lösen**

Die elf Teams aus `Erdi-ERC/Helpers/F1TeamsHelper.cs` werden als
`body.team-<CssKey>`-Blöcke eingetragen. Jeder Block setzt **nur** `--team` und `--team-2`,
niemals die Signalfarben:

```css
body.team-redbull     { --team:#3671C6; --team-2:#CC1E4A; }
body.team-mercedes    { --team:#00D2BE; --team-2:#C0C0C0; }
body.team-ferrari     { --team:#E8002D; --team-2:#FFFFFF; }
body.team-mclaren     { --team:#FF8000; --team-2:#000000; }
body.team-astonmartin { --team:#229971; --team-2:#CEDC00; }
body.team-alpine      { --team:#FF87BC; --team-2:#0093CC; }
body.team-williams    { --team:#64C4FF; --team-2:#FFFFFF; }
body.team-haas        { --team:#B6BABD; --team-2:#E8002D; }
body.team-racingbulls,
body.team-rb          { --team:#6692FF; --team-2:#CC1E4A; }
body.team-sauber,
body.team-kicksauber,
body.team-audi        { --team:#8A9597; --team-2:#FFFFFF; }

/* Cadillac ist #000000 — als Balken und Verlauf unsichtbar.
   Rueckfall auf die Tertiaerfarbe, die Sekundaerfarbe wird zum schmalen Zusatzstreifen. */
body.team-cadillac    { --team:var(--ink-2); --team-2:#FFFFFF; }
```

- [ ] **Step 5: Das Bundle im csproj nachziehen (beide Stellen)**

In `Erdi-ERC/Erdi-ERC.csproj`:

`12-tokens-f1.css` und `13-tokens-light.css` aus `Inputs=` **und** aus den
`_CssBundle Include=`-Zeilen entfernen. Danach muss die Zahl der Einträge an beiden
Stellen identisch sein (13 statt 15).

- [ ] **Step 6: Die `@font-face`-Regeln und den Layout-Kopf umstellen**

In `_Layout.cshtml` die beiden Google-Fonts-`<link>`-Blöcke (Zeilen mit
`fonts.googleapis.com` und `cdn.jsdelivr.net` für `bootstrap-icons`) sowie das
zugehörige `<noscript>` entfernen. Stattdessen in `01-tokens.css` oben:

```css
@font-face {
    font-family: 'Saira';
    src: url('/fonts/saira-latin.woff2') format('woff2');
    font-weight: 400 700;
    font-display: swap;
    unicode-range: U+0000-00FF, U+0131, U+0152-0153, U+02BB-02BC, U+2000-206F, U+2074, U+20AC, U+2122, U+2191, U+2193, U+2212, U+2215, U+FEFF, U+FFFD;
}
@font-face {
    font-family: 'Saira Semi Condensed';
    src: url('/fonts/saira-semi-condensed-latin.woff2') format('woff2');
    font-weight: 400 700;
    font-display: swap;
    unicode-range: U+0000-00FF, U+0131, U+0152-0153, U+02BB-02BC, U+2000-206F, U+2074, U+20AC, U+2122, U+2191, U+2193, U+2212, U+2215, U+FEFF, U+FFFD;
}
/* ponytail: Metrik-Override gegen FOUT-Sprung, sobald die Schnitte vermessen sind.
   Bis dahin reicht font-display:swap — Saira und system-ui liegen nah beieinander. */
```

Dieselbe Umstellung in `Views/Admin/_AdminLayout.cshtml`. Das `bootstrap-icons`-CDN
entfällt hier ebenfalls; Task 12 liefert die selbst gehostete Fassung. Bis dahin bleibt
der Admin ohne Icon-Font lauffähig — die `bi bi-*`-Glyphen erscheinen als leere Flächen,
was für Zwischenstände akzeptabel ist und in Task 12 verschwindet.

- [ ] **Step 7: Tests laufen lassen**

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter "FullyQualifiedName~DesignContractTests"`
Expected: `Keine_Anfrage_An_Externe_Domain` und `Jede_Css_Partial_Steht_Im_Bundle` **PASS**.
Die übrigen bleiben rot (Tasks 4–8).

- [ ] **Step 8: Build und Sichtprüfung**

Run: `dotnet build Erdi-ERC/Erdi-ERC.csproj`
Expected: Build erfolgreich, kein Razor-Fehler.

Run: `dotnet run --project Erdi-ERC --launch-profile http` → `http://localhost:5005`
Expected: Seite lädt, deutsche Umlaute korrekt (Review-Focus 1), keine Kästchen.

- [ ] **Step 9: Commit**

```bash
git add Erdi-ERC/wwwroot/css/site/01-tokens.css Erdi-ERC/Erdi-ERC.csproj \
        Erdi-ERC/wwwroot/fonts Erdi-ERC/Views/Shared/_Layout.cshtml \
        Erdi-ERC/Views/Admin/_AdminLayout.cshtml
git rm Erdi-ERC/wwwroot/css/site/12-tokens-f1.css Erdi-ERC/wwwroot/css/site/13-tokens-light.css
git commit -m "feat(frontend): Race-Control-Tokens und selbst gehostete Saira"
```

---

## Task 3: Base-Ebene

**Files:**
- Modify: `Erdi-ERC/wwwroot/css/site/02-base.css` (vollständig ersetzen)

**Interfaces:**
- Consumes: alle Tokens aus Task 2.
- Produces: `body`-Grundtypografie, `.visually-hidden`, globale `:focus-visible`-Regel,
  `.u-num` (tabellenzahlige Auszeichnung).

- [ ] **Step 1: `02-base.css` ersetzen**

```css
/* =====================================================================
   02 · BASE
   ===================================================================== */
*, *::before, *::after { box-sizing: border-box; }

html { -webkit-text-size-adjust: 100%; }

body {
    margin: 0;
    background: var(--track);
    color: var(--ink);
    font-family: var(--ff);
    font-size: var(--fs-m);
    line-height: 1.55;
    -webkit-font-smoothing: antialiased;
}

h1, h2, h3, h4 {
    margin: 0 0 var(--sp-3);
    font-weight: 700;
    line-height: 1.15;
    letter-spacing: -0.01em;
}
h1 { font-size: var(--fs-3xl); }
h2 { font-size: var(--fs-2xl); }
h3 { font-size: var(--fs-xl); }

p { margin: 0 0 var(--sp-4); max-width: 72ch; }

a { color: var(--team); text-decoration-thickness: 1px; text-underline-offset: 2px; }
a:hover { color: var(--ink); }

/* Zahlen richten aus, ohne Mono-Schrift. */
.u-num, .tower td, .data-table td, time, .delta { font-variant-numeric: var(--num); }

hr { border: 0; border-top: 1px solid var(--rule); margin: var(--sp-6) 0; }

:focus-visible {
    outline: 2px solid var(--best);
    outline-offset: 2px;
    border-radius: var(--r-1);
}

::selection { background: var(--team); color: #fff; }

.visually-hidden {
    position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px;
    overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; border: 0;
}

@media (prefers-reduced-motion: reduce) {
    *, *::before, *::after {
        animation-duration: .01ms !important;
        animation-iteration-count: 1 !important;
        transition-duration: .01ms !important;
        scroll-behavior: auto !important;
    }
}
```

- [ ] **Step 2: Build und Sichtprüfung**

Run: `dotnet build Erdi-ERC/Erdi-ERC.csproj && dotnet run --project Erdi-ERC --launch-profile http`
Expected: Seite lädt, Grundtypografie in Saira, Fokusring beim Durchtabben sichtbar.

- [ ] **Step 3: Commit**

```bash
git add Erdi-ERC/wwwroot/css/site/02-base.css
git commit -m "feat(frontend): Base-Ebene auf Race-Control-Tokens"
```

---

## Task 4: Layout-Shell ohne Turm

**Files:**
- Modify: `Erdi-ERC/wwwroot/css/site/03-layout.css` (vollständig ersetzen)
- Modify: `Erdi-ERC/Views/Shared/_Layout.cshtml` (Topbar-Markup)

**Interfaces:**
- Consumes: Tokens aus Task 2.
- Produces: `.strip` (Broadcast-Streifen), `.app-shell-container`, `.page`, `.footer`,
  `.music-player` (schmaler Streifen). Task 11 fügt `.app-shell` als Grid mit `--rail` hinzu.

- [ ] **Step 1: `03-layout.css` ersetzen**

Die komplette Cockpit-Fassung (Glas-Reflex, Carbon, Alu-Trim, Ambientelicht,
Hamburger-Animation, `backdrop-filter`) entfällt. Ersetzt durch:

```css
/* =====================================================================
   04 · LAYOUT SHELL
   ===================================================================== */
.app-shell-container {
    width: 100%;
    padding-left: var(--gutter);
    padding-right: var(--gutter);
}

/* --- Broadcast-Streifen: duenn, hartkantig, sticky --- */
.strip {
    position: sticky; top: 0; z-index: 1040;
    display: flex; align-items: center; gap: var(--sp-4);
    min-height: 56px;
    padding: var(--sp-2) var(--gutter);
    background: var(--track);
    border-bottom: 1px solid var(--rule);
}
.strip__session { display: flex; align-items: baseline; gap: var(--sp-3); }
.strip__kicker {
    font-family: var(--ff-cond);
    font-size: var(--fs-s); font-weight: 600;
    letter-spacing: .14em; text-transform: uppercase;
    color: var(--ink-3);
}
.strip__nav { display: flex; flex-wrap: wrap; gap: var(--sp-4); margin-left: auto; }
.strip__nav a {
    font-size: var(--fs-s); font-weight: 600;
    letter-spacing: .06em; text-transform: uppercase;
    color: var(--ink-2); text-decoration: none;
}
.strip__nav a:hover, .strip__nav a.active { color: var(--ink); }
.strip__nav a.active { box-shadow: inset 0 -2px 0 var(--team); }

.strip__status { display: flex; align-items: center; gap: var(--sp-3); margin-left: auto; }

/* --- Teamfarben-Verlauf hinter dem Streifen (Zuordnung, nicht Stimmung) --- */
.topbar-glow {
    position: absolute; inset: 0; pointer-events: none;
    background: linear-gradient(180deg, color-mix(in srgb, var(--team) 12%, transparent), transparent);
    opacity: .9;
}

.live-dot {
    width: 7px; height: 7px; border-radius: 50%;
    background: var(--best); flex: none;
}

.page { padding: var(--sp-6) 0 var(--sp-8); }

.footer {
    border-top: 1px solid var(--rule);
    padding: var(--sp-5) 0;
    font-size: var(--fs-s); color: var(--ink-3);
}
.footer a { color: var(--ink-2); text-decoration: none; }
.footer a:hover { color: var(--ink); }

.music-player {
    position: fixed; left: var(--gutter); bottom: var(--sp-4); z-index: 1030;
    display: flex; align-items: center; gap: var(--sp-3);
    padding: var(--sp-2) var(--sp-3);
    background: var(--tarmac); border: 1px solid var(--rule); border-radius: var(--r-2);
    font-size: var(--fs-s);
}
```

`.navbar`, `.navbar-toggler*`, `.topbar`, `.brand-mark`, `.brand-word`, `.nav-f1-badge`,
`.live-race-pill` erhalten **keine** Stilregeln mehr. Das Markup wird in Step 2 reduziert.

- [ ] **Step 2: Das Topbar-Markup in `_Layout.cshtml` ersetzen**

Den gesamten `<header class="topbar" id="mainNav">…</header>`-Block (Zeilen 262–416)
ersetzen durch:

```html
<header class="strip" id="mainNav">
    <div class="app-shell-container d-flex align-items-center w-100 position-relative">
        <a class="navbar-brand" asp-controller="Home" asp-action="Index">
            <span class="strip__kicker">Erdi&#x27;s Racing Community</span>
        </a>
        <nav class="strip__nav">
            <a class="@IsActive("Stats","Erdi10")" asp-controller="Stats" asp-action="Erdi10">Racing Hub</a>
            <a class="@IsActive("Races","Results","LeagueResults")" asp-controller="Races" asp-action="Results">Ergebnisse</a>
            <a class="@((IsActive("Races","AllRaces","RaceDetail")))" asp-controller="Races" asp-action="AllRaces">Rennen</a>
            <a class="@IsActive("Setups","TrackSetups")" asp-controller="Setups" asp-action="TrackSetups">Setups</a>
            <a class="@IsActive("Community","Events","EventDetail")" asp-controller="Community" asp-action="Events">Events</a>
            <a class="@IsActive("Races","RaceCalendar")" asp-controller="Races" asp-action="RaceCalendar">Kalender</a>
            <a class="@IsActive("Application","Apply","Submitted","MyApplication")" asp-controller="Application" asp-action="Apply">Bewerbung</a>
        </nav>
        <div class="strip__status">
            @if (_activeStream != null)
            {
                <a href="@(_activeStream.Url ?? "https://twitch.tv/erdi10")" target="_blank" rel="noopener"
                   class="d-flex align-items-center gap-2 text-decoration-none" title="@_activeStream.Title">
                    <span class="live-dot" aria-hidden="true"></span><span>Live</span>
                </a>
            }
            <button type="button" id="ercThemeToggle" class="btn btn-sm btn-ghost"
                    aria-label="Design umschalten (hell/dunkel)" aria-pressed="false">
                <i class="bi bi-sun" aria-hidden="true"></i>
            </button>
            @if (User.Identity?.IsAuthenticated == true)
            {
                <a class="navbar-auth-user--link" asp-controller="Profile" asp-action="Index">@User.Identity!.Name</a>
                <form asp-controller="Account" asp-action="Logout" method="post" class="d-inline">
                    @Html.AntiForgeryToken()
                    <button type="submit" class="btn btn-sm btn-ghost">Logout</button>
                </form>
            }
            else
            {
                <a class="btn btn-sm btn-primary" asp-controller="Account" asp-action="Login">Login</a>
            }
        </div>
        <div class="topbar-glow" aria-hidden="true"></div>
    </div>
</header>
```

**Was dabei verloren geht und bewusst ersetzt wird:**
- Die Dropdown-Menüs „ERC Liga", „Partner", „Mehr" — ihr Inhalt (Ewige Liste, Hall of Fame,
  Teams, Regelwerk, Impressum, Shops, Admin-Zugang) wandert in den Footer. Ein
  Broadcast-Streifen hat keine Untermenüs, und Dropdowns sind im Testprofil der teuerste
  Interaktionsteil. Der Admin-Link bleibt im Footer für `User.HasClaim("erdi:admin","true")`.
- Der Live-Stream-Button wird zum Live-Punkt im Statusbereich.

- [ ] **Step 3: Den Footer um die ausgelagerten Links erweitern**

Im `<footer class="footer">`-Block die Liste ergänzen: Ewige Liste, Hall of Fame, Teams,
Reservefahrer-Börse, Community-Votings, Highlights, Fahrerlevel, Stewarding, FAQ,
Über mich, Regelwerk, Impressum, Datenschutz, ERC Fanshop, Erdi10 Shop,
Admin Dashboard (nur mit Claim), App herunterladen (nur wenn
`SiteSettings.IsAppDownloadEnabled()`).

- [ ] **Step 4: Build und Sichtprüfung**

Run: `dotnet build Erdi-ERC/Erdi-ERC.csproj && dotnet run --project Erdi-ERC --launch-profile http`
Expected: Streifen oben, hartkantig, klebt beim Scrollen. Footer trägt alle Links.
Durchtabben: Fokusring sichtbar auf allen Links (**Review-Focus 5**, Vorstufe).

- [ ] **Step 5: Commit**

```bash
git add Erdi-ERC/wwwroot/css/site/03-layout.css Erdi-ERC/Views/Shared/_Layout.cshtml
git commit -m "feat(frontend): Broadcast-Streifen ersetzt die Glas-Navbar, Nebennavigation in den Footer"
```

---

## Task 5: Komponenten und die neuen Primitive

**Files:**
- Modify: `Erdi-ERC/wwwroot/css/site/04-components.css` (ersetzen)
- Create: `Erdi-ERC/wwwroot/css/site/04f-tower.css`
- Modify: `Erdi-ERC/Erdi-ERC.csproj` (**beide** Bundle-Listen: `04f-tower.css` eintragen)

**Interfaces:**
- Consumes: Tokens aus Task 2, `.strip`/`.app-shell-container` aus Task 4.
- Produces: `.tower`/`.tower__*`, `.sector`/`.sector__*`, `.compound`/`.compound--*`,
  `.delta`, `.flagstate`, `.data-table`, `.panel`/`.panel__*`, `.btn`-Familie,
  `.f1-btn`-Familie (Kompatibilität). Task 11 baut den Turm-Partial darauf.

- [ ] **Step 1: `04f-tower.css` neu anlegen**

Vollständiger Code — diese Datei trägt die Identität:

```css
/* =====================================================================
   04f · TURM UND DATENKODIERUNGEN
   Das tragende Bauteil: eine Zeile ist ein Fahrer, kein Karteikärtchen.
   ===================================================================== */

/* --- Der Turm --- */
.tower { width: 100%; border-collapse: collapse; font-family: var(--ff-cond); }
.tower caption {
    text-align: left; padding: 0 0 var(--sp-2);
    font-family: var(--ff); font-size: var(--fs-xs); font-weight: 700;
    letter-spacing: .16em; text-transform: uppercase; color: var(--ink-3);
}
.tower th {
    padding: var(--sp-2) var(--sp-2);
    font-family: var(--ff); font-size: var(--fs-xs); font-weight: 600;
    letter-spacing: .1em; text-transform: uppercase;
    color: var(--ink-3); text-align: left;
    border-bottom: 1px solid var(--rule-strong);
}
.tower td {
    padding: var(--sp-2);
    border-bottom: 1px solid var(--rule);
    font-variant-numeric: var(--num);
    height: 2.25rem;                 /* feste Zeilenhoehe -> kein CLS */
}
.tower tbody tr { background: transparent; }
.tower tbody tr:hover { background: var(--tarmac); }

.tower__pos { width: 2.25rem; color: var(--ink-3); font-weight: 700; }
.tower__team {
    width: 3px; padding: 0; border-bottom: 1px solid var(--rule);
    background: var(--team);
}
.tower__name {
    font-family: var(--ff); font-weight: 600; font-size: var(--fs-s);
    letter-spacing: .02em; text-transform: uppercase;
    white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
    max-width: 0;                   /* laesst text-overflow im Tabellenkontext greifen */
}
.tower__pts { text-align: right; color: var(--ink); font-weight: 700; }
.tower__gap { text-align: right; color: var(--ink-2); }

/* Eigene Zeile hervorheben — der Teamfarben-Verlauf aus der Spec. */
.tower__row--self {
    background: linear-gradient(90deg, color-mix(in srgb, var(--team) 14%, transparent), transparent 70%);
}

/* --- Sektorzeiten: Farbe UND Glyphe, nie Farbe allein --- */
.sector { display: inline-flex; align-items: center; gap: 2px; }
.sector__seg { width: 12px; height: 8px; background: var(--kerb); }
.sector__seg--fast { background: var(--fast); }
.sector__seg--best { background: var(--best); }
.sector__seg--slow { background: var(--slow); }
.sector__glyph { font-size: .7em; line-height: 1; color: var(--ink-3); }

/* --- Reifenmischung --- */
.compound {
    display: inline-flex; align-items: center; justify-content: center;
    width: 1.35rem; height: 1.35rem;
    font-size: .62rem; font-weight: 700; letter-spacing: 0;
    font-family: var(--ff);
    border: 1.5px solid var(--ink-3); border-radius: 50%;
    color: var(--ink); background: transparent;
}
.compound--soft   { border-color: var(--flag); color: var(--flag); }
.compound--medium { border-color: var(--slow); color: var(--slow); }
.compound--hard   { border-color: var(--ink);  color: var(--ink); }
.compound--inter  { border-color: var(--best); color: var(--best); }
.compound--wet    { border-color: var(--wet);  color: var(--wet); }

/* --- Zeitdifferenz --- */
.delta { font-variant-numeric: var(--num); color: var(--ink-2); }
.delta--lead { color: var(--ink); font-weight: 700; }
.delta--loss { color: var(--ink-2); }

/* --- Streckenstatus --- */
.flagstate {
    display: inline-flex; align-items: center; gap: var(--sp-2);
    font-family: var(--ff); font-size: var(--fs-xs); font-weight: 700;
    letter-spacing: .12em; text-transform: uppercase;
    padding: 2px var(--sp-2); border: 1px solid var(--ink-3); color: var(--ink-2);
}
.flagstate--green { border-color: var(--best); color: var(--best); }
.flagstate--yellow{ border-color: var(--slow); color: var(--slow); }
.flagstate--red   { border-color: var(--flag); color: var(--flag); }
.flagstate--chequer { border-color: var(--ink); color: var(--ink); }

/* --- Dichte Datentabelle --- */
.data-table { width: 100%; border-collapse: collapse; font-size: var(--fs-s); }
.data-table th {
    padding: var(--sp-2); text-align: left;
    font-size: var(--fs-xs); font-weight: 600; letter-spacing: .1em; text-transform: uppercase;
    color: var(--ink-3); border-bottom: 1px solid var(--rule-strong);
    position: sticky; top: 56px; background: var(--track);   /* unter dem Streifen */
    z-index: 2;
}
.data-table td { padding: var(--sp-2); border-bottom: 1px solid var(--rule); font-variant-numeric: var(--num); }
.data-table tbody tr:hover { background: var(--tarmac); }

/* --- Panel: harter Kasten mit Kopfzeile --- */
.panel { background: var(--tarmac); border: 1px solid var(--rule); border-radius: var(--r-2); }
.panel__head {
    display: flex; align-items: center; gap: var(--sp-3);
    padding: var(--sp-3) var(--sp-4); border-bottom: 1px solid var(--rule);
}
.panel__head-title {
    font-family: var(--ff-cond); font-size: var(--fs-s); font-weight: 700;
    letter-spacing: .1em; text-transform: uppercase;
}
.panel__body { padding: var(--sp-4); }

/* --- Sektionstrennung: Hairline plus Farbtick, keine Karte --- */
.section { margin: var(--sp-7) 0; }
.section-head { display: flex; align-items: baseline; gap: var(--sp-3); margin-bottom: var(--sp-4); }
.section-head__bar { width: 3px; height: 1.1em; background: var(--team); flex: none; }
.section-head__title {
    margin: 0; font-family: var(--ff-cond); font-size: var(--fs-l); font-weight: 700;
    letter-spacing: .06em; text-transform: uppercase;
}

/* --- Leere Datenmengen duerfen nicht kollabieren (Review-Focus 3) --- */
.tower:empty, .data-table:empty { display: none; }
.tower__empty, .data-table__empty {
    padding: var(--sp-6) var(--sp-4); text-align: left;
    color: var(--ink-2); border: 1px dashed var(--rule); border-radius: var(--r-2);
}
```

- [ ] **Step 2: `04-components.css` auf Bootstrap-Mapping reduzieren**

Diese Datei hält die Bootstrap-Klassen auf den neuen Tokens, damit hunderte Views ohne
Änderung weiterlaufen. Kernregeln (vollständig, weil jede einzelne eine
Kontrast-Entscheidung trägt):

```css
/* Ankerpunkt: das Menue ist der Weg zu allem anderen (Review-Focus 5). */
.dropdown-menu {
    background: var(--tarmac); border: 1px solid var(--rule-strong);
    border-radius: var(--r-2); padding: var(--sp-1) 0; min-width: 12rem;
}
.dropdown-item { color: var(--ink); padding: var(--sp-2) var(--sp-4); font-size: var(--fs-s); }
.dropdown-item:hover, .dropdown-item:focus { background: var(--kerb); color: var(--ink); }
.dropdown-divider { border-top-color: var(--rule); }

.btn { border-radius: var(--r-2); font-weight: 600; letter-spacing: .02em; }
.btn-primary { background: var(--team); border-color: var(--team); color: #fff; }
.btn-primary:hover { filter: brightness(1.12); }
.btn-secondary { background: var(--kerb); border-color: var(--rule-strong); color: var(--ink); }
.btn-ghost, .btn-outline-primary {
    background: transparent; border: 1px solid var(--rule-strong); color: var(--ink);
}
.btn-ghost:hover, .btn-outline-primary:hover { border-color: var(--ink); color: var(--ink); }
.btn-xs { font-size: var(--fs-xs); padding: .15rem .5rem; }

.card { background: var(--tarmac); border: 1px solid var(--rule); border-radius: var(--r-2); }
.card-header { background: transparent; border-bottom: 1px solid var(--rule); font-weight: 600; }

.form-control, .form-select {
    background: var(--track); border: 1px solid var(--rule-strong);
    border-radius: var(--r-2); color: var(--ink);
}
.form-control:focus, .form-select:focus {
    background: var(--track); color: var(--ink);
    border-color: var(--best); box-shadow: 0 0 0 3px color-mix(in srgb, var(--best) 25%, transparent);
}
.form-control::placeholder { color: var(--ink-3); }

.badge { background: var(--kerb); color: var(--ink); font-weight: 600; border-radius: var(--r-1); }
.alert { border-radius: var(--r-2); border: 1px solid var(--rule-strong); background: var(--tarmac); color: var(--ink); }
.alert-success { border-color: var(--best); }
.alert-warning { border-color: var(--slow); }
.alert-danger  { border-color: var(--flag); }

.pagination .page-link { background: var(--tarmac); border-color: var(--rule); color: var(--ink-2); }
.pagination .page-link:hover { background: var(--kerb); color: var(--ink); }
.pagination .active .page-link { background: var(--team); border-color: var(--team); color: #fff; }

.progress { background: var(--kerb); border-radius: var(--r-1); height: .5rem; }
.progress-bar { background: var(--team); }

.accordion-item { background: var(--tarmac); border: 1px solid var(--rule); }
.accordion-button { background: var(--tarmac); color: var(--ink); }
.accordion-button:not(.collapsed) { background: var(--kerb); color: var(--ink); box-shadow: none; }

.table { color: var(--ink); border-color: var(--rule); }
.table > :not(caption) > * > * { background: transparent; border-bottom-color: var(--rule); }
```

**Wichtig:** Bootstrap wird in Task 12 in das Bundle aufgenommen. Bis dahin bleibt
`bootstrap.min.css` als eigener `<link>`, damit die Reihenfolge stimmt (Bootstrap zuerst,
eigene Ebenen danach).

- [ ] **Step 3: Die `f1-btn`-Familie als Kompatibilitätsschicht weiterführen**

66 Vorkommen in den Views. In `04-components.css` ergänzen, sodass sie auf dieselben
Regeln zeigen wie `.btn`:

```css
.f1-btn {
    display: inline-flex; align-items: center; gap: var(--sp-2);
    padding: var(--sp-2) var(--sp-4); border-radius: var(--r-2);
    font-weight: 600; font-size: var(--fs-s); text-decoration: none;
    border: 1px solid transparent; cursor: pointer;
}
.f1-btn--primary { background: var(--team); color: #fff; }
.f1-btn--outline { background: transparent; border-color: var(--rule-strong); color: var(--ink); }
.f1-btn--ghost   { background: transparent; border-color: transparent; color: var(--ink-2); }
.f1-btn--ghost:hover { color: var(--ink); }
```

- [ ] **Step 4: Das Bundle nachziehen**

`wwwroot/css/site/04f-tower.css` in `Erdi-ERC.csproj` eintragen — **an beiden Stellen**.
Die Datei muss nach `04-components.css` liegen, damit ihre Regeln gewinnen.

- [ ] **Step 5: Tests und Build**

Run: `dotnet build Erdi-ERC/Erdi-ERC.csproj`
Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter "FullyQualifiedName~Jede_Css_Partial_Steht_Im_Bundle"`
Expected: PASS — die neue Datei ist in beiden Listen.

- [ ] **Step 6: Sichtprüfung des Dropdowns (Review-Focus 5)**

Run: `dotnet run --project Erdi-ERC --launch-profile http`
Expected: Ein Dropdown-Menü (z. B. im Profil oder Admin-Kopf) öffnen — Einträge sind
lesbar, Hover hebt sichtbar hervor, Kontrast Text/Hintergrund fällt nicht durch.
Da Task 4 die Hauptnavigation-Dropdowns entfernt hat, hier eine verbliebene Stelle
suchen (`grep -rn 'data-bs-toggle="dropdown"' Erdi-ERC/Views`).

- [ ] **Step 7: Commit**

```bash
git add Erdi-ERC/wwwroot/css/site/04-components.css Erdi-ERC/wwwroot/css/site/04f-tower.css Erdi-ERC/Erdi-ERC.csproj
git commit -m "feat(frontend): Turm-Primitive und Bootstrap-Mapping auf Race-Control"
```

---

## Task 6: Die Hauptseiten-CSS überführen (35 Views)

`wwwroot/css/pages/home-index.css` hat **~1040 Zeilen** und wird von **~35 Views** geladen
— sie ist das zweite Design-System, nicht Seiten-CSS. Bis sie umgestellt ist, sehen die
meisten öffentlichen Seiten alt aus, egal was Tasks 2–5 getan haben.

**Files:**
- Modify: `Erdi-ERC/wwwroot/css/pages/home-index.css`
- Delete: `Erdi-ERC/wwwroot/css/pages/home-index-fx.css`
- Modify: alle Views, die die FX-Datei verlinken (nur `Views/Home/Index.cshtml:35`)

**Interfaces:**
- Consumes: Tokens (Task 2), `.tower`-Familie (Task 5).
- Produces: neue Definitionen der Klassen `.f1-hero*`, `.f1-section*`, `.f1-stat*`,
  `.f1-leaguecard*`, `.f1-results-card`, `.f1-timing-card`, `.f1-page-hero`, `.reveal`.
  Die Klassennamen bleiben, damit die 35 Views unangetastet weiterlaufen.

- [ ] **Step 1: Erheben, welche Klassen aus dieser Datei wirklich benutzt werden**

```bash
cd /c/Users/basti/source/repos/Erdi-ERC/Erdi-ERC
grep -oE '^\s*\.[a-zA-Z][a-zA-Z0-9_-]*' wwwroot/css/pages/home-index.css | tr -d ' ' | sort -u > /tmp/homeidx-selectors.txt
while read -r sel; do
  n=$(grep -rl "${sel#.}" Views --include=*.cshtml 2>/dev/null | wc -l)
  [ "$n" -eq 0 ] && echo "TOT: $sel"
done < /tmp/homeidx-selectors.txt
```

Alles, was als `TOT` gemeldet wird, wird **gelöscht statt umgeschrieben**. Das ist der
größte Einzelgewinn in dieser Aufgabe.

- [ ] **Step 2: Die Überlebenden auf die Tokens umschreiben**

Abnahmekriterien — jede Zeile ist eine Prüfung, nicht ein Vorschlag:

- Kein `backdrop-filter`, kein `box-shadow` außer `0 0 0 1px var(--rule)`,
  kein `linear-gradient` außer dem Teamfarben-Verlauf und den Signalfarben-Markern.
- Keine Farb-Literale: `grep -nE '#[0-9a-fA-F]{3,8}|rgba?\(' wwwroot/css/pages/home-index.css`
  darf nur die Teamfarben-Blöcke und `var()`-Fallbacks zeigen.
- Jede verbleibende Regel benutzt ausschließlich Tokens aus `01-tokens.css`.
- `.f1-hero` wird zur Race-Control-Kopfzeile: Sessionname, Flaggenstatus, Countdown,
  Top-5-Vorschau — kein Logo-plus-Verlauf, keine Speedlines, keine Funken.
- `.f1-stat`, `.stat-tile`: Wert in `--fs-xl` tabellarisch, Label in `--fs-xs`,
  Versalien, `--ink-3`. Keine Icons in Kreisen, kein Farbverlauf.
- `.reveal`: **keine** `opacity: 0`-Regel mehr. Die Einblendung entfällt; die Klasse wird
  auf `display: block` neutralisiert, damit die Views nicht angefasst werden müssen.
- `.f1-section__bar` wird der `3px`-Farbtick aus `.section-head__bar`.
- `.f1-leaguecard`, `.f1-results-card`, `.f1-timing-card`: `.panel`-Optik — eine Fläche,
  eine 1-px-Kante, `--r-2`.

- [ ] **Step 3: `home-index-fx.css` löschen und die Verlinkung entfernen**

`Views/Home/Index.cshtml:33-36`: den `<link>` auf `home-index-fx.css` streichen.
Die Datei aus dem Repository nehmen. Sie steht **nicht** in der Bundle-Liste, es ist also
nur die View-Zeile zu entfernen.

- [ ] **Step 4: Build, Tests, Sichtprüfung über die betroffenen Seiten**

Run: `dotnet build Erdi-ERC/Erdi-ERC.csproj`
Run: `dotnet run --project Erdi-ERC --launch-profile http`
Expected: Home, `/Races/Results`, `/Races/AllRaces`, `/Stats/Erdi10`, `/Community/Events`,
`/Setups/TrackSetups`, `/Account/Login` und `/Profile` tragen alle die neue Gestalt.
Leere Ergebnisseiten (eine Liga ohne Daten) brechen nicht — **Review-Focus 3**.

- [ ] **Step 5: Commit**

```bash
git add Erdi-ERC/wwwroot/css/pages/home-index.css Erdi-ERC/Views/Home/Index.cshtml
git rm Erdi-ERC/wwwroot/css/pages/home-index-fx.css
git commit -m "refactor(frontend): Hauptseiten-CSS auf Race-Control-Tokens, FX-Datei entfernt"
```

---

## Task 7: `05`, `10`, `11` überführen und die FX-Klassen streichen

**Files:**
- Modify: `Erdi-ERC/wwwroot/css/site/05-f1-modules.css`
- Modify: `Erdi-ERC/wwwroot/css/site/10-f1-quickwins.css`
- Modify: `Erdi-ERC/wwwroot/css/site/11-unique-addons.css`
- Modify: `Erdi-ERC/wwwroot/css/site/08-animations.css`
- Modify: die Views, die verbotene Klassen im Markup tragen

**Interfaces:**
- Consumes: Task 5 (`.tower`, `.sector`, `.compound`).
- Produces: grüne `Verbotene_Klasse_Ist_Verschwunden`-Tests.

- [ ] **Step 1: Die verbotenen Klassen aufspüren**

```bash
cd /c/Users/basti/source/repos/Erdi-ERC/Erdi-ERC
for k in f1-speedline f1-spark chequered-sweep race-lights__chequers race-lights__scan \
         race-lights__speedlines f1-border-anim f1-holographic f1-lift f1-tiltable \
         f1-home-backdrop page-transition scroll-progress; do
  echo "== $k"; grep -rl "$k" Views wwwroot/css 2>/dev/null
done
```

- [ ] **Step 2: CSS-Regeln entfernen, Markup entfernen, JS-Aufrufer prüfen**

Für jede gefundene Stelle: Regel streichen (aus `05`/`10`/`11`/`08`) und das zugehörige
Markup in der View entfernen. Danach prüfen, ob eine der gestrichenen Klassen von JS
angesprochen wird:

```bash
grep -rn "tiltable\|home-backdrop\|page-transition\|scrollProgress" wwwroot/js Views/Shared/_Layout.cshtml
```

Was hier auftaucht, ist in Task 10 zu entfernen — die Liste dort ergänzen.

- [ ] **Step 3: `.sector`/`.tire`/`.lap-counter` aus `10-f1-quickwins.css` auf die neue Implementierung umbiegen**

`10-f1-quickwins.css` enthält bereits `.sector`, `.tire` und `.lap-counter`. Die alten
Regeln für `.sector` und `.tire` werden **gelöscht** — die neuen stehen in
`04f-tower.css` und gewinnen durch die spätere Position im Bundle. `.lap-counter` wird auf
die Tokens gezogen (Wert tabellarisch, Label Versalien) und behalten.

- [ ] **Step 4: Die restlichen Regeln der drei Dateien auf Tokens ziehen**

Abnahme: keine Farb-Literale außerhalb der Teamfarben-Blöcke, keine Schlagschatten,
kein `border-radius` über `--r-2`, `prefers-reduced-motion` respektiert
(`08-animations.css` bekommt am Ende die globale Regel aus `02-base.css` — lokale
Animationen dort einzeln abschalten).

- [ ] **Step 5: Tests laufen lassen**

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter "FullyQualifiedName~Verbotene_Klasse_Ist_Verschwunden"`
Expected: **PASS für alle 13.** Bleibt einer rot, zeigt der Fehlschlag die Datei — dort
weitermachen.

- [ ] **Step 6: Commit**

```bash
git add -A Erdi-ERC/wwwroot/css Erdi-ERC/Views
git commit -m "refactor(frontend): FX-Klassen entfernt, Modul- und Quickwin-Ebenen auf Tokens"
```

---

## Task 8: Die geteilten Partials

**Files:**
- Modify: `Views/Shared/_F1PageHero.cshtml`, `_F1Section.cshtml`, `_F1StatTileInner.cshtml`,
  `_F1LeagueCard.cshtml`, `_F1PitCard.cshtml`, `_DriverCard.cshtml`, `_LiveRaceBanner.cshtml`,
  `_AdminPageHero.cshtml`, `_AdminHeroCard.cshtml`
- Modify: `Erdi-ERC/wwwroot/css/driver-card.css`

**Interfaces:**
- Consumes: `.tower`-Familie (Task 5), Tokens (Task 2).
- Produces: unveränderte `@model`-Typen — die VMs bleiben, wie sie sind
  (`F1PageHeroVm`, `F1SectionVm`, `F1StatTileVm`, `F1PitCardVm`, `F1LeagueCardVm`).

**Wichtig:** Die Partials sind reine Markup-Erzeuger. Die `@model`-Signaturen werden
**nicht** geändert, sonst brechen die aufrufenden Views.

- [ ] **Step 1: `_F1PageHero.cshtml` auf die Race-Control-Kopfzeile umbauen**

`F1PageHeroVm` behält seine Felder. Das Markup wird:

```html
<section class="f1-hero">
    @if (Model.Telemetry) { <div class="f1-hero__rule" aria-hidden="true"></div> }
    @if (!string.IsNullOrWhiteSpace(Model.Eyebrow))
    {
        <span class="f1-hero__eyebrow">
            @if (Model.LiveDot) { <span class="live-dot" aria-hidden="true"></span> }
            @Model.Eyebrow
        </span>
    }
    <h1 class="f1-hero__title">
        @Model.Title
        @if (!string.IsNullOrWhiteSpace(Model.Highlight)) { <em>@Model.Highlight</em> }
    </h1>
    @if (!string.IsNullOrWhiteSpace(Model.Lead)) { <p class="f1-hero__lead">@Model.Lead</p> }
    @if (Model.Actions.Count > 0)
    {
        <div class="f1-hero__actions">
            @foreach (var a in Model.Actions)
            {
                <a class="f1-btn f1-btn--@(a.Variant ?? "ghost")"
                   href="@a.Href" target="@a.Target">
                    @if (!string.IsNullOrWhiteSpace(a.IconLucide))
                    { <i class="f1-icon" data-lucide="@a.IconLucide"></i> }
                    @a.Label
                </a>
            }
        </div>
    }
</section>
```

`.f1-hero__chequer` und die Telemetrie-Ebene entfallen; `.f1-hero__rule` ist eine
einzelne 1-px-Linie mit Teamfarben-Tick.

- [ ] **Step 2: `_F1StatTileInner.cshtml` auf die Datenkachel bringen**

Wert in `--fs-xl`, `font-variant-numeric: var(--num)`, Label in `--fs-xs` Versalien
`--ink-3`. Positions-Badge (`Model.Pos`) wird zum `--ink-3`-Präfix links vom Wert, keine
farbige Blase.

- [ ] **Step 3: Die übrigen Partials nach demselben Muster**

`_F1Section` → `.section-head`-Struktur. `_F1PitCard` → `.panel`. `_F1LeagueCard` →
`.panel` mit Teamfarben-Tick. `_DriverCard` → `.panel` mit `.compound`-Reihe; dessen
Inline-Styles (**64 Vorkommen**) werden zu CSS-Klassen in `driver-card.css` — das ist der
größte Einzelposten der Inline-Style-Aufräumung und gehört deshalb hierher.
`_LiveRaceBanner` → `.strip`-Ableger. `_AdminPageHero`/`_AdminHeroCard` → dieselbe Optik,
aber mit `adm-*`-Klassen, da sie im Admin-Namespace leben.

- [ ] **Step 4: Build und Sichtprüfung jeder Seite, die ein Partial benutzt**

Run: `dotnet build Erdi-ERC/Erdi-ERC.csproj`
Run: `grep -rln '_F1PageHero\|_F1PitCard\|_F1LeagueCard\|_F1StatTile\|_DriverCard\|_LiveRaceBanner' Views`
Expected: jede gefundene Seite lädt und zeigt das neue Partial ohne Bruch.

- [ ] **Step 5: Commit**

```bash
git add Erdi-ERC/Views/Shared Erdi-ERC/wwwroot/css/driver-card.css
git commit -m "refactor(frontend): geteilte Partials auf Race-Control, DriverCard-Inline-Styles entfernt"
```

---

## Task 9: Die Startseite als Referenz-Umsetzung

**Files:**
- Modify: `Erdi-ERC/Views/Home/Index.cshtml`

**Interfaces:**
- Consumes: `.f1-hero` (Task 6/8), `.panel` und `.strip` (Tasks 4/5), `.flagstate` (Task 5).
- Produces: die Referenz, an der sich die folgenden Seiten orientieren.
- **Nicht** abhängig von Task 11 — der Turm kommt erst dort und erscheint dann über das
  Layout automatisch auch auf dieser Seite.

- [ ] **Step 1: Die FX-Blöcke aus dem Markup entfernen**

In `Views/Home/Index.cshtml` entfallen:
- `.f1-hero__speedlines` mit den sechs `.f1-speedline`-Spans (Zeilen 50–57),
- `.f1-hero__sparks` mit den zwölf `.f1-spark`-Spans (Zeilen 60–73),
- `.f1-hero__ghost`, `.f1-hero__chequer`, `.f1-hero__scanline`, `.f1-hero__depth-grid`,
- `.f1-home-backdrop` (Zeile 39),
- alle `f1-chip--drs`/`--fastest`-Dekorationschips.

- [ ] **Step 2: Den Hero auf die Race-Control-Kopfzeile setzen**

Struktur: Session-Zeile (Liga + Flaggenstatus) → Countdown zum nächsten Rennen →
Top-5-Vorschau im Turm → zwei Aktionen. Der Countdown bleibt das vorhandene
`#nextStreamCountdown`-Markup, damit das JS weiterläuft (`data-target`).

```html
<section class="f1-hero">
    <span class="f1-hero__eyebrow">
        @if (nextUpcomingEvent is not null) { <span class="live-dot" aria-hidden="true"></span> }
        F1 26 &middot; Sim Racing League
    </span>
    <h1 class="f1-hero__title">Erdi&#x27;s <em>Racing</em> Community</h1>
    <p class="f1-hero__lead">
        Drei Ligen. Ein Ziel. Bestenlisten, Ergebnisse und kommende Events der ERC
        rund um Twitch-Streamer <a href="https://twitch.tv/erdi10" target="_blank" rel="noopener">Erdi10</a>.
    </p>
    <div class="f1-hero__actions">
        <a class="f1-btn f1-btn--primary" asp-controller="Stats" asp-action="Erdi10">Zum Racing Hub</a>
        <a class="f1-btn f1-btn--outline" href="https://twitch.tv/erdi10" target="_blank" rel="noopener">Auf Twitch</a>
    </div>
</section>
```

- [ ] **Step 3: Den Statusblock einsetzen, wo heute Broadcast-Card und Hero-Logo stehen**

Ein Panel mit Session, Flaggenstatus und Countdown zum nächsten Rennen:

```html
@if (nextUpcomingEvent is not null || nextStream is not null)
{
    <div class="panel">
        <div class="panel__head">
            <span class="panel__head-title">N&auml;chstes Rennen</span>
            @if (nextUpcomingEvent is not null)
            {
                <span class="flagstate flagstate--green">Ready</span>
            }
        </div>
        <div class="panel__body">
            @if (nextStream is not null)
            {
                <div class="u-num" id="nextStreamCountdown"
                     data-target="@(((DateTime)nextStream.StartAt).ToString("O"))"
                     style="font-size:var(--fs-xl)">Countdown l&auml;dt&hellip;</div>
                <div class="text-mute">@nextStream.Title</div>
            }
            <a class="f1-btn f1-btn--outline mt-3" asp-controller="Stats" asp-action="Erdi10">Zur Meisterschaft</a>
        </div>
    </div>
}
```

**Bewusste Abweichung von der Spec:** Die Spec nennt für den Hero eine
Top-5-Turm-Vorschau. Sie entfällt hier, weil der Turm als feste linke Spalte ohnehin auf
derselben Seite steht (Task 9) — zweimal dieselbe Wertung übereinander wäre Redundanz.
Unter 1200 px ist die Spalte ausgeblendet; dort führt der Button „Zur Meisterschaft" zur
vollständigen Wertung. Das ist der einzige Ort, an dem der Plan von der Spec abweicht.

- [ ] **Step 4: Build und Sichtprüfung**

Run: `dotnet build Erdi-ERC/Erdi-ERC.csproj && dotnet run --project Erdi-ERC --launch-profile http`
Expected: Startseite ohne Funken, Speedlines, Ghost-Text und Glasflächen. Der LCP
(Überschrift) erscheint sofort.

- [ ] **Step 5: Commit**

```bash
git add Erdi-ERC/Views/Home/Index.cshtml
git commit -m "feat(frontend): Startseite als Race-Control-Referenz"
```

---

## Task 10: Die Ampel aus dem kritischen Pfad holen

Heute setzt das Layout `#appMain` auf `opacity: 0`, solange die Sequenz läuft, und blendet
den Inhalt erst danach ein. Das schiebt den LCP um die Dauer der Sequenz nach hinten —
der teuerste Einzelposten im wahrgenommenen Tempo.

**Files:**
- Modify: `Erdi-ERC/Views/Shared/_Layout.cshtml` (der Block „Page transition",
  der Block „Race-Start Lights" und die `.race-lights`-Regeln)

**Interfaces:**
- Consumes: nichts aus früheren Tasks.
- Produces: grüner `AppMain_Wird_Nicht_Versteckt`-Test.

- [ ] **Step 1: Den Test rot sehen**

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter "FullyQualifiedName~AppMain_Wird_Nicht_Versteckt"`
Expected: FAIL.

- [ ] **Step 2: Den „Page transition"-Block vollständig entfernen**

Aus `_Layout.cshtml` den Kommentar `// ---------- Page transition (enter on load, leave on internal nav) ----------`
und den gesamten `(function () { ... })();`-Block darunter löschen (Zeilen 726–810),
inklusive der `MutationObserver`-Logik, die `main.style.opacity` setzt.

- [ ] **Step 3: Den `<main>`-Aufruf entschärfen**

```html
<!-- vorher: <main role="main" id="appMain" class="page-transition is-entering"> -->
<main role="main" id="appMain" class="page">
```

- [ ] **Step 4: Die Ampel auf 1.2 s kürzen und die Icons/Layer entfernen**

Im Block „Race-Start Lights":
- `.race-lights__bg` mit `__chequers`, `__speedlines`, `__scan` und die zwölf
  `__icons`-Einträge aus dem Markup entfernen,
- das Auto-SVG durch ein neutrales, kleineres Standbild ersetzen **oder** entfernen —
  das SVG trägt die Teamfarben, was einen Server-Roundtrip wert ist; einfacher ist, es zu
  entfernen und das Rig auf die fünf Lampen plus Beschriftung zu reduzieren,
- `const SLOW_THRESHOLD_MS = 260;` auf `0` setzen und die Slow-Navigation-Überlagerung
  (der zweite Teil des Blocks, ab `// ---- Slow-navigation overlay ----`) entfernen,
- `setTimeout(tick, 480)` auf `setTimeout(tick, 220)` und den Hold von
  `400 + Math.random() * 800` auf `260` setzen — das ergibt 5 × 220 ms + 260 ms ≈ 1.36 s.

- [ ] **Step 5: Den Scroll-Progress-Balken entfernen**

`<div class="scroll-progress" id="scrollProgress">` und im Scroll-Handler die Zeile
`if (bar) bar.style.width = ...` streichen; `const bar` mit entfernen. Der Handler bleibt
nur für `nav.classList.toggle('is-scrolled', …)` — wandert der Streifen später auf eine
reine CSS-Lösung, fällt er ganz weg.

- [ ] **Step 6: Den `reveal`-IntersectionObserver entfernen**

Der Block `// ---------- Reveal on scroll ----------` entfällt; `.reveal` ist in Task 6
neutralisiert worden.

- [ ] **Step 7: Test, Build, Sichtprüfung**

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter "FullyQualifiedName~AppMain_Wird_Nicht_Versteckt"`
Expected: **PASS**.

Run: `dotnet build Erdi-ERC/Erdi-ERC.csproj && dotnet run --project Erdi-ERC --launch-profile http`
Expected: Inhalt ist sofort da, die Ampel liegt darüber und verschwindet nach ~1.4 s.
Bei `prefers-reduced-motion` erscheint sie gar nicht.

**CLS prüfen:** Im DevTools-Performance-Panel neu laden — der Inhalt darf nicht
nachträglich springen, wenn die Ampel verschwindet.

- [ ] **Step 8: Commit**

```bash
git add Erdi-ERC/Views/Shared/_Layout.cshtml
git commit -m "perf(frontend): Ampel als Ueberlagerung statt Inhalts-Blockade, Sequenz auf 1.2s"
```

---

## Task 11: Der Turm — Daten und Einbindung

Der Turm steht auf jeder öffentlichen Seite. Die Daten liegen heute nicht im Layout
vor; deshalb die eng begrenzte Service-Erweiterung.

**Files:**
- Modify: `Erdi-ERC/Services/ILayoutDataService.cs`
- Modify: `Erdi-ERC/Services/LayoutDataService.cs`
- Create: `Erdi-ERC/Models/UiComponents/TowerVm.cs`
- Create: `Erdi-ERC/Views/Shared/_Tower.cshtml`
- Modify: `Erdi-ERC/wwwroot/css/site/03-layout.css` (`.app-shell` als Grid)
- Modify: `Erdi-ERC/Views/Shared/_Layout.cshtml` (Turm einbinden)
- Test: `tests/Erdi-ERC.Tests/Services/LayoutDataServiceTowerTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record TowerRowVm(int Pos, string DriverName, string TeamKey, string TeamName, int Points);`
  - `public sealed record TowerVm(string Title, IReadOnlyList<TowerRowVm> Rows, int MaxRows = 10);`
  - `LayoutData` bekommt das Feld `IReadOnlyList<TowerRowVm> TopDrivers` **als letztes**
    Feld, damit die vorhandenen `with`-Ausdrücke weiterlaufen.

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

```csharp
using Erdi_ERC.Models.UiComponents;

namespace Erdi_ERC.Tests.Services;

public class LayoutDataServiceTowerTests
{
    [Fact]
    public void TowerVm_Kuerzt_Auf_MaxRows_Und_Behaelt_Die_Reihenfolge()
    {
        var rows = Enumerable.Range(1, 20)
            .Select(i => new TowerRowVm(i, $"Fahrer{i}", "ferrari", "Ferrari", 100 - i))
            .ToArray();

        var vm = new TowerVm("Meisterschaft", rows, maxRows: 10);

        Assert.Equal(10, vm.Rows.Count);
        Assert.Equal(1, vm.Rows[0].Pos);
        Assert.Equal(10, vm.Rows[^1].Pos);
    }

    [Fact]
    public void TowerVm_Vertraegt_Leere_Liste()
    {
        var vm = new TowerVm("Meisterschaft", Array.Empty<TowerRowVm>(), maxRows: 10);
        Assert.Empty(vm.Rows);
    }
}
```

- [ ] **Step 2: Test laufen lassen — er muss fehlschlagen**

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter "FullyQualifiedName~LayoutDataServiceTowerTests"`
Expected: FAIL — `TowerVm`/`TowerRowVm` existieren nicht (Compile-Fehler).

- [ ] **Step 3: `TowerVm.cs` anlegen**

```csharp
namespace Erdi_ERC.Models.UiComponents;

public sealed record TowerRowVm(
    int Pos,
    string DriverName,
    string TeamKey,
    string TeamName,
    int Points);

/// <summary>
/// Kein positionaler Record: die Zeilen werden beim Erzeugen gekuerzt und sortiert,
/// das geht mit einem positionalen Parameter und einer gleichnamigen Eigenschaft nicht.
/// </summary>
public sealed record TowerVm
{
    public TowerVm(string title, IReadOnlyList<TowerRowVm>? rows, int maxRows = 10)
    {
        Title = title;
        MaxRows = maxRows;
        Rows = (rows ?? Array.Empty<TowerRowVm>())
            .OrderBy(r => r.Pos)
            .Take(maxRows)
            .ToArray();
    }

    public string Title { get; }
    public IReadOnlyList<TowerRowVm> Rows { get; }
    public int MaxRows { get; }
}
```

- [ ] **Step 4: Test laufen lassen — er muss jetzt bestehen**

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter "FullyQualifiedName~LayoutDataServiceTowerTests"`
Expected: PASS.

- [ ] **Step 5: `LayoutData` und den Dienst erweitern**

In `ILayoutDataService.cs` das Record um das letzte Feld ergänzen:

```csharp
public sealed record LayoutData(
    string? WinnerTeamKey,
    string WinnerCarPrimary,
    string WinnerCarPrimaryLight,
    string WinnerCarPrimaryDark,
    string WinnerCarSecondary,
    StreamSchedule? ActiveStream,
    DateTime? LatestSetupActivityUtc,
    IReadOnlyList<Erdi_ERC.Models.UiComponents.TowerRowVm> TopDrivers);
```

In `LayoutDataService.cs`:
- `EmptyData` um `Array.Empty<TowerRowVm>()` als achtes Argument erweitern,
- `CacheKey` von `"layout-data:v1"` auf `"layout-data:v2"` heben (das Record hat sich
  geändert; ein alter Cache-Eintrag hätte ein anderes Shape),
- in `GetLayoutDataAsync` die Top 10 ermitteln. Die Rangfolge-Quelle ist die vorhandene
  Gesamtwertung — der Executor liest die bestehende Abfrage aus
  `Services/StatsService.cs` bzw. dem Standings-Export und verwendet **dieselbe**
  Reihenfolge, damit Turm und Ergebnisseite nie auseinanderlaufen. Teamzuordnung über
  `F1TeamsHelper.GetCssKeyForDriver(driverName)`; liefert das `null`, wird
  `TeamKey = "unknown"` und `--team` fällt auf `--ink-2` zurück.

- [ ] **Step 6: Das Turm-Partial schreiben**

```html
@model Erdi_ERC.Models.UiComponents.TowerVm
@*
    Turm — Top N der Meisterschaft. Steht auf jeder oeffentlichen Seite.
    Bewusst eine echte Tabelle mit th scope: die Daten sind tabellarisch.
*@
@if (Model.Rows.Count == 0)
{
    <p class="tower__empty">Noch keine Wertung — die Saison hat nicht begonnen.</p>
}
else
{
    <table class="tower">
        <caption>@Model.Title</caption>
        <thead>
            <tr>
                <th scope="col">P</th>
                <th scope="col"><span class="visually-hidden">Team</span></th>
                <th scope="col">Fahrer</th>
                <th scope="col" class="tower__pts">Pkt</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var r in Model.Rows)
            {
                <tr class="tower__row--team-@r.TeamKey">
                    <td class="tower__pos">@r.Pos</td>
                    <td class="tower__team" style="--team:var(--team-@r.TeamKey, var(--ink-2))"></td>
                    <td class="tower__name" title="@r.DriverName">@r.DriverName</td>
                    <td class="tower__pts">@r.Points</td>
                </tr>
            }
        </tbody>
    </table>
}
```

**Zu `--team-@r.TeamKey`:** Statt elf `body.team-*`-Blöcke nur im Layout zu nutzen,
definiert `01-tokens.css` zusätzlich elf Variablen `--team-redbull: #3671C6;` usw. Damit
kann eine beliebige Zeile unabhängig vom `body`-Team eingefärbt werden — der Turm zeigt
ja viele Teams gleichzeitig. Diese elf Variablen in Task 2 nachtragen, falls noch nicht
geschehen.

- [ ] **Step 7: Das Grid im Layout aufbauen**

In `03-layout.css`:

```css
.app-shell { display: block; }
@media (min-width: 1200px) {
    .app-shell {
        display: grid;
        grid-template-columns: var(--rail) minmax(0, 1fr);
        gap: var(--sp-6);
        align-items: start;
    }
    .app-shell__rail { position: sticky; top: 72px; }
}
@media (max-width: 1199.98px) {
    .app-shell__rail { display: none; }   /* unter 1200px kein Turm */
}
```

In `_Layout.cshtml` um `<main>` herum einwickeln und die Turm-Spalte einsetzen.
Der Admin ist davon nicht betroffen — er hat eine eigene Shell.

- [ ] **Step 8: Sichtprüfung inklusive der beiden Review-Focus-Fälle**

Run: `dotnet build Erdi-ERC/Erdi-ERC.csproj && dotnet run --project Erdi-ERC --launch-profile http`
Expected:
- Der Turm steht links auf Home, Results, AllRaces, Erdi10, Events, TrackSetups, Kalender.
- **Review-Focus 2 (Cadillac):** Ist Cadillac in den Top 10, ist dessen Balken sichtbar
  (Rückfall auf `--ink-2`), nicht unsichtbar.
- **Review-Focus 4 (lange Namen):** Ein Name mit ≥ 20 Zeichen wird abgeschnitten
  (Ellipse), die Zeilenhöhe bleibt bei `2.25rem`, nichts springt.
- Unter 1200 px Breite ist der Turm ausgeblendet, das Layout ist einspaltig.
- Eine leere Wertung zeigt den Hinweistext, keinen leeren Kasten.

- [ ] **Step 9: Commit**

```bash
git add Erdi-ERC/Services Erdi-ERC/Models/UiComponents/TowerVm.cs \
        Erdi-ERC/Views/Shared/_Tower.cshtml Erdi-ERC/Views/Shared/_Layout.cshtml \
        Erdi-ERC/wwwroot/css/site/03-layout.css \
        tests/Erdi-ERC.Tests/Services/LayoutDataServiceTowerTests.cs
git commit -m "feat(frontend): Turm mit Top-10-Wertung auf jeder oeffentlichen Seite"
```

---

## Task 12: Assets, Admin-Gleichstand und Aufräumen

**Files:**
- Modify: `Erdi-ERC/Erdi-ERC.csproj` (Bootstrap ins Bundle, tote Dateien aus beiden Listen)
- Create: `Erdi-ERC/wwwroot/lib/bootstrap-icons/` (beschnitten)
- Modify: `Erdi-ERC/Views/Home/Index.cshtml` (Hero-Bild über `_Picture`)
- Modify: `Erdi-ERC/Views/Admin/_AdminLayout.cshtml` (gebündeltes Stylesheet)
- Delete: `wwwroot/css/pages/driver-detail.css`, `wwwroot/css/site.community.css`,
  `wwwroot/css/site.track-setups.css`

- [ ] **Step 1: Alle benutzten Bootstrap-Icons sammeln und beschneiden**

```bash
cd /c/Users/basti/source/repos/Erdi-ERC/Erdi-ERC
grep -rhoE 'bi bi-[a-z0-9-]+' Views --include=*.cshtml | sort -u | sed 's/^bi //' > /tmp/bi-icons.txt
wc -l /tmp/bi-icons.txt
```

Aus `wwwroot/lib/bootstrap/` (bzw. dem npm-Paket) die `bootstrap-icons.woff2` nehmen und
mit `glyphhanger` oder `fonttools pyftsubset` auf die Namen aus `/tmp/bi-icons.txt`
beschneiden:

```bash
python -m pip install fonttools brotli
python -m fontTools.subset wwwroot/lib/bootstrap-icons/bootstrap-icons.woff2 \
    --unicodes-file=/tmp/bi-icons-unicodes.txt \
    --flavor=woff2 \
    --output-file=wwwroot/lib/bootstrap-icons/bootstrap-icons-subset.woff2
```

Die Namen in Codepoints übersetzen — `bootstrap-icons.css` enthält sie als
`.bi-<name>::before { content: "\f102"; }`. Das Skript bricht ab, wenn ein Name nicht
auflöst; ein stillschweigend fehlendes Icon wäre später nur als leere Fläche sichtbar:

```bash
python - <<'PY'
import re, pathlib
css = pathlib.Path('wwwroot/lib/bootstrap-icons/bootstrap-icons.css').read_text(encoding='utf-8')
codepoints = {m.group(1): int(m.group(2), 16)
              for m in re.finditer(r'\.bi-([a-z0-9-]+)::before\s*\{\s*content:\s*"\\([0-9a-fA-F]+)"', css)}
gesucht = [l.strip() for l in pathlib.Path('/tmp/bi-icons.txt').read_text().splitlines() if l.strip()]
fehlend = [n for n in gesucht if n not in codepoints]
if fehlend:
    raise SystemExit('Nicht in bootstrap-icons.css gefunden: ' + ', '.join(fehlend))
cps = sorted({codepoints[n] for n in gesucht})
pathlib.Path('/tmp/bi-icons-unicodes.txt').write_text(','.join(f'U+{c:04X}' for c in cps))
print(f'{len(gesucht)} Icons -> {len(cps)} Codepoints -> /tmp/bi-icons-unicodes.txt')
PY
```

Danach die `@font-face`-Regel mit
`src: url('/lib/bootstrap-icons/bootstrap-icons-subset.woff2')` in `01-tokens.css`
eintragen und die CDN-`<link>` in beiden Layouts entfernen.

**Prüfen (Review-Focus aus der Risikoliste):** Nach dem Beschneiden jede Hauptseite
laden und auf leere Flächen achten, wo ein Icon stehen sollte. Fehlt eines, den Namen
in `/tmp/bi-icons.txt` ergänzen und erneut beschneiden.

- [ ] **Step 2: Bootstrap ins Bundle aufnehmen**

In `Erdi-ERC.csproj` `wwwroot/lib/bootstrap/dist/css/bootstrap.min.css` als **ersten**
Eintrag in beide Bundle-Listen aufnehmen. Danach die `<link>`-Zeile auf
`bootstrap.min.css` aus `_Layout.cshtml` und `_AdminLayout.cshtml` entfernen.

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter "FullyQualifiedName~Jede_Css_Partial_Steht_Im_Bundle"`
Expected: **FAIL** — der Test vergleicht nur `wwwroot/css/site/`. Den Test in
`DesignContractTests` um die Bootstrap-Ausnahme erweitern: die Zeile
`Assert.Equal(aufPlatte, imBundle)` überspringt Dateien, die nicht unter `css/site/`
liegen. Erst den Test anpassen, dann die Bundle-Liste — der Test ist die Spezifikation.

- [ ] **Step 3: Den Admin auf das gebündelte Stylesheet umstellen**

In `_AdminLayout.cshtml:81` `~/css/site.css` durch `~/css/site.bundled.min.css` ersetzen
(mit `asp-append-version="true"`), passend zum öffentlichen Layout.

- [ ] **Step 4: Das Hero-Bild über `_Picture` ausliefern**

In `Views/Home/Index.cshtml` das Logo-`<img>` bzw. den `PreloadHero`-Pfad durch das
vorhandene `_Picture`-Partial mit `Src`, `Width`, `Height`, `Sizes` und
`Loading="eager"` ersetzen. Eine AVIF/WebP-Fassung neben das PNG legen; `_Picture`
wählt über `Retina`/`ExtraAttrs` die Quelle. Das `preload` in `_Layout.cshtml` zeigt
weiterhin auf die PNG-Fassung, solange die moderne Variante nicht überall greift.

- [ ] **Step 5: Tote Dateien und Bundle-Liste aufräumen**

```bash
git rm Erdi-ERC/wwwroot/css/pages/driver-detail.css \
       Erdi-ERC/wwwroot/css/site.community.css \
       Erdi-ERC/wwwroot/css/site.track-setups.css
```

`site.community.css` und `site.track-setups.css` stehen in **beiden** Bundle-Listen und
müssen dort entfernt werden, sonst bricht der Build.

Run: `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj --filter "FullyQualifiedName~DesignContractTests"`
Expected: **alle grün.**

- [ ] **Step 6: Die Performance-Budgets messen**

Run: `dotnet build Erdi-ERC/Erdi-ERC.csproj -c Release && dotnet run --project Erdi-ERC -c Release --launch-profile http`
Dann im Browser (Chrome DevTools MCP): Lighthouse-Audit und ein Performance-Trace auf
`/` und `/Races/Results`, gegen ein gedrosseltes Mobilprofil.

Expected:
- LCP < 1.5 s, CLS < 0.05, INP < 200 ms,
- CSS gesamt < 60 KB gzip in **einem** Request,
- **keine** Anfrage an eine fremde Domain,
- das LCP-Element ist deutlich vor dem Ende der Ampel-Sequenz gemalt.

Werden die Budgets gerissen: zuerst die Bundle-Größe prüfen (`ls -l site.bundled.min.css`,
gzip messen), dann die Zahl der Requests. Nicht am Design drehen, bevor die Zahlen
gelesen sind.

- [ ] **Step 7: Den Durchklick über die Hauptpfade machen**

Home, Racing Hub, Results, Alle Rennen, Renn-Detail, Setups, Events, Bewerbung, Kalender,
Login, Profil, plus eine Admin-Seite. Jede Seite: lädt, trägt die neue Gestalt, Turm
vorhanden (außer Admin), keine leeren Icon-Flächen.

- [ ] **Step 8: Commit**

```bash
git add -A Erdi-ERC tests
git commit -m "perf(frontend): Bootstrap und Icons gebuendelt, Admin angeglichen, tote CSS entfernt"
```

---

## Abschluss

Nach Task 12:

- Alle `DesignContractTests` sind grün.
- `dotnet test tests/Erdi-ERC.Tests/Erdi-ERC.Tests.csproj` ist grün (die 200+ bestehenden
  Tests dürfen nicht gebrochen sein — das ist die Regression, die zählt).
- Die Performance-Budgets sind **gemessen**, nicht geschätzt.
- Deploy ist **nicht** Teil dieses Plans: `dotnet publish` + `robocopy T:\` +
  `systemctl restart` liegt beim Auftraggeber (siehe `Docs/Features/production-deploy.md`,
  inklusive der Schema-vor-Restart-Regel — hier nicht relevant, da keine Migration
  dazukommt).

**Noch offen und bewusst nicht in diesem Plan:** die ~40 öffentlichen Seiten einzeln
bewusst zu gestalten, der Admin-Bereich über die Token-Vererbung hinaus, und die
verbleibenden Inline-Styles außerhalb von `_DriverCard`.
