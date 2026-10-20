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

    /// <summary>
    /// Twitch-Benutzername des Chat-Bots. Leer = anonymer Lesezugriff ohne
    /// Bestätigungen im Chat.
    /// </summary>
    public string BotUsername { get; set; } = string.Empty;

    /// <summary>
    /// OAuth-Token des Bots mit <c>chat:read</c> und <c>chat:edit</c>.
    /// In Production über <c>Dotd__BotOAuthToken</c> setzen, nie committen.
    /// </summary>
    public string BotOAuthToken { get; set; } = string.Empty;

    public bool CanWrite =>
        !string.IsNullOrWhiteSpace(BotUsername)
        && !string.IsNullOrWhiteSpace(BotOAuthToken);
}
