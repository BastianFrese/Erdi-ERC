namespace Erdi_ERC.Options;

/// <summary>
/// Einstellungen für das Driver-of-the-Day-Chat-Voting. Sektion: <c>Dotd</c>.
/// </summary>
public sealed class DotdOptions
{
    public const string SectionName = "Dotd";

    /// <summary>
    /// Der Chat-Leser verbindet sich nur in Production. Für einen lokalen
    /// End-to-End-Test gegen den echten Twitch-Chat auf true setzen.
    /// </summary>
    public bool ConnectOutsideProduction { get; set; }
}
