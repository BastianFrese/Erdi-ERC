using Erdi_ERC.Models;
using Erdi_ERC.Services;

namespace Erdi_ERC.Services
{
    public class MediaService : IMediaService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IAdminAuditService _audit;
        private readonly ILogger<MediaService> _log;

        public MediaService(
            IWebHostEnvironment env,
            IAdminAuditService audit,
            ILogger<MediaService> log)
        {
            _env = env;
            _audit = audit;
            _log = log;
        }

        // ---- Background Music ----
        public Task<List<BackgroundMusicFileViewModel>> GetBackgroundMusicFilesAsync()
        {
            var musicDir = Path.Combine(_env.ContentRootPath, "backgroundmusic");
            Directory.CreateDirectory(musicDir);

            var supportedExtensions = new[] { ".mp3", ".wav", ".ogg" };
            var files = Directory.GetFiles(musicDir)
                .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Select(f => new BackgroundMusicFileViewModel
                {
                    Name = Path.GetFileName(f),
                    Path = f,
                    Size = new FileInfo(f).Length,
                    UploadedAt = System.IO.File.GetCreationTime(f)
                })
                .OrderBy(f => f.Name)
                .ToList();

            return Task.FromResult(files);
        }

        public async Task<bool> UploadBackgroundMusicAsync(IFormFile? musicFile)
        {
            if (musicFile is null || musicFile.Length == 0)
                return false;

            var ext = Path.GetExtension(musicFile.FileName).ToLowerInvariant();
            if (ext != ".mp3" && ext != ".wav" && ext != ".ogg")
                return false;

            if (musicFile.Length > 50 * 1024 * 1024)
                return false;

            var musicDir = Path.Combine(_env.ContentRootPath, "backgroundmusic");
            Directory.CreateDirectory(musicDir);

            var safeFileName = Path.GetFileName(musicFile.FileName);
            var targetPath = Path.Combine(musicDir, safeFileName);

            if (System.IO.File.Exists(targetPath))
                return false;

            await using (var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await musicFile.CopyToAsync(fs);
            }

            await _audit.LogAsync("UploadBackgroundMusic", "MusicFile", safeFileName, $"File={musicFile.FileName}, Size={musicFile.Length}");
            return true;
        }

        public async Task<bool> DeleteBackgroundMusicAsync(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            // Path-Traversal verhindern: nur einfache Dateinamen erlaubt (kein Verzeichnisanteil)
            var safeFileName = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName != fileName)
                return false;

            var musicDir = Path.Combine(_env.ContentRootPath, "backgroundmusic");
            var targetPath = Path.GetFullPath(Path.Combine(musicDir, safeFileName));

            // Sicherstellen, dass der Zielpfad tatsächlich im Music-Verzeichnis liegt
            if (!targetPath.StartsWith(Path.GetFullPath(musicDir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!System.IO.File.Exists(targetPath))
                return false;

            System.IO.File.Delete(targetPath);
            await _audit.LogAsync("DeleteBackgroundMusic", "MusicFile", safeFileName, $"File={safeFileName}");
            return true;
        }

        // ---- Event Images ----
        private static readonly HashSet<string> _allowedImageExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".avif" };

        private const long MaxImageSizeBytes = 10 * 1024 * 1024; // 10 MB

