// Audio must speak the language the text is actually in. Every Listen button
// resolves its tag here: script evidence first, then the translator's detected
// code, then local word/letter detection, then English.
using System;

namespace Qalam
{
    internal static class SpeechLanguage
    {
        public static string Normalize(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            string value = code.Trim().Replace('_', '-').ToLowerInvariant();
            if (value == "auto" || value == "und") return null;
            if (value == "iw") value = "he";
            if (value == "in") value = "id";
            if (value == "fil" || value == "tl-ph") value = "tl";
            if (value == "zh" || value == "zh-hans" || value == "zh-cn") value = "zh-CN";
            if (value == "zh-hant" || value == "zh-tw") value = "zh-TW";
            if (LanguageCatalog.Contains(value)) return value;
            int dash = value.IndexOf('-');
            if (dash > 0)
            {
                string root = value.Substring(0, dash);
                if (LanguageCatalog.Contains(root)) return root;
                if (root == "zh") return "zh-CN";
            }
            // Keep a well-formed tag Google can speak even when the catalog
            // does not list it.
            if (value.Length >= 2 && value.Length <= 16) return value;
            return null;
        }

        public static string Resolve(string text, string detected)
        {
            string source = text ?? "";
            string script = Script(source);
            if (script != null) return script;

            string code = Normalize(detected);
            string local = Normalize(Colloquial.DetectLanguage(source));

            // A Latin-script text tagged with a non-Latin language is a bad
            // detection; trust the local reading instead.
            if (!string.IsNullOrEmpty(code) && IsNonLatin(code)) code = null;

            // Latin-to-Latin repairs. French and Spanish share most of their
            // function words, so accents and a local re-read decide ties.
            int fr = FrenchHints(source);
            int es = SpanishHints(source);
            if (code == "es" && local == "fr") code = "fr";
            else if (code == "fr" && local == "es") code = "es";
            else if (code == "es" && fr > 0 && fr >= es) code = "fr";
            else if (code == "fr" && es > 0 && es >= fr) code = "es";
            else if (code == "en" && local != null && local != "en") code = local;
            else if (code == null) code = local;

            if (!string.IsNullOrEmpty(code)) return code;
            if (local != null) return local;
            return "en";
        }

        private static bool IsNonLatin(string code)
        {
            switch (code)
            {
                case "ar": case "fa": case "ur": case "ru": case "uk": case "el":
                case "he": case "hi": case "th": case "ja": case "ko": case "zh-CN":
                case "zh-TW": case "ta": case "te": case "bn": case "mr": case "si":
                case "km": case "lo": case "my": case "ne": case "am": case "ka":
                case "hy":
                    return true;
                default:
                    return false;
            }
        }

        private static int FrenchHints(string text)
        {
            int hints = 0;
            foreach (char c in text.ToLowerInvariant())
                if ("çœæàèùêâîôûëïÿ".IndexOf(c) >= 0) hints++;
            return hints;
        }

        private static int SpanishHints(string text)
        {
            int hints = 0;
            foreach (char c in text.ToLowerInvariant())
                if (c == 'ñ' || c == '¿' || c == '¡') hints++;
            return hints;
        }

        // Scripts force a speech language regardless of what a weak detector
        // guessed. The returned code is already catalog-normalized.
        private static string Script(string text)
        {
            bool arabic = false, cyrillic = false, greek = false, hebrew = false;
            bool devanagari = false, thai = false, han = false, kana = false, hangul = false;
            bool ukrainian = false, persian = false, urdu = false;
            foreach (char c in text)
            {
                if ((c >= 0x0600 && c <= 0x06FF) || (c >= 0x0750 && c <= 0x077F) || (c >= 0x08A0 && c <= 0x08FF))
                {
                    arabic = true;
                    if (c == '\u067E' || c == '\u0686' || c == '\u0698' || c == '\u06AF') persian = true;
                    if (c == '\u0679' || c == '\u0688' || c == '\u0691' || c == '\u06BA' || c == '\u06D2') urdu = true;
                }
                else if (c >= 0x0400 && c <= 0x04FF)
                {
                    cyrillic = true;
                    if (c == '\u0406' || c == '\u0407' || c == '\u0404' || c == '\u0490') ukrainian = true;
                }
                else if (c >= 0x0370 && c <= 0x03FF) greek = true;
                else if (c >= 0x0590 && c <= 0x05FF) hebrew = true;
                else if (c >= 0x0900 && c <= 0x097F) devanagari = true;
                else if (c >= 0x0E00 && c <= 0x0E7F) thai = true;
                else if (c >= 0x4E00 && c <= 0x9FFF) han = true;
                else if (c >= 0x3040 && c <= 0x30FF) kana = true;
                else if (c >= 0xAC00 && c <= 0xD7AF) hangul = true;
            }
            if (kana) return "ja";
            if (hangul) return "ko";
            if (han) return "zh-CN";
            if (arabic) return urdu ? "ur" : (persian ? "fa" : "ar");
            if (cyrillic) return ukrainian ? "uk" : "ru";
            if (greek) return "el";
            if (hebrew) return "he";
            if (devanagari) return "hi";
            if (thai) return "th";
            return null;
        }
    }
}
