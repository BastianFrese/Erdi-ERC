namespace <OWNER_HANDLE>_ERC.Models
{
    public class BackgroundMusicFileViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
