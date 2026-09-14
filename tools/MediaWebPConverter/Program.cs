// =====================================================================
// MediaWebPConverter — Offline-Werkzeug (KEIN Teil der Web-App)
//
// Erzeugt für jedes Bild in einem Uploads-Ordner eine WebP-Zweitdatei
// ({stem}.webp) direkt daneben. Die DB-Referenzen auf die Original-URLs
// bleiben unangetastet; das _Picture-Partial liefert die Variante aus,
// sobald sie existiert (Fallback: Original).
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

const int MaxEdge = 1200;   // längste Kante — reicht für Hero/Lightbox (Zoom bis 4x)
const int Quality = 75;     // WebP-Qualität (lossy) — Foto-tauglich, deutlich kleiner als q82

var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png" };

long before = 0, after = 0;
int converted = 0, skipped = 0, failed = 0;

foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
{
    var ext = Path.GetExtension(file);
    if (!extensions.Contains(ext)) continue;

    var webpPath = Path.Combine(
        Path.GetDirectoryName(file)!,
        Path.GetFileNameWithoutExtension(file) + ".webp");

    before += new FileInfo(file).Length;

    // Idempotenz: nur konvertieren, wenn Variante fehlt ODER älter als das Original
    if (File.Exists(webpPath) &&
        File.GetLastWriteTimeUtc(webpPath) >= File.GetLastWriteTimeUtc(file))
    {
        skipped++;
        after += new FileInfo(webpPath).Length;
        continue;
    }

    try
    {
        using var image = await Image.LoadAsync(file);
        if (image.Width > MaxEdge || image.Height > MaxEdge)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxEdge, MaxEdge)
            }));
        }
        await image.SaveAsWebpAsync(webpPath, new SixLabors.ImageSharp.Formats.Webp.WebpEncoder
        {
            Quality = Quality
        });
        after += new FileInfo(webpPath).Length;
        converted++;
        Console.WriteLine($"OK   {Path.GetRelativePath(root, file)}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FEHLER {Path.GetRelativePath(root, file)}: {ex.Message}");
        if (File.Exists(webpPath)) File.Delete(webpPath); // keine halben Varianten liegen lassen
    }
}

Console.WriteLine();
Console.WriteLine($"Konvertiert: {converted}, übersprungen (aktuell): {skipped}, Fehler: {failed}");
Console.WriteLine($"Originals gesamt: {before / 1024 / 1024.0:F1} MB  →  WebP-Varianten: {after / 1024 / 1024.0:F1} MB");
return failed == 0 ? 0 : 2;