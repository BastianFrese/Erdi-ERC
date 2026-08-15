using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

namespace Erdi_ERC.Services.ImageProcessing
{
    /// <summary>
    /// Generates resized WebP variants of an uploaded image. The original file is kept
    /// untouched by the caller; this generator only writes the variants.
    /// AVIF is intentionally NOT produced here — ImageSharp has no native AVIF encoder
    /// (as of 4.0.0). The <c>&lt;picture&gt;</c> markup still emits AVIF <c>&lt;source&gt;</c>
    /// entries; the browser will receive 404 and fall back to the WebP variant, which is
    /// still a 60–80 % size reduction over the original JPG.
    /// </summary>
    public sealed class ImageVariantGenerator
    {
        private readonly ImageProcessingOptions _opts;
        private readonly ILogger<ImageVariantGenerator> _log;

        public ImageVariantGenerator(IOptions<ImageProcessingOptions> opts, ILogger<ImageVariantGenerator> log)
        {
            _opts = opts.Value;
            _log = log;
        }

        /// <summary>
        /// Generates a WebP variant for every configured target width.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the already-saved original image.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of (absolutePath, width) for the generated variants.</returns>
        public async Task<IReadOnlyList<(string Path, int Width)>> GenerateWebpVariantsAsync(
            string sourcePath, CancellationToken ct = default)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Source image for variant generation not found.", sourcePath);

            var dir = Path.GetDirectoryName(sourcePath)!;
            var stem = Path.GetFileNameWithoutExtension(sourcePath);
            var produced = new List<(string, int)>();
            var encoder = new WebpEncoder { Quality = _opts.WebpQuality };

            // Load once, dispose before any file IO. Synchronous load is required by ImageSharp.
            using var image = await Image.LoadAsync(sourcePath, ct);
            // Strip EXIF so we don't leak camera/geo. Also normalize orientation.
            image.Metadata.ExifProfile = null;
            image.Mutate(x => x.AutoOrient());

            foreach (var width in _opts.TargetWidths)
            {
                if (width <= 0 || width > image.Width * 2) continue; // skip upscales

                var variantPath = Path.Combine(dir, $"{stem}-{width}.webp");
                try
                {
                    using var clone = image.Clone(ctx => ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(width, 0),
                        Mode = ResizeMode.Max
                    }));
                    await clone.SaveAsync(variantPath, encoder, ct);

                    var size = new FileInfo(variantPath).Length;
                    if (size > _opts.MaxVariantBytes)
                    {
                        _log.LogWarning("Variant {Path} exceeds cap ({Bytes} > {Max} bytes), deleting.",
                            variantPath, size, _opts.MaxVariantBytes);
                        File.Delete(variantPath);
                        continue;
                    }
                    produced.Add((variantPath, width));
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to generate WebP variant at width {Width} for {Source}", width, sourcePath);
                }
            }
            return produced;
        }
    }
}
