using Erdi_ERC.Helpers;
using Xunit;

namespace Erdi_ERC.Tests;

/// <summary>
/// Kopfdaten-Auswertung von <see cref="ImageDimensions"/>. Es werden nur die Kopf-Bytes
/// nachgebaut — es braucht keine echten Bilddateien, um die Parser-Pfade zu prüfen.
/// </summary>
public class ImageDimensionsTests
{
    [Fact]
    public void Liest_PNG_Maße()
    {
        var b = new byte[32];
        b[0] = 0x89; b[1] = 0x50; b[2] = 0x4E; b[3] = 0x47; // ‰PNG
        b[12] = (byte)'I'; b[13] = (byte)'H'; b[14] = (byte)'D'; b[15] = (byte)'R';
        b[16] = 0; b[17] = 0; b[18] = 0; b[19] = 2;   // Breite 2 (Big-Endian)
        b[20] = 0; b[21] = 0; b[22] = 0; b[23] = 3;   // Höhe 3

        var size = ImageDimensions.TryParse(b, b.Length);

        Assert.Equal((2, 3), (size!.Value.Width, size.Value.Height));
    }

    [Fact]
    public void Liest_GIF_Maße()
    {
        var b = new byte[16];
        b[0] = (byte)'G'; b[1] = (byte)'I'; b[2] = (byte)'F'; b[3] = (byte)'8'; b[4] = (byte)'9'; b[5] = (byte)'a';
        b[6] = 10; b[7] = 0;   // Breite 10 (Little-Endian)
        b[8] = 20; b[9] = 0;   // Höhe 20

        var size = ImageDimensions.TryParse(b, b.Length);

        Assert.Equal((10, 20), (size!.Value.Width, size.Value.Height));
    }

    [Fact]
    public void Liest_JPEG_Maße_aus_SOF0()
    {
        var b = new byte[16];
        b[0] = 0xFF; b[1] = 0xD8;            // SOI
        b[2] = 0xFF; b[3] = 0xC0;            // SOF0
        b[4] = 0x00; b[5] = 0x11;            // Segmentlänge
        b[6] = 0x08;                         // Präzision
        b[7] = 0x00; b[8] = 0x05;            // Höhe 5 (Big-Endian)
        b[9] = 0x00; b[10] = 0x04;           // Breite 4

        var size = ImageDimensions.TryParse(b, b.Length);

        Assert.Equal((4, 5), (size!.Value.Width, size.Value.Height));
    }

    [Fact]
    public void Liest_WebP_VP8_Maße()
    {
        var b = new byte[30];
        b[0] = (byte)'R'; b[1] = (byte)'I'; b[2] = (byte)'F'; b[3] = (byte)'F';
        b[8] = (byte)'W'; b[9] = (byte)'E'; b[10] = (byte)'B'; b[11] = (byte)'P';
        b[12] = (byte)'V'; b[13] = (byte)'P'; b[14] = (byte)'8'; b[15] = (byte)' ';
        b[23] = 0x9D; b[24] = 0x01; b[25] = 0x2A;   // Startcode
        b[26] = 10; b[27] = 0;                      // Breite 10 (Little-Endian)
        b[28] = 20; b[29] = 0;                      // Höhe 20

        var size = ImageDimensions.TryParse(b, b.Length);

        Assert.Equal((10, 20), (size!.Value.Width, size.Value.Height));
    }

    [Fact]
    public void Unbekannter_Kopf_ergibt_null()
    {
        var b = new byte[64];
        for (var i = 0; i < b.Length; i++) b[i] = 0x42;

        Assert.Null(ImageDimensions.TryParse(b, b.Length));
    }

    [Fact]
    public void Zu_kurzer_Puffer_ergibt_null()
    {
        Assert.Null(ImageDimensions.TryParse(new byte[8], 8));
    }

    [Fact]
    public void Nicht_Uploads_Pfad_wird_ignoriert()
    {
        Assert.Null(ImageDimensions.TryReadFromUpload("wwwroot", "/images/logo.png"));
        Assert.Null(ImageDimensions.TryReadFromUpload("wwwroot", "https://cdn.example.com/a.png"));
    }
}