        public async Task<string?> SaveEventImageAsync(IFormFile image)
        {
            if (image is null || image.Length <= 0)
                return null;

            if (image.Length > MaxImageSizeBytes)
                return null;

            var ext = Path.GetExtension(image.FileName)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(ext) || !_allowedImageExtensions.Contains(ext))
                return null;

            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "events");
            Directory.CreateDirectory(uploadDir);

            // Dateiname immer als GUID erzeugen – nie den Originalname übernehmen
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadDir, fileName);

            await using var fs = new FileStream(fullPath, FileMode.CreateNew);
            await image.CopyToAsync(fs);
            return fileName;
        }

        public void TryDeleteEventImage(string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                    return;

                // Path-Traversal verhindern – nur einfache Dateinamen erlauben (keine Verzeichnisanteile).
                var safe = Path.GetFileName(fileName);
                if (safe != fileName)
                    return;

                var full = Path.Combine(_env.WebRootPath, "uploads", "events", safe);
                if (System.IO.File.Exists(full))
                    System.IO.File.Delete(full);
            }
            catch { /* best effort */ }
        }

        // ---- Race Calendar Background ----
        public async Task<string?> SaveCalendarBackgroundAsync(IFormFile image)
        {
            if (image is null || image.Length <= 0)
                return null;
            if (image.Length > MaxImageSizeBytes)
                return null;

            var ext = Path.GetExtension(image.FileName)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(ext) || !_allowedImageExtensions.Contains(ext))
                return null;

            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "calendar");
            Directory.CreateDirectory(uploadDir);

            var fileName = $"bg-{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadDir, fileName);

            await using var fs = new FileStream(fullPath, FileMode.CreateNew);
            await image.CopyToAsync(fs);
            return fileName;
        }

        public void TryDeleteCalendarBackground(string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                    return;
                var safe = Path.GetFileName(fileName);
                if (safe != fileName) return;

                var full = Path.Combine(_env.WebRootPath, "uploads", "calendar", safe);
                if (System.IO.File.Exists(full))
                    System.IO.File.Delete(full);
            }
            catch { /* best effort */ }
        }

        // ---- About Me Images ----
        public async Task<string?> SaveAboutImageAsync(IFormFile image, string slot)
        {
            if (image is null || image.Length <= 0)
                return null;

            if (image.Length > MaxImageSizeBytes)
                return null;

            var ext = Path.GetExtension(image.FileName)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(ext) || !_allowedImageExtensions.Contains(ext))
                return null;

            var safeSlot = string.Concat((slot ?? "img").Where(char.IsLetterOrDigit));
            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "about");
            Directory.CreateDirectory(uploadDir);

            var fileName = $"{safeSlot}-{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadDir, fileName);

            await using var fs = new FileStream(fullPath, FileMode.CreateNew);
            await image.CopyToAsync(fs);

            return $"/uploads/about/{fileName}";
        }

        public void TryDeleteAboutImage(string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName) || !fileName.StartsWith("/uploads/about/"))
                    return;
                var full = Path.Combine(_env.WebRootPath, fileName.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(full))
                    System.IO.File.Delete(full);
            }
            catch { /* best effort */ }
        }

        // ---- Driver Profile Photos ----
        public async Task<string?> SaveDriverPhotoAsync(IFormFile image, string discordId)
        {
            if (image is null || image.Length <= 0)
                return null;

            if (image.Length > MaxImageSizeBytes)
                return null;

            var ext = Path.GetExtension(image.FileName)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(ext) || !_allowedImageExtensions.Contains(ext))
                return null;

            // Discord-Id nur als Datei-Präfix; alles Nicht-Alphanumerische raus (kein Path-Traversal).
            var safeId = string.Concat((discordId ?? "driver").Where(char.IsLetterOrDigit));
            if (string.IsNullOrWhiteSpace(safeId)) safeId = "driver";

            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "drivers");
            Directory.CreateDirectory(uploadDir);

            // Dateiname immer als GUID erzeugen – nie den Originalname übernehmen.
            var fileName = $"{safeId}-{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadDir, fileName);

            await using var fs = new FileStream(fullPath, FileMode.CreateNew);
            await image.CopyToAsync(fs);

            await _audit.LogAsync("UploadDriverPhoto", "DriverProfile", discordId ?? safeId, $"File=/uploads/drivers/{fileName}, Size={image.Length}");
            return $"/uploads/drivers/{fileName}";
        }

        public void TryDeleteDriverPhoto(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/uploads/drivers/"))
                    return;
                var full = Path.Combine(_env.WebRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(full))
                    System.IO.File.Delete(full);
            }
            catch { /* best effort */ }
        }

        // ---- Ewige Liste ----
        public async Task<bool> UploadEwigeListeAsync(IFormFile? workbook)
        {
            if (workbook is null || workbook.Length == 0)
                return false;

            var ext = Path.GetExtension(workbook.FileName).ToLowerInvariant();
            if (ext != ".xlsx")
                return false;

            if (workbook.Length > 25 * 1024 * 1024)
                return false;

            var targetDir = Path.Combine(_env.ContentRootPath, "data", "ewige");
            Directory.CreateDirectory(targetDir);

            var tempPath = Path.Combine(targetDir, "active.tmp");
            var targetPath = Path.Combine(targetDir, "active.xlsx");

            await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await workbook.CopyToAsync(fs);
            }

            if (System.IO.File.Exists(targetPath))
                System.IO.File.Delete(targetPath);

            System.IO.File.Move(tempPath, targetPath);
            await _audit.LogAsync("UploadEwigeListe", "Workbook", "active.xlsx", $"File={workbook.FileName}, Size={workbook.Length}");
            return true;
        }
    }
}
