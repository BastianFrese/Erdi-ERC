namespace Erdi_ERC.Services.ImageProcessing
{
    /// <summary>
    /// Configuration for <see cref="ImageVariantGenerator"/>.
    /// Bound from appsettings.json section <c>ImageProcessing</c>.
    /// </summary>
    public sealed class ImageProcessingOptions
    {
        public const string SectionName = "ImageProcessing";

        /// <summary>
        /// Target widths (px) for the resized variants. The aspect ratio is preserved.
        /// The longest edge is scaled to the target width; the shorter edge is derived.
        /// </summary>
        public int[] TargetWidths { get; set; } = new[] { 640, 1280, 1920, 3840 };

        /// <summary>WebP quality (0–100). 75 is the F1-sweet-spot for photographic content.</summary>
        public int WebpQuality { get; set; } = 75;

        /// <summary>JPEG quality (0–100) for the master copy.</summary>
        public int JpegQuality { get; set; } = 85;

        /// <summary>Hard cap per variant. Variants exceeding this are skipped (caller logs).</summary>
        public long MaxVariantBytes { get; set; } = 4L * 1024 * 1024; // 4 MB
    }
}
