using Erdi_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Memory;

namespace Erdi_ERC.Controllers
{
    [Authorize(Policy = "Admin.System.Music")]
    public class AdminMediaController : Controller
    {
        private const string BgMusicCacheKey = "bg-music-tracks:v1";

        private readonly IMediaService _mediaService;
        private readonly IMemoryCache _cache;
        private readonly IOutputCacheStore _outputCache;

        public AdminMediaController(
            IMediaService mediaService,
            IMemoryCache cache,
            IOutputCacheStore outputCache)
        {
            _mediaService = mediaService;
            _cache = cache;
            _outputCache = outputCache;
        }

        private void InvalidateBackgroundMusicCaches()
        {
            _cache.Remove(BgMusicCacheKey);
            // "public" tag is set on every OutputCache policy in Program.cs (public-2min, public-30s).
            // Evicting that tag drops every cached read page so the freshly uploaded track shows
            // up immediately on /, /Races/AllRaces, /Stats/Erdi10, etc.
            _outputCache.EvictByTagAsync("public", default).GetAwaiter().GetResult();
        }

        [HttpGet]
        public async Task<IActionResult> BackgroundMusic()
        {
            var files = await _mediaService.GetBackgroundMusicFilesAsync();
            return View("~/Views/Admin/BackgroundMusic.cshtml", files);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadBackgroundMusic(IFormFile? musicFile)
        {
            if (musicFile is null || musicFile.Length == 0)
            {
                TempData["AdminMessage"] = "Bitte eine Musikdatei auswählen.";
                return RedirectToAction(nameof(BackgroundMusic));
            }

            var ext = Path.GetExtension(musicFile.FileName).ToLowerInvariant();
            if (ext != ".mp3" && ext != ".wav" && ext != ".ogg")
            {
                TempData["AdminMessage"] = "Nur MP3, WAV oder OGG Dateien sind erlaubt.";
                return RedirectToAction(nameof(BackgroundMusic));
            }

            if (musicFile.Length > 50 * 1024 * 1024)
            {
                TempData["AdminMessage"] = "Datei ist zu groß (max. 50 MB).";
                return RedirectToAction(nameof(BackgroundMusic));
            }

            var success = await _mediaService.UploadBackgroundMusicAsync(musicFile);
            if (success)
            {
                InvalidateBackgroundMusicCaches();
                TempData["AdminMessage"] = $"Song \"{Path.GetFileName(musicFile.FileName)}\" erfolgreich hochgeladen.";
            }
            else
            {
                TempData["AdminMessage"] = "Eine Datei mit diesem Namen existiert bereits oder Upload fehlgeschlagen.";
            }

            return RedirectToAction(nameof(BackgroundMusic));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBackgroundMusic(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                TempData["AdminMessage"] = "Dateiname fehlt.";
                return RedirectToAction(nameof(BackgroundMusic));
            }

            var success = await _mediaService.DeleteBackgroundMusicAsync(fileName);
            if (success)
            {
                InvalidateBackgroundMusicCaches();
                TempData["AdminMessage"] = $"Song \"{fileName}\" wurde gelöscht.";
            }
            else
            {
                TempData["AdminMessage"] = "Datei existiert nicht oder konnte nicht gelöscht werden.";
            }

            return RedirectToAction(nameof(BackgroundMusic));
        }
    }
}
