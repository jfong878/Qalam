// Qalam - pronunciation register (Fu\u1e63\u1e25\u0101 vs Lahjaat).
//
// Tashkeel and the spoken reader should not always sound like MSA. Reading a
// Sudanese or Iraqi sentence with case endings is not how anyone says it, so the
// register is a first-class setting rather than a note in the phrasebook.
//
// The offline diacritizer does the acoustic work. tashkeel_server.py applies a
// curated dialect lexicon and drops \u02bei\u02bfr\u0101b when the register is not Fu\u1e63\u1e25\u0101,
// and that vocalized string is what the audio request sends. The language tags
// below exist so a future voice-per-region engine can use them, but the public
// Arabic voice currently returns the same audio for every one of them, so the
// distinction reaches the ear through the vocalized text.

using System;

namespace Qalam
{
    internal static class Pronunciation
    {
        public const string Fusha = "fusha";

        private static readonly string[] AllCodes = new string[]
        {
            Fusha, "egyptian", "levantine", "gulf", "sudanese", "iraqi",
            "yemeni", "hijazi", "moroccan", "tunisian", "algerian", "libyan"
        };

        public static string[] Codes
        {
            get { return AllCodes; }
        }

        // The popup has no Config of its own, and the register is process-wide
        // for this single-user app, so the loaded value lives here too.
        private static string _current = Fusha;

        public static string Current
        {
            get { return _current; }
            set { _current = Normalize(value); }
        }

        // Accepts the spellings people actually type in config.json so a hand
        // edit does not silently fall back to Fu\u1e63\u1e25\u0101.
        public static string Normalize(string code)
        {
            string value = (code ?? "").Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
            if (value.Length == 0) return Fusha;
            foreach (string known in AllCodes)
                if (value == known) return known;
            if (value == "msa" || value == "fushaa" || value == "fus7a" || value == "standard" ||
                value == "fu\u1e63\u1e25\u0101" || value == "fusha") return Fusha;
            if (value == "masri" || value == "egypt") return "egyptian";
            if (value == "shami" || value == "levant") return "levantine";
            if (value == "khaliji" || value == "gulfi") return "gulf";
            if (value == "sudan") return "sudanese";
            if (value == "iraq") return "iraqi";
            if (value == "yemen") return "yemeni";
            if (value == "hejazi") return "hijazi";
            if (value == "maghrebi" || value == "morocco") return "moroccan";
            if (value == "tunisia") return "tunisian";
            if (value == "algeria") return "algerian";
            if (value == "libya") return "libyan";
            return Fusha;
        }

        public static string Name(string code)
        {
            switch (Normalize(code))
            {
                case "egyptian": return "Egyptian";
                case "levantine": return "Levantine";
                case "gulf": return "Gulf";
                case "sudanese": return "Sudanese";
                case "iraqi": return "Iraqi";
                case "yemeni": return "Yemeni";
                case "hijazi": return "Hijazi";
                case "moroccan": return "Moroccan";
                case "tunisian": return "Tunisian";
                case "algerian": return "Algerian";
                case "libyan": return "Libyan";
                default: return "Fu\u1e63\u1e25\u0101";
            }
        }

        // Language tag for the speech endpoint.
        public static string TtsLanguage(string code)
        {
            switch (Normalize(code))
            {
                case "egyptian": return "ar-EG";
                case "levantine": return "ar-LB";
                case "gulf": return "ar-AE";
                case "sudanese": return "ar-SD";
                case "iraqi": return "ar-IQ";
                case "yemeni": return "ar-YE";
                case "hijazi": return "ar-SA";
                case "moroccan": return "ar-MA";
                case "tunisian": return "ar-TN";
                case "algerian": return "ar-DZ";
                case "libyan": return "ar-LY";
                default: return "ar";
            }
        }

        public static bool IsArabic(string language)
        {
            string value = (language ?? "").Trim().ToLowerInvariant();
            return value == "ar" || value.StartsWith("ar-", StringComparison.Ordinal);
        }

        // Fu\u1e63\u1e25\u0101 keeps Mishkal's case endings; every lahja gets the dialect pass.
        public static bool NeedsDialectVocalization(string code)
        {
            return Normalize(code) != Fusha;
        }
    }
}
