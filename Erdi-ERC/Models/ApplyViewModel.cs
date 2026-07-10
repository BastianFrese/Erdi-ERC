using System.ComponentModel.DataAnnotations;

namespace <OWNER_HANDLE>_ERC.Models
{
    /// <summary>
    /// Formular-Modell der öffentlichen Bewerbung. Bewusst von der Entity
    /// <see cref="ApplicationForm"/> getrennt: hier stehen NUR Felder, die der
    /// Bewerber selbst setzen darf — Workflow- und Status-Felder existieren im
    /// Binding gar nicht und sind damit strukturell vor Overposting geschützt.
    /// </summary>
    public class ApplyViewModel
    {
        [Required(ErrorMessage = "Bitte gib dein Alter an.")]
        [Range(13, 99, ErrorMessage = "Bitte ein gültiges Alter zwischen 13 und 99 angeben.")]
        public int? Age { get; set; }

        /// <summary>Stammfahrer oder Ersatzfahrer.</summary>
        [Required(ErrorMessage = "Bitte wähle eine Rolle.")]
        [MaxLength(64)]
        public string Role { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte wähle deine Plattform.")]
        [MaxLength(64)]
        public string Platform { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte gib deinen Ingame-Namen an.")]
        [MaxLength(128)]
        public string GamingName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte gib deine KI-Stufe an.")]
        [MaxLength(64)]
        public string AiLevel { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte wähle eine Liga aus.")]
        [MaxLength(64)]
        public string? AppliedLeagueId { get; set; }

        // ── Optionale Angaben (fließen in Fahrer-Karte und Liga-Eintrag) ────────

        [MaxLength(64)]
        public string? SimHardware { get; set; }

        [Range(0, 999, ErrorMessage = "Fahrernummer muss zwischen 0 und 999 liegen.")]
        public int? PreferredNumber { get; set; }

        [MaxLength(64)]
        public string? PreferredTeam { get; set; }

        [MaxLength(256)]
        public string? PaceReference { get; set; }

        // ── Reine Anzeige-Felder — der Server überschreibt sie immer aus dem
        //    Discord-Login bzw. der Guild-Verifikation, egal was gepostet wird. ──

        public string DiscordName { get; set; } = string.Empty;
        public bool JoinedCommunityDiscord { get; set; }
        public bool JoinedLeagueDiscord { get; set; }
    }
}
