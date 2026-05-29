namespace <OWNER_HANDLE>_ERC.Options
{
    /// <summary>
    /// Konfiguration für den Hintergrundmusik-Ordner und Upload-Limits.
    /// Sektion: <c>BackgroundMusic</c>
    /// </summary>
    public sealed class BackgroundMusicOptions
    {
        public const string SectionName = "BackgroundMusic";

        /// <summary>Ordner relativ zum ContentRoot, in dem die Songs liegen.</summary>
        public string Directory { get; set; } = "backgroundmusic";

        /// <summary>Erlaubte Datei-Endungen (immer mit führendem Punkt, z. B. ".mp3").</summary>
        public string[] AllowedExtensions { get; set; } = { ".mp3", ".wav", ".ogg" };

        /// <summary>Maximale Dateigröße pro Upload in Megabyte.</summary>
        public int MaxUploadSizeMb { get; set; } = 50;

        public long MaxUploadSizeBytes => (long)MaxUploadSizeMb * 1024 * 1024;
    }
}
