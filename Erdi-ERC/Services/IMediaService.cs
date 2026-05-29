using <OWNER_HANDLE>_ERC.Models;

namespace <OWNER_HANDLE>_ERC.Services
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
