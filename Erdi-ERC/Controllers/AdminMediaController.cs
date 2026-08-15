using Erdi_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erdi_ERC.Controllers
{
    [Authorize(Policy = "Admin.System.Music")]
    public class AdminMediaController : Controller
    {
        private readonly IMediaService _mediaService;

        public AdminMediaController(IMediaService mediaService)
        {
            _mediaService = mediaService;
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
