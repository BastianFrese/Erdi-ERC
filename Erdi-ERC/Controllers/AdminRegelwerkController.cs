using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Erdi_ERC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erdi_ERC.Controllers
{
    [Authorize(Policy = "Admin.System.Regelwerk")]
    [Route("admin/regelwerk")]
    public class AdminRegelwerkController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _audit;
        private readonly IWebHostEnvironment _env;

        private static readonly string[] AllowedExtensions = [".pdf", ".md", ".txt"];
        private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

        public AdminRegelwerkController(AppDbContext db, IAdminAuditService audit, IWebHostEnvironment env)
        {
            _db = db;
            _audit = audit;
            _env = env;
        }

        // ── Index / Übersicht ────────────────────────────────────────────────────
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var docs = await _db.RegelwerkDocuments
                .OrderByDescending(d => d.IsActive)
                .ThenByDescending(d => d.UploadedAt)
                .ToListAsync();
            return View("~/Views/Admin/Regelwerk/Index.cshtml", docs);
        }

        // ── Upload GET ───────────────────────────────────────────────────────────
        [HttpGet("upload")]
        public IActionResult Upload() => View("~/Views/Admin/Regelwerk/Upload.cshtml", new RegelwerkDocument());

        // ── Upload POST ──────────────────────────────────────────────────────────
        [HttpPost("upload")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(25 * 1024 * 1024)]
        public async Task<IActionResult> Upload(RegelwerkDocument model, IFormFile? file, bool makeActive)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("", "Bitte eine Datei auswählen.");
                return View("~/Views/Admin/Regelwerk/Upload.cshtml", model);
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                ModelState.AddModelError("", $"Erlaubte Dateitypen: {string.Join(", ", AllowedExtensions)}");
                return View("~/Views/Admin/Regelwerk/Upload.cshtml", model);
            }

            if (file.Length > MaxFileSizeBytes)
            {
                ModelState.AddModelError("", "Datei zu groß (max. 20 MB).");
                return View("~/Views/Admin/Regelwerk/Upload.cshtml", model);
            }

            // Datei speichern
            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "regelwerk");
            Directory.CreateDirectory(uploadDir);
            var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadDir, fileName);

            await using (var stream = System.IO.File.Create(fullPath))
                await file.CopyToAsync(stream);

            // Wenn aktiv gesetzt: alle anderen deaktivieren + archivieren
            if (makeActive)
            {
                var existing = await _db.RegelwerkDocuments.AsTracking().Where(d => d.IsActive).ToListAsync();
                foreach (var d in existing)
                {
                    d.IsActive = false;
                    d.IsArchived = true;
                }
            }

            var doc = new RegelwerkDocument
            {
                Title = model.Title,
                Version = model.Version,
                Description = model.Description,
                FilePath = $"/uploads/regelwerk/{fileName}",
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                IsActive = makeActive,
                IsArchived = false,
                UploadedBy = User.Identity?.Name ?? "Admin",
                UploadedAt = DateTime.UtcNow
            };

            _db.RegelwerkDocuments.Add(doc);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Regelwerk", "RegelwerkDocument", doc.Id.ToString(), $"Upload: '{doc.Title}' v{doc.Version} (aktiv={makeActive})");
            TempData["AdminMessage"] = $"✓ Regelwerk \"{doc.Title}\" wurde erfolgreich hochgeladen.";
            return RedirectToAction(nameof(Index));
        }

        // ── Als aktiv markieren ──────────────────────────────────────────────────
        [HttpPost("set-active/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(int id)
        {
            var doc = await _db.RegelwerkDocuments.FindAsync(id);
            if (doc == null) return NotFound();

            var others = await _db.RegelwerkDocuments.AsTracking().Where(d => d.IsActive && d.Id != id).ToListAsync();
            foreach (var d in others) { d.IsActive = false; d.IsArchived = true; }

            doc.IsActive = true;
            doc.IsArchived = false;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Regelwerk", "RegelwerkDocument", doc.Id.ToString(), $"SetActive: '{doc.Title}' v{doc.Version}");
            TempData["AdminMessage"] = $"✓ \"{doc.Title}\" ist jetzt das aktive Regelwerk.";
            return RedirectToAction(nameof(Index));
        }

        // ── Archivieren ──────────────────────────────────────────────────────────
        [HttpPost("archive/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var doc = await _db.RegelwerkDocuments.FindAsync(id);
            if (doc == null) return NotFound();

            doc.IsActive = false;
            doc.IsArchived = true;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Regelwerk", "RegelwerkDocument", doc.Id.ToString(), $"Archiviert: '{doc.Title}' v{doc.Version}");
            TempData["AdminMessage"] = $"✓ \"{doc.Title}\" archiviert.";
            return RedirectToAction(nameof(Index));
        }

        // ── Löschen ──────────────────────────────────────────────────────────────
        [HttpPost("delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var doc = await _db.RegelwerkDocuments.FindAsync(id);
            if (doc == null) return NotFound();

            // Physische Datei entfernen
            var physPath = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(physPath))
                System.IO.File.Delete(physPath);

            _db.RegelwerkDocuments.Remove(doc);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Regelwerk", "RegelwerkDocument", doc.Id.ToString(), $"Gelöscht: '{doc.Title}' v{doc.Version}");
            TempData["AdminMessage"] = $"✓ \"{doc.Title}\" wurde gelöscht.";
            return RedirectToAction(nameof(Index));
        }
    }
}
