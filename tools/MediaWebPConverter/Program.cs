// =====================================================================
// MediaWebPConverter — Offline-Werkzeug (KEIN Teil der Web-App)
//
// Erzeugt für jedes Bild in einem Uploads-Ordner zwei WebP-Zweitdateien
// ({stem}.webp für 1x und {stem}@2x.webp für HiDPI) direkt daneben. Die
// DB-Referenzen auf die Original-URLs bleiben unangetastet; das _Picture-Partial
// liefert die Varianten aus, sobald sie existieren (Fallback: Original).
//
// Idempotent: existiert die .webp bereits und ist NICHT älter als das
// Original, wird sie übersprungen. Dadurch jederzeit re-runnable
// ("nur neue Dateien konvertieren").
//
// Aufruf:  dotnet run --project tools/MediaWebPConverter -- C:\...\wwwroot\uploads
// =====================================================================
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: MediaWebPConverter <uploads-dir>");
    return 1;
}

var root = Path.GetFullPath(args[0]);
if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"Ordner nicht gefunden: {root}");
    return 1;
}

const int MaxEdge = 1200;    // längste Kante der 1x-Variante — reicht für Hero/Lightbox
const int MaxEdge2x = 2400;  // 2x-Variante für HiDPI-Displays ({stem}@2x.webp)
const int Quality = 75;      // WebP-Qualität (lossy) — Foto-tauglich, deutlich kleiner als q82

// Beide Varianten pro Bild. Das _Picture-Partial prüft die Existenz und nimmt
// die @2x-Datei nur, wenn Model.Retina gesetzt ist (sonst reicht die 1x-Quelle).
var variants = new (int Edge, string Suffix)[] { (MaxEdge, ".webp"), (MaxEdge2x, "@2x.webp") };

var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png" };

long before = 0, after = 0;
int converted = 0, skipped = 0, failed = 0;

foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
{
    var ext = Path.GetExtension(file);
    if (!extensions.Contains(ext)) continue;

    var dir = Path.GetDirectoryName(file)!;
    var stem = Path.GetFileNameWithoutExtension(file);
    before += new FileInfo(file).Length;

    foreach (var (edge, suffix) in variants)
    {
        var target = Path.Combine(dir, stem + suffix);
        var label = Path.GetRelativePath(root, target);

        // Idempotenz: nur konvertieren, wenn Variante fehlt ODER älter als das Original
        if (File.Exists(target) &&
            File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(file))
        {
            skipped++;
            after += new FileInfo(target).Length;
            continue;
        }

        try
        {
            using var image = await Image.LoadAsync(file);
            if (image.Width > edge || image.Height > edge)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(edge, edge)
                }));
            }
            await image.SaveAsWebpAsync(target, new SixLabors.ImageSharp.Formats.Webp.WebpEncoder
            {
                Quality = Quality
            });
            after += new FileInfo(target).Length;
            converted++;
            Console.WriteLine($"OK   {label}");
        }
        catch (Exception ex)
        {
            failed++;
            Console.Error.WriteLine($"FEHLER {label}: {ex.Message}");
            if (File.Exists(target)) File.Delete(target); // keine halben Varianten liegen lassen
        }
    }
}

Console.WriteLine();
Console.WriteLine($"Konvertiert: {converted}, übersprungen (aktuell): {skipped}, Fehler: {failed}");
Console.WriteLine($"Originals gesamt: {before / 1024 / 1024.0:F1} MB  →  WebP-Varianten: {after / 1024 / 1024.0:F1} MB");
return failed == 0 ? 0 : 2;