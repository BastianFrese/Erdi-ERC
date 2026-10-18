using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Erdi_ERC.Middleware;

/// <summary>
/// Liefert die vorkomprimierten Dateien aus, die das StaticWebAssets-SDK beim
/// Publish erzeugt (site.bundled.min.css.br/.gz und alles andere aus
/// CompressionIncludePatterns).
///
/// Warum ueberhaupt: die App komprimiert Antworten zur Laufzeit, aber mit
/// CompressionLevel.Fastest. Gemessen lag das gebundelte CSS damit bei 88 KB
/// (gzip) bzw. 85 KB (brotli) auf der Leitung, waehrend die danebenliegenden
/// vorkomprimierten Dateien 53 KB bzw. 44 KB gross sind. Vorkomprimiert
/// ausliefern kostet ausserdem keine CPU pro Anfrage.
///
/// Drei Fallen, die hier umgangen werden:
///  1. ".br" ist kein bekannter MIME-Typ. StaticFileMiddleware antwortet darauf
///     mit 404, solange ServeUnknownFileTypes aus ist — und das soll aus
///     bleiben. Deshalb setzt <see cref="InhaltstypAnbieter"/> den Typ der
///     ORIGINALdatei.
///  2. Die Cache-Regeln in Program.cs pruefen den Pfad auf ".css"/".js" am Ende.
///     Nach dem Umschreiben endet er auf ".br" — der Originalpfad liegt deshalb
///     in <see cref="OriginalerPfad"/> fuer OnPrepareResponse bereit.
///  3. Bereichsanfragen (Range) werden uebersprungen: ein Teilbereich der
///     komprimierten Datei ist nicht derselbe Teilbereich des Originals.
///
/// Fehlt die komprimierte Datei (im Entwicklungsbetrieb, wo nur publish sie
/// erzeugt), passiert nichts — die Antwort geht unveraendert weiter und die
/// Laufzeitkompression greift wie bisher.
/// </summary>
public sealed class PrecompressedStaticFilesMiddleware
{
    /// <summary>Schluessel fuer <c>HttpContext.Items</c>: der Pfad vor dem Umschreiben.</summary>
    public static readonly object OriginalerPfad = new();

    private static readonly string[] Kodierungen = { "br", "gzip" };

    private readonly RequestDelegate _next;
    private readonly IFileProvider _dateien;

    public PrecompressedStaticFilesMiddleware(RequestDelegate next, IWebHostEnvironment umgebung)
    {
        _next = next;
        _dateien = umgebung.WebRootFileProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var pfad = context.Request.Path.Value;
        if (string.IsNullOrEmpty(pfad)
            || pfad.EndsWith(".br", StringComparison.OrdinalIgnoreCase)
            || pfad.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.Request.Headers.ContainsKey(HeaderNames.Range))
        {
            await _next(context);
            return;
        }

        var passend = PassendeKodierung(context.Request.Headers.AcceptEncoding);
        if (passend is null)
        {
            await _next(context);
            return;
        }

        var (endung, kodierung) = passend.Value;
        var komprimiert = _dateien.GetFileInfo(pfad + endung);
        if (!komprimiert.Exists || komprimiert.IsDirectory)
        {
            await _next(context);
            return;
        }

        context.Items[OriginalerPfad] = pfad;
        context.Response.Headers.ContentEncoding = kodierung;

        // Ohne Vary kann ein Zwischenspeicher die brotli-Fassung an einen Client
        // ausliefern, der sie nicht lesen kann.
        context.Response.Headers.Append(HeaderNames.Vary, "Accept-Encoding");

        var original = context.Request.Path;
        context.Request.Path = original + endung;
        try
        {
            await _next(context);
        }
        finally
        {
            context.Request.Path = original;
        }
    }

    /// <summary>
    /// Beste gemeinsame Kodierung. Bewusst einfach gehalten: "q=0" schliesst aus,
    /// sonst gewinnt br vor gzip. Reicht fuer die ueblichen Browser; die volle
    /// Qualitaetsbewertung waere hier Aufwand ohne Nutzen.
    /// </summary>
    private static (string Endung, string Kodierung)? PassendeKodierung(StringValues akzeptiert)
    {
        var wert = akzeptiert.ToString();
        if (string.IsNullOrEmpty(wert)) return null;

        foreach (var name in Kodierungen)
        {
            var stelle = wert.IndexOf(name, StringComparison.OrdinalIgnoreCase);
            if (stelle < 0) continue;

            var rest = wert[stelle..];
            if (rest.StartsWith(name + ";q=0", StringComparison.OrdinalIgnoreCase)
                || rest.StartsWith(name + " ;q=0", StringComparison.OrdinalIgnoreCase))
                continue;

            return (name == "br" ? ".br" : ".gz", name);
        }
        return null;
    }
}

/// <summary>
/// MIME-Typ fuer vorkomprimierte Dateien: gefragt wird die Originaldatei.
/// Ohne das antwortet StaticFileMiddleware auf ".br" mit 404.
/// </summary>
public sealed class InhaltstypAnbieter : IContentTypeProvider
{
    private readonly FileExtensionContentTypeProvider _basis = new();

    public bool TryGetContentType(string unterpfad, out string inhaltstyp)
    {
        var ohneKodierung = unterpfad.EndsWith(".br", StringComparison.OrdinalIgnoreCase)
            ? unterpfad[..^3]
            : unterpfad.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                ? unterpfad[..^3]
                : unterpfad;

        return _basis.TryGetContentType(ohneKodierung, out inhaltstyp!);
    }
}

public static class PrecompressedStaticFilesExtensions
{
    /// <summary>
    /// Vor UseStaticFiles und vor UseResponseCompression einhaengen. Die
    /// Laufzeitkompression ueberspringt Antworten, die schon ein
    /// Content-Encoding tragen — die bereits komprimierten Bytes werden also
    /// nicht ein zweites Mal gepackt.
    /// </summary>
    public static IApplicationBuilder UsePrecompressedStaticFiles(this IApplicationBuilder app) =>
        app.UseMiddleware<PrecompressedStaticFilesMiddleware>();
}
