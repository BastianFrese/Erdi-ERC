using System;
using System.Text;

namespace <OWNER_HANDLE>_ERC.Helpers
{
    /// <summary>
    /// Leitet aus einem Fahrernamen das 1–2 Zeichen lange Initialen-Monogramm der
    /// Fahrer-Karte ab. Da Fahrer ihre <b>Gamertags</b> eintragen, dürfen Trennzeichen
    /// wie Unterstriche, Bindestriche, Punkte oder Klammern NICHT ins Kürzel fließen –
    /// sie wirken stattdessen als Wortgrenzen (z.&***REMOVED***160;B. "lt_wiener" → "LW",
    /// "_wiener" → "W", "John Doe" → "JD").
    /// </summary>
    public static class DriverInitialsHelper
    {
        private const string Fallback = "?";
        private const int MaxLength = 2;

        /// <summary>
        /// Bildet das Initialen-Kürzel: erstes alphanumerisches Zeichen jedes Tokens
        /// (Tokens werden durch beliebige Nicht-Buchstaben/-Ziffern getrennt), maximal
        /// zwei Zeichen, in Großbuchstaben. Liefert "?" wenn kein verwertbares Zeichen
        /// vorhanden ist.
        /// </summary>
        public static string FromGamertag(string? gamertag)
        {
            if (string.IsNullOrWhiteSpace(gamertag)) return Fallback;

            var initials = new StringBuilder(MaxLength);
            var atTokenStart = true;

            foreach (var c in gamertag)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (atTokenStart)
                    {
                        initials.Append(char.ToUpperInvariant(c));
                        if (initials.Length == MaxLength) break;
                        atTokenStart = false;
                    }
                }
                else
                {
                    atTokenStart = true;
                }
            }

            return initials.Length > 0 ? initials.ToString() : Fallback;
        }
    }
}
