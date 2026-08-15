namespace Erdi_ERC.Options
{
    /// <summary>
    /// Datei-Logging-Parameter (Rolling File).
    /// Sektion: <c>Logging:File</c>
    /// </summary>
    public sealed class SerilogFileOptions
    {
        public const string SectionName = "Logging:File";

        public string Directory { get; set; } = "logs";
        public string FileNamePrefix { get; set; } = "erdi-erc-";
        public int RetainedFileCount { get; set; } = 14;
        public int FileSizeLimitMb { get; set; } = 50;
    }
}
