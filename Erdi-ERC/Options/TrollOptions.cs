namespace Erdi_ERC.Options
{
    /// <summary>
    /// Konfiguration für das Erdi-Troll-System: kleine Login-Pranks, die nach
    /// erfolgreichem Discord-Login mit geringer Wahrscheinlichkeit auslösen.
    /// Sektion: <c>Troll</c>
    /// </summary>
    public sealed class TrollOptions
    {
        public const string SectionName = "Troll";

        /// <summary>Master-Schalter. False = Login verhält sich exakt wie ohne Troll-System.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Wahrscheinlichkeit (0..1), dass pro Login überhaupt ein Gag ausgelöst wird.</summary>
        public double TriggerChance { get; set; } = 0.25;

        /// <summary>Pro User: nach einem Troll wird für diese Dauer kein weiterer ausgelöst.</summary>
        public int CooldownMinutes { get; set; } = 360;

        /// <summary>Auch Admins trollen? Default true – dank Gnaden-Ausweg wird niemand ausgesperrt.</summary>
        public bool ApplyToAdmins { get; set; } = true;

        /// <summary>
        /// Anzahl Fehlversuche bei Pflicht-Gags (Mathe/Quiz), nach der „Erdi gnädig wird"
        /// und einen Durchlass-Button anbietet. Verhindert, dass sich jemand aussperrt.
        /// </summary>
        public int MercyAfterAttempts { get; set; } = 3;

        /// <summary>Größter Operand der Mathe-Aufgabe. 9 = einstellige Additionsaufgaben (a + b, 1..9).</summary>
        public int MathMaxOperand { get; set; } = 9;

        /// <summary>
        /// Optionale Gewichts-Overrides je Gag-Key (z. B. <c>{"math": 30, "dice": 0}</c>).
        /// Gewicht 0 deaktiviert einen Gag. Nicht gesetzte Keys nutzen das Registry-Default-Gewicht.
        /// </summary>
        public Dictionary<string, int> Weights { get; set; } = new();
    }
}
