using System.Collections.Concurrent;

namespace Erdi_ERC.Helpers;

/// <summary>
/// Liest die Pixelmaße eines Bildes direkt aus dem Dateikopf (JPEG/PNG/GIF/WebP).
/// Bewusst ohne Bildbibliothek — ImageSharp wurde aus der Web-App entfernt, damit
/// beim Upload kein CPU-Job läuft; hier wird nur der Kopf gelesen, nicht dekodiert.
/// Ergebnis wird prozessweit gecacht: pro Datei höchstens ein Dateizugriff.
///
/// Zweck: das _Picture-Partial setzt width/height am &lt;img&gt;, damit der Browser den
/// Platz vor dem Laden reservieren kann (kein Layout-Sprung / CLS).
/// </summary>
public static class ImageDimensions
{
    public readonly record struct Size(int Width, int Height);

    private static readonly ConcurrentDictionary<string, Size?> Cache = new();

    /// <summary>Maße für eine /uploads/...-URL; null, wenn nicht lesbar oder kein Uploads-Pfad.</summary>
    public static Size? TryReadFromUpload(string webRootPath, string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (url.StartsWith("//", StringComparison.Ordinal) || url.Contains("://", StringComparison.Ordinal))
            return null; // extern/absolut — liegt nicht im wwwroot

        var path = url.StartsWith("~/", StringComparison.Ordinal) ? url[2..] : url;
        if (!path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return null;

        var full = Path.Combine(webRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        return Cache.GetOrAdd(full, Read);
    }

    private static Size? Read(string fullPath)
    {
        try
        {
            if (!File.Exists(fullPath)) return null;

            // Die Kopf-Daten passen für alle unterstützten Formate deutlich in 64 KiB.
            var buffer = new byte[64 * 1024];
            using var fs = File.OpenRead(fullPath);
            var read = fs.Read(buffer, 0, buffer.Length);
            return TryParse(buffer, read);
        }
        catch
        {
            return null; // best effort — ein unlesbares Bild darf die Seite nicht kippen
        }
    }

    /// <summary>Reine Kopf-Auswertung (ohne IO) — testbar ohne Dateien.</summary>
    public static Size? TryParse(byte[] b, int len)
    {
        if (len < 16) return null;

        // PNG: Signatur + IHDR, Breite/Höhe als Big-Endian-UInt32
        if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 &&
            b[12] == 'I' && b[13] == 'H' && b[14] == 'D' && b[15] == 'R')
        {
            var w = (int)BE32(b, 16);
            var h = (int)BE32(b, 20);
            return Sanity(w, h);
        }

        // GIF: "GIF87a"/"GIF89a", Breite/Höhe als Little-Endian-UInt16
        if (b[0] == 'G' && b[1] == 'I' && b[2] == 'F' && b[3] == '8')
        {
            return Sanity(b[6] | (b[7] << 8), b[8] | (b[9] << 8));
        }

        // JPEG: SOF-Segment suchen (Marker 0xC0–0xCF außer 0xC4/0xC8/0xCC)
        if (b[0] == 0xFF && b[1] == 0xD8)
        {
            var i = 2;
            while (i + 9 < len)
            {
                if (b[i] != 0xFF) { i++; continue; }
                var marker = b[i + 1];
                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    i += 2; // markerlose Segmente
                    continue;
                }
                var segLen = (b[i + 2] << 8) | b[i + 3];
                if (segLen < 2) break;

                var isSof = marker >= 0xC0 && marker <= 0xCF &&
                            marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                if (isSof)
                {
                    return Sanity((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
                }
                i += 2 + segLen;
            }
            return null;
        }

        // WebP: RIFF....WEBP + VP8 (lossy) / VP8L (lossless) / VP8X (erweitert)
        if (b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' &&
            b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
        {
            // Chunk-Kopf (fourcc + size) beginnt bei 12, die Nutzdaten bei 20
            if (b[12] == 'V' && b[13] == 'P' && b[14] == '8' && b[15] == ' ')
            {
                // 3 Bytes Frame-Tag, 3 Bytes Startcode (9D 01 2A), dann je 2 Bytes LE
                if (b[23] != 0x9D || b[24] != 0x01 || b[25] != 0x2A) return null;
                var w = (b[26] | (b[27] << 8)) & 0x3FFF;
                var h = (b[28] | (b[29] << 8)) & 0x3FFF;
                return Sanity(w, h);
            }
            if (b[12] == 'V' && b[13] == 'P' && b[14] == '8' && b[15] == 'L')
            {
                if (b[20] != 0x2F) return null;
                var v = (uint)(b[21] | (b[22] << 8) | (b[23] << 16) | (b[24] << 24));
                var w = (int)(v & 0x3FFF) + 1;
                var h = (int)((v >> 14) & 0x3FFF) + 1;
                return Sanity(w, h);
            }
            if (b[12] == 'V' && b[13] == 'P' && b[14] == '8' && b[15] == 'X')
            {
                var w = (int)((uint)(b[24] | (b[25] << 8) | (b[26] << 16))) + 1;
                var h = (int)((uint)(b[27] | (b[28] << 8) | (b[29] << 16))) + 1;
                return Sanity(w, h);
            }
        }

        return null;
    }

    private static long BE32(byte[] b, int i) =>
        ((long)b[i] << 24) | ((long)b[i + 1] << 16) | ((long)b[i + 2] << 8) | b[i + 3];

    private static Size? Sanity(int w, int h) =>
        w > 0 && h > 0 && w <= 100_000 && h <= 100_000 ? new Size(w, h) : null;
}
