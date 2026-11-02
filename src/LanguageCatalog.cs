// One language table for every surface: popup, main window, tray, documents,
// and the browser client. Adding a language here makes it available everywhere.

using System;
using System.Text;

namespace Qalam
{
    internal static class LanguageCatalog
    {
        private static readonly string[,] Entries = new string[,]
        {
            { "ar", "Arabic" }, { "en", "English" }, { "fr", "French" }, { "es", "Spanish" },
            { "de", "German" }, { "ru", "Russian" }, { "zh-CN", "Chinese (Simplified)" },
            { "zh-TW", "Chinese (Traditional)" }, { "tr", "Turkish" }, { "ur", "Urdu" },
            { "fa", "Persian" }, { "hi", "Hindi" }, { "pt", "Portuguese" }, { "it", "Italian" },
            { "ja", "Japanese" }, { "ko", "Korean" }, { "id", "Indonesian" },
            { "nl", "Dutch" }, { "pl", "Polish" }, { "uk", "Ukrainian" }, { "el", "Greek" },
            { "he", "Hebrew" }, { "vi", "Vietnamese" }, { "th", "Thai" }, { "ro", "Romanian" },
            { "cs", "Czech" }, { "sv", "Swedish" }, { "da", "Danish" }, { "no", "Norwegian" },
            { "fi", "Finnish" }, { "hu", "Hungarian" }, { "bg", "Bulgarian" },
            { "hr", "Croatian" }, { "sr", "Serbian" }, { "sk", "Slovak" },
            { "sl", "Slovenian" }, { "ca", "Catalan" }, { "tl", "Filipino" },
            { "ms", "Malay" }, { "bn", "Bengali" }, { "ta", "Tamil" }, { "te", "Telugu" },
            { "mr", "Marathi" }, { "sw", "Swahili" }, { "az", "Azerbaijani" },
            { "kk", "Kazakh" }, { "uz", "Uzbek" }, { "af", "Afrikaans" },
            { "sq", "Albanian" }, { "hy", "Armenian" }, { "ka", "Georgian" },
            { "km", "Khmer" }, { "lo", "Lao" }, { "my", "Burmese" }, { "ne", "Nepali" },
            { "si", "Sinhala" }, { "am", "Amharic" }, { "yo", "Yoruba" }, { "zu", "Zulu" },
            { "is", "Icelandic" }, { "ga", "Irish" }, { "cy", "Welsh" }, { "mt", "Maltese" },
            { "mk", "Macedonian" }, { "bs", "Bosnian" }, { "be", "Belarusian" },
            { "lt", "Lithuanian" }, { "lv", "Latvian" }, { "et", "Estonian" },
            { "eu", "Basque" }, { "gl", "Galician" }, { "eo", "Esperanto" }, { "la", "Latin" }
        };

        public static int Count { get { return Entries.GetLength(0); } }

        public static string Code(int index)
        {
            return index >= 0 && index < Count ? Entries[index, 0] : "";
        }

        public static string Name(int index)
        {
            return index >= 0 && index < Count ? Entries[index, 1] : "";
        }

        public static string NameOf(string code)
        {
            if (string.IsNullOrEmpty(code)) return "Auto";
            if (string.Equals(code, "zh", StringComparison.OrdinalIgnoreCase)) return "Chinese";
            for (int i = 0; i < Count; i++)
                if (string.Equals(Entries[i, 0], code, StringComparison.OrdinalIgnoreCase))
                    return Entries[i, 1];
            return code.ToUpperInvariant();
        }

        public static bool Contains(string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            for (int i = 0; i < Count; i++)
                if (string.Equals(Entries[i, 0], code, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public static string Json()
        {
            StringBuilder json = new StringBuilder();
            json.Append('[');
            for (int i = 0; i < Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append("{\"code\":\"");
                json.Append(JsonEscape(Entries[i, 0]));
                json.Append("\",\"name\":\"");
                json.Append(JsonEscape(Entries[i, 1]));
                json.Append("\"}");
            }
            json.Append(']');
            return json.ToString();
        }

        private static string JsonEscape(string value)
        {
            if (value == null) return "";
            StringBuilder builder = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                if (c == '"' || c == '\\') builder.Append('\\').Append(c);
                else if (c == '\n') builder.Append("\\n");
                else if (c == '\r') builder.Append("\\r");
                else if (c == '\t') builder.Append("\\t");
                else if (c < 0x20) builder.Append("\\u").Append(((int)c).ToString("x4"));
                else builder.Append(c);
            }
            return builder.ToString();
        }
    }
}
