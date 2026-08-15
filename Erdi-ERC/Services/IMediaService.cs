using Erdi_ERC.Models;

namespace Erdi_ERC.Services
{
    public interface IMediaService
    {
        // Background Music
        Task<List<BackgroundMusicFileViewModel>> GetBackgroundMusicFilesAsync();
        Task<bool> UploadBackgroundMusicAsync(IFormFile? musicFile);
        Task<bool> DeleteBackgroundMusicAsync(string fileName);

        // About Me Images
        Task<string?> SaveAboutImageAsync(IFormFile image, string slot);
        void TryDeleteAboutImage(string fileName);

        // Driver Profile Photos (vom Profil-Besitzer selbst hochgeladen)
        Task<string?> SaveDriverPhotoAsync(IFormFile image, string discordId);
        void TryDeleteDriverPhoto(string url);

        // Event Images
        Task<string?> SaveEventImageAsync(IFormFile image);
        void TryDeleteEventImage(string fileName);

        // Race Calendar Background
        Task<string?> SaveCalendarBackgroundAsync(IFormFile image);
        void TryDeleteCalendarBackground(string fileName);

        // Ewige Liste
        Task<bool> UploadEwigeListeAsync(IFormFile? workbook);
    }
}
