// Colloquial Spanish/English phrasebook and language detection.
//
// Machine translators tend to flatten slang and profanity into literal nonsense.
// This table gives Qalam a curated rendering for common expressions and, more
// importantly, keeps the speaker's register when the phrase is sent to a local
// model. It is deliberately small and reviewable rather than an attempt at a
// complete slang dictionary.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Qalam
{
    internal sealed class ColloquialEntry
    {
        public string Spanish;
        public string English;
        public string Note;
        public string[] ExampleSpanish;
        public string[] ExampleEnglish;
        public string[] SpanishWords;
        public string[] EnglishWords;

        public ColloquialEntry(string spanish, string english, string note,
            string[] exampleSpanish, string[] exampleEnglish)
        {
            Spanish = spanish;
            English = english;
            Note = note;
            ExampleSpanish = exampleSpanish ?? new string[0];
            ExampleEnglish = exampleEnglish ?? new string[0];
            SpanishWords = PhraseWords(spanish);
            EnglishWords = PhraseWords(english);
        }

        private static string[] PhraseWords(string phrase)
        {
            string folded = Colloquial.Fold(phrase);
            return folded.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }

    internal sealed class ColloquialMatch
    {
        public ColloquialEntry Entry;
        public int WordStart;
        public int WordCount;
    }

    // The regional dialect an expression belongs to. A phrase can belong to
    // several at once (ماشي is Sudanese, Gulf, and Egyptian), so entries carry a
    // set rather than a single value.
    internal enum Lahja
    {
        Msa,
        Egyptian,
        Levantine,
        Gulf,
        Sudanese,
        Iraqi,
        Yemeni,
        Hijazi,
        Moroccan,
        Tunisian,
        Algerian,
        Libyan
    }

    internal static class LahjaInfo
    {
        private static readonly Lahja[] All = new Lahja[]
        {
            Lahja.Msa, Lahja.Egyptian, Lahja.Levantine, Lahja.Gulf, Lahja.Sudanese,
            Lahja.Iraqi, Lahja.Yemeni, Lahja.Hijazi, Lahja.Moroccan,
            Lahja.Tunisian, Lahja.Algerian, Lahja.Libyan
        };

        public static string Name(Lahja lahja)
        {
            switch (lahja)
            {
                case Lahja.Msa: return "MSA";
                case Lahja.Egyptian: return "Egyptian";
                case Lahja.Levantine: return "Levantine";
                case Lahja.Gulf: return "Gulf";
                case Lahja.Sudanese: return "Sudanese";
                case Lahja.Iraqi: return "Iraqi";
                case Lahja.Yemeni: return "Yemeni";
                case Lahja.Hijazi: return "Hijazi";
                case Lahja.Moroccan: return "Moroccan";
                case Lahja.Tunisian: return "Tunisian";
                case Lahja.Algerian: return "Algerian";
                case Lahja.Libyan: return "Libyan";
                default: return "";
            }
        }

        public static string Label(Lahja[] lahjaat)
        {
            if (lahjaat == null || lahjaat.Length == 0) return "";
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < lahjaat.Length; i++)
            {
                string name = Name(lahjaat[i]);
                if (name.Length == 0) continue;
                if (text.Length > 0) text.Append('/');
                text.Append(name);
            }
            return text.ToString();
        }

        // Deduplicates and sorts into enum order so a label reads the same way
        // whatever order a caller listed the dialects in.
        public static Lahja[] Normalize(Lahja[] lahjaat)
        {
            List<Lahja> ordered = new List<Lahja>();
            foreach (Lahja lahja in All)
                if (lahjaat != null && Array.IndexOf(lahjaat, lahja) >= 0) ordered.Add(lahja);
            return ordered.ToArray();
        }

        // Notes already open with the dialects they cover ("Levantine/Gulf; ..."),
        // so existing entries get their labels from that one sentence instead of
        // a second table that could drift out of sync with the prose.
        public static Lahja[] ParseNote(string note)
        {
            string head = note ?? "";
            int cut = head.IndexOf(';');
            if (cut < 0) cut = head.IndexOf('.');
            if (cut >= 0) head = head.Substring(0, cut);
            if (head.Length == 0) return new Lahja[0];

            List<Lahja> found = new List<Lahja>();
            foreach (Lahja lahja in All)
            {
                string name = Name(lahja);
                if (name.Length == 0) continue;
                if (head.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) found.Add(lahja);
            }
            return found.ToArray();
        }
    }

    internal sealed class ArabicColloquialEntry
    {
        public string Arabic;
        public string English;
        public string Note;
        public string[] ExampleArabic;
        public string[] ExampleEnglish;
        public string[] ArabicWords;
        public Lahja[] Lahjaat;
        public string LahjaLabel;

        public ArabicColloquialEntry(string arabic, string english, string note,
            string[] exampleArabic, string[] exampleEnglish, Lahja[] lahjaat)
        {
            Arabic = arabic;
            English = english;
            Note = note;
            ExampleArabic = exampleArabic ?? new string[0];
            ExampleEnglish = exampleEnglish ?? new string[0];
            ArabicWords = Colloquial.Fold(arabic).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Lahjaat = LahjaInfo.Normalize(lahjaat != null && lahjaat.Length > 0 ? lahjaat : LahjaInfo.ParseNote(note));
            LahjaLabel = LahjaInfo.Label(Lahjaat);
        }
    }

    internal sealed class ArabicMatch
    {
        public ArabicColloquialEntry Entry;
        public EnglishArabicEntry EnglishEntry;
        public int WordStart;
        public int WordCount;
    }

    internal sealed class EnglishArabicEntry
    {
        public string English;
        public string Arabic;
        public string Note;
        public string[] ExampleEnglish;
        public string[] ExampleArabic;
        public string[] EnglishWords;

        public EnglishArabicEntry(string english, string arabic, string note,
            string[] exampleEnglish, string[] exampleArabic)
        {
            English = english;
            Arabic = arabic;
            Note = note;
            ExampleEnglish = exampleEnglish ?? new string[0];
            ExampleArabic = exampleArabic ?? new string[0];
            EnglishWords = Colloquial.WordTokens(english);
        }
    }

    internal sealed class ColloquialToken
    {
        public string Text;
        public bool IsWord;
        public bool IsWhitespace;
        public int WordIndex = -1;
        public string Folded;
    }

    internal static class Colloquial
    {
        // The table is intentionally bilingual. English renderings are the ones
        // used for phrasebook composition; examples give the popup real usage.
        private static readonly List<ColloquialEntry> Entries = BuildEntries();
        private static readonly List<ArabicColloquialEntry> ArabicEntries = BuildArabicEntries();
        private static readonly List<EnglishArabicEntry> EnglishArabicEntries = BuildEnglishArabicEntries();

        private static readonly HashSet<string> ColloquialMarkers = BuildMarkers();
        private static readonly HashSet<string> ArabicMarkers = BuildArabicMarkers();

        private static readonly Dictionary<string, HashSet<string>> LatinStopwords = BuildStopwords();

        public static List<ColloquialMatch> FindMatches(string text, string sourceLanguage)
        {
            List<ColloquialMatch> matches = new List<ColloquialMatch>();
            if (string.IsNullOrWhiteSpace(text)) return matches;

            string source = NormalizeCode(sourceLanguage);
            if (source != "es" && source != "en") return matches;

            List<ColloquialToken> tokens = Tokenize(text);
            List<int> words = WordIndexes(tokens);
            int position = 0;
            while (position < words.Count)
            {
                ColloquialEntry best = null;
                int bestCount = 0;
                foreach (ColloquialEntry entry in Entries)
                {
                    string[] phrase = source == "en" ? entry.EnglishWords : entry.SpanishWords;
                    if (phrase.Length == 0 || phrase.Length <= bestCount) continue;
                    if (MatchesAt(tokens, words, position, phrase))
                    {
                        best = entry;
                        bestCount = phrase.Length;
                    }
                }

                if (best != null)
                {
                    ColloquialMatch match = new ColloquialMatch();
                    match.Entry = best;
                    match.WordStart = position;
                    match.WordCount = bestCount;
                    matches.Add(match);
                    position += bestCount;
                }
                else
                {
                    position++;
                }
            }
            return matches;
        }

        public static string ComposeTranslation(string text, string sourceLanguage, string targetLanguage)
        {
            string source = NormalizeCode(sourceLanguage);
            string target = NormalizeCode(targetLanguage);
            if (string.IsNullOrWhiteSpace(text)) return null;

            if (source == "ar" && target == "en") return ComposeArabic(text);
            if (source == "en" && target == "ar") return ComposeEnglishToArabic(text);

            // The curated Spanish renderings are Spanish -> English. A local model
            // handles other directions and any phrase that is not completely covered.
            if (source != "es" || target != "en") return null;

            List<ColloquialToken> tokens = Tokenize(text);
            List<int> words = WordIndexes(tokens);
            if (words.Count == 0) return null;

            List<ColloquialMatch> matches = FindMatches(text, source);
            if (matches.Count == 0) return null;

            int[] matchAtWord = new int[words.Count];
            for (int i = 0; i < matchAtWord.Length; i++) matchAtWord[i] = -1;
            int[] matchEndToken = new int[matches.Count];

            for (int m = 0; m < matches.Count; m++)
            {
                ColloquialMatch match = matches[m];
                for (int i = 0; i < match.WordCount; i++) matchAtWord[match.WordStart + i] = m;
                matchEndToken[m] = words[match.WordStart + match.WordCount - 1];
            }

            // Partial coverage would silently drop words. Leave those phrases to
            // the model rather than returning a half-translated sentence.
            for (int i = 0; i < matchAtWord.Length; i++)
                if (matchAtWord[i] < 0) return null;

            StringBuilder output = new StringBuilder();
            for (int i = 0; i < tokens.Count; i++)
            {
                ColloquialToken token = tokens[i];
                if (token.IsWord && token.WordIndex >= 0)
                {
                    int m = matchAtWord[token.WordIndex];
                    if (m >= 0 && matches[m].WordStart == token.WordIndex)
                    {
                        output.Append(matches[m].Entry.English);
                        i = matchEndToken[m];
                        continue;
                    }
                    if (m >= 0)
                    {
                        // Interior word of a match. It was emitted with the match.
                        continue;
                    }
                }
                output.Append(token.Text);
            }

            string result = CleanTargetPunctuation(output.ToString().Trim());
            if (result.Length == 0) return null;
            return Capitalize(result);
        }

        public static bool LooksColloquial(string text, string sourceLanguage)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string source = NormalizeCode(sourceLanguage);
            if (source == "ar")
            {
                if (FindArabicMatches(text).Count > 0) return true;
                foreach (ColloquialToken token in Tokenize(text))
                    if (token.IsWord && ArabicMarkers.Contains(token.Folded)) return true;
                return false;
            }
            if (FindMatches(text, sourceLanguage).Count > 0) return true;

            foreach (ColloquialToken token in Tokenize(text))
            {
                if (token.IsWord && ColloquialMarkers.Contains(token.Folded)) return true;
            }
            return false;
        }

        // Self-test hook: every Arabic entry must name at least one lahja so the
        // table cannot quietly grow unlabelled phrases again.
        public static int UnlabelledArabicEntries
        {
            get
            {
                int count = 0;
                foreach (ArabicColloquialEntry entry in ArabicEntries)
                    if (entry.Lahjaat.Length == 0) count++;
                return count;
            }
        }

        // Every lahja the matched phrasebook entries are used in, e.g.
        // "Egyptian/Gulf/Sudanese" for ماشي. Empty when nothing matched.
        public static string LahjaLabelFor(string text, string sourceLanguage)
        {
            if (NormalizeCode(sourceLanguage) != "ar") return "";
            List<Lahja> found = new List<Lahja>();
            foreach (ArabicMatch match in FindArabicMatches(text))
                foreach (Lahja lahja in match.Entry.Lahjaat)
                    if (!found.Contains(lahja)) found.Add(lahja);
            return LahjaInfo.Label(LahjaInfo.Normalize(found.ToArray()));
        }

        public static List<ExamplePair> ExamplesFor(string text, string sourceLanguage, string targetLanguage, int max)
        {
            List<ExamplePair> result = new List<ExamplePair>();
            string source = NormalizeCode(sourceLanguage);
            string target = NormalizeCode(targetLanguage);
            if (source == "ar" && target == "en")
                return ArabicExamplesFor(text, max);
            if (source == "en" && target == "ar")
                return EnglishArabicExamplesFor(text, max);
            bool spanishToEnglish = source == "es" && target == "en";
            bool englishToSpanish = source == "en" && target == "es";
            if (!spanishToEnglish && !englishToSpanish) return result;

            HashSet<string> seen = new HashSet<string>();

            // Prefer the longest expressions first. A query can contain both a
            // full phrase and a shorter idiom inside it.
            List<ColloquialEntry> ordered = EntriesIn(text, source);
            foreach (ColloquialEntry entry in ordered)
            {
                int count = Math.Min(entry.ExampleSpanish.Length, entry.ExampleEnglish.Length);
                for (int i = 0; i < count; i++)
                {
                    string sourceLine = spanishToEnglish ? entry.ExampleSpanish[i] : entry.ExampleEnglish[i];
                    string targetLine = spanishToEnglish ? entry.ExampleEnglish[i] : entry.ExampleSpanish[i];
                    if (string.IsNullOrEmpty(sourceLine) || string.IsNullOrEmpty(targetLine)) continue;
                    // The selected phrase is already at the top of the popup. Do not
                    // repeat it as its own context line.
                    if (string.Equals(SourceKey(sourceLine), SourceKey(text), StringComparison.Ordinal)) continue;
                    string key = Fold(sourceLine).Trim().ToLowerInvariant();
                    if (!seen.Add(key)) continue;

                    ExamplePair pair = new ExamplePair();
                    pair.Source = sourceLine;
                    pair.Target = targetLine;
                    pair.Term = spanishToEnglish ? entry.Spanish : entry.English;
                    pair.FromPhrasebook = true;
                    result.Add(pair);
                    if (result.Count >= max) return result;
                }
            }
            return result;
        }

        public static List<string> HintsFor(string text, string sourceLanguage, string targetLanguage)
        {
            List<string> hints = new List<string>();
            if (NormalizeCode(targetLanguage) == "en" && NormalizeCode(sourceLanguage) == "ar")
            {
                foreach (ArabicMatch match in FindArabicMatches(text))
                {
                    // The lahja rides along with the gloss so the model knows which
                    // dialect the expression belongs to, and the popup can show it.
                    string hint = match.Entry.Arabic + " = " + match.Entry.English;
                    if (!string.IsNullOrEmpty(match.Entry.LahjaLabel))
                        hint += " [" + match.Entry.LahjaLabel + "]";
                    if (!hints.Contains(hint)) hints.Add(hint);
                }
                return hints;
            }
            if (NormalizeCode(targetLanguage) == "ar" && NormalizeCode(sourceLanguage) == "en")
            {
                foreach (ArabicMatch match in FindEnglishArabicMatches(text))
                    if (match.EnglishEntry != null)
                    {
                        string hint = match.EnglishEntry.English + " = " + match.EnglishEntry.Arabic;
                        if (!hints.Contains(hint)) hints.Add(hint);
                    }
                return hints;
            }
            if (NormalizeCode(targetLanguage) != "en" || NormalizeCode(sourceLanguage) != "es") return hints;

            foreach (ColloquialMatch match in FindMatches(text, "es"))
            {
                string hint = match.Entry.Spanish + " = " + match.Entry.English;
                if (!hints.Contains(hint)) hints.Add(hint);
            }
            return hints;
        }

        private static string ComposeArabic(string text)
        {
            List<ColloquialToken> tokens = Tokenize(text);
            List<int> words = WordIndexes(tokens);
            if (words.Count == 0) return null;

            List<ArabicMatch> matches = FindArabicMatches(text);
            if (matches.Count == 0) return null;

            int[] matchAtWord = new int[words.Count];
            for (int i = 0; i < matchAtWord.Length; i++) matchAtWord[i] = -1;
            int[] matchEndToken = new int[matches.Count];

            for (int m = 0; m < matches.Count; m++)
            {
                ArabicMatch match = matches[m];
                for (int i = 0; i < match.WordCount; i++) matchAtWord[match.WordStart + i] = m;
                matchEndToken[m] = words[match.WordStart + match.WordCount - 1];
            }

            for (int i = 0; i < matchAtWord.Length; i++)
                if (matchAtWord[i] < 0) return null;

            StringBuilder output = new StringBuilder();
            for (int i = 0; i < tokens.Count; i++)
            {
                ColloquialToken token = tokens[i];
                if (token.IsWord && token.WordIndex >= 0)
                {
                    int m = matchAtWord[token.WordIndex];
                    if (m >= 0 && matches[m].WordStart == token.WordIndex)
                    {
                        output.Append(matches[m].Entry.English);
                        i = matchEndToken[m];
                        continue;
                    }
                    if (m >= 0) continue;
                }
                output.Append(TargetPunctuation(token.Text));
            }

            string result = CleanTargetPunctuation(output.ToString().Trim());
            if (result.Length == 0) return null;
            return Capitalize(result);
        }

        private static string CleanTargetPunctuation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // Spanish inverted marks do not belong in an English rendering.
            text = text.Replace("\u00a1", "").Replace("\u00bf", "");
            // A phrasebook entry may already end in ? or !; do not add the source
            // punctuation a second time.
            while (text.IndexOf("??", StringComparison.Ordinal) >= 0)
                text = text.Replace("??", "?");
            while (text.IndexOf("!!", StringComparison.Ordinal) >= 0)
                text = text.Replace("!!", "!");
            return text.Trim();
        }

        private static string TargetPunctuation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("\u061f", "?").Replace("\u060c", ",").Replace("\u061b", ";");
        }

        private static string ArabicPunctuation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("?", "\u061f").Replace(",", "\u060c").Replace(";", "\u061b");
        }

        private static string ComposeEnglishToArabic(string text)
        {
            List<ColloquialToken> tokens = Tokenize(text);
            List<int> words = WordIndexes(tokens);
            if (words.Count == 0) return null;

            List<ArabicMatch> matches = FindEnglishArabicMatches(text);
            if (matches.Count == 0) return null;

            int[] matchAtWord = new int[words.Count];
            for (int i = 0; i < matchAtWord.Length; i++) matchAtWord[i] = -1;
            int[] matchEndToken = new int[matches.Count];
            for (int m = 0; m < matches.Count; m++)
            {
                ArabicMatch match = matches[m];
                for (int i = 0; i < match.WordCount; i++) matchAtWord[match.WordStart + i] = m;
                matchEndToken[m] = words[match.WordStart + match.WordCount - 1];
            }
            for (int i = 0; i < matchAtWord.Length; i++)
                if (matchAtWord[i] < 0) return null;

            StringBuilder output = new StringBuilder();
            for (int i = 0; i < tokens.Count; i++)
            {
                ColloquialToken token = tokens[i];
                if (token.IsWord && token.WordIndex >= 0)
                {
                    int m = matchAtWord[token.WordIndex];
                    if (m >= 0 && matches[m].WordStart == token.WordIndex)
                    {
                        output.Append(matches[m].EnglishEntry == null
                            ? matches[m].Entry.Arabic
                            : matches[m].EnglishEntry.Arabic);
                        i = matchEndToken[m];
                        continue;
                    }
                    if (m >= 0) continue;
                }
                output.Append(ArabicPunctuation(token.Text));
            }

            string result = output.ToString().Trim();
            if (result.Length == 0) return null;
            return result;
        }

        private static List<ArabicMatch> FindEnglishArabicMatches(string text)
        {
            List<ArabicMatch> matches = new List<ArabicMatch>();
            if (string.IsNullOrWhiteSpace(text)) return matches;

            List<ColloquialToken> tokens = Tokenize(text);
            List<int> words = WordIndexes(tokens);
            int position = 0;
            while (position < words.Count)
            {
                EnglishArabicEntry best = null;
                int bestCount = 0;
                foreach (EnglishArabicEntry entry in EnglishArabicEntries)
                {
                    string[] phrase = entry.EnglishWords;
                    if (phrase.Length == 0 || phrase.Length <= bestCount) continue;
                    if (MatchesAt(tokens, words, position, phrase))
                    {
                        best = entry;
                        bestCount = phrase.Length;
                    }
                }

                if (best != null)
                {
                    ArabicMatch match = new ArabicMatch();
                    match.EnglishEntry = best;
                    match.WordStart = position;
                    match.WordCount = bestCount;
                    matches.Add(match);
                    position += bestCount;
                }
                else
                {
                    position++;
                }
            }
            return matches;
        }

        private static List<ArabicMatch> FindArabicMatches(string text)
        {
            List<ArabicMatch> matches = new List<ArabicMatch>();
            if (string.IsNullOrWhiteSpace(text)) return matches;

            List<ColloquialToken> tokens = Tokenize(text);
            List<int> words = WordIndexes(tokens);
            int position = 0;
            while (position < words.Count)
            {
                ArabicColloquialEntry best = null;
                int bestCount = 0;
                foreach (ArabicColloquialEntry entry in ArabicEntries)
                {
                    string[] phrase = entry.ArabicWords;
                    if (phrase.Length == 0 || phrase.Length <= bestCount) continue;
                    if (MatchesAt(tokens, words, position, phrase))
                    {
                        best = entry;
                        bestCount = phrase.Length;
                    }
                }

                if (best != null)
                {
                    ArabicMatch match = new ArabicMatch();
                    match.Entry = best;
                    match.WordStart = position;
                    match.WordCount = bestCount;
                    matches.Add(match);
                    position += bestCount;
                }
                else
                {
                    position++;
                }
            }
            return matches;
        }

        private static List<ExamplePair> EnglishArabicExamplesFor(string text, int max)
        {
            List<ExamplePair> result = new List<ExamplePair>();
            HashSet<string> seen = new HashSet<string>();
            List<EnglishArabicEntry> ordered = new List<EnglishArabicEntry>();
            List<ColloquialToken> tokens = Tokenize(text);
            List<int> words = WordIndexes(tokens);

            foreach (EnglishArabicEntry entry in EnglishArabicEntries)
            {
                string[] phrase = entry.EnglishWords;
                bool found = false;
                for (int position = 0; position + phrase.Length <= words.Count && !found; position++)
                    found = MatchesAt(tokens, words, position, phrase);
                if (found) ordered.Add(entry);
            }
            ordered.Sort(delegate(EnglishArabicEntry a, EnglishArabicEntry b)
            {
                return b.EnglishWords.Length.CompareTo(a.EnglishWords.Length);
            });

            foreach (EnglishArabicEntry entry in ordered)
            {
                int count = Math.Min(entry.ExampleEnglish.Length, entry.ExampleArabic.Length);
                for (int i = 0; i < count; i++)
                {
                    string sourceLine = entry.ExampleEnglish[i];
                    string targetLine = entry.ExampleArabic[i];
                    if (string.IsNullOrEmpty(sourceLine) || string.IsNullOrEmpty(targetLine)) continue;
                    if (string.Equals(SourceKey(sourceLine), SourceKey(text), StringComparison.Ordinal)) continue;
                    string key = Fold(sourceLine).Trim().ToLowerInvariant();
                    if (!seen.Add(key)) continue;

                    ExamplePair pair = new ExamplePair();
                    pair.Source = sourceLine;
                    pair.Target = targetLine;
                    pair.Term = entry.English;
                    pair.FromPhrasebook = true;
                    result.Add(pair);
                    if (result.Count >= max) return result;
                }
            }
            return result;
        }

        private static List<ExamplePair> ArabicExamplesFor(string text, int max)
        {
            List<ExamplePair> result = new List<ExamplePair>();
            HashSet<string> seen = new HashSet<string>();
            List<ArabicColloquialEntry> ordered = new List<ArabicColloquialEntry>();
            List<ColloquialToken> tokens = Tokenize(text);
            List<int> words = WordIndexes(tokens);

            foreach (ArabicColloquialEntry entry in ArabicEntries)
            {
                string[] phrase = entry.ArabicWords;
                bool found = false;
                for (int position = 0; position + phrase.Length <= words.Count && !found; position++)
                    found = MatchesAt(tokens, words, position, phrase);
                if (found) ordered.Add(entry);
            }
            ordered.Sort(delegate(ArabicColloquialEntry a, ArabicColloquialEntry b)
            {
                return b.ArabicWords.Length.CompareTo(a.ArabicWords.Length);
            });

            foreach (ArabicColloquialEntry entry in ordered)
            {
                int count = Math.Min(entry.ExampleArabic.Length, entry.ExampleEnglish.Length);
                for (int i = 0; i < count; i++)
                {
                    string sourceLine = entry.ExampleArabic[i];
                    string targetLine = entry.ExampleEnglish[i];
                    if (string.IsNullOrEmpty(sourceLine) || string.IsNullOrEmpty(targetLine)) continue;
                    if (string.Equals(SourceKey(sourceLine), SourceKey(text), StringComparison.Ordinal)) continue;
                    string key = Fold(sourceLine).Trim().ToLowerInvariant();
                    if (!seen.Add(key)) continue;

                    ExamplePair pair = new ExamplePair();
                    pair.Source = sourceLine;
                    pair.Target = targetLine;
                    pair.Term = entry.Arabic;
                    pair.FromPhrasebook = true;
                    result.Add(pair);
                    if (result.Count >= max) return result;
                }
            }
            return result;
        }

        public static string DetectLanguage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            bool hasArabic = false;
            bool hasPersian = false;
            bool hasUrdu = false;
            bool hasCyrillic = false;
            bool hasUkrainian = false;
            bool hasGreek = false;
            bool hasHebrew = false;
            bool hasDevanagari = false;
            bool hasThai = false;
            bool hasHan = false;
            bool hasKana = false;
            bool hasHangul = false;

            foreach (char c in text)
            {
                if ((c >= 0x0600 && c <= 0x06FF) || (c >= 0x0750 && c <= 0x077F) || (c >= 0x08A0 && c <= 0x08FF))
                {
                    hasArabic = true;
                    if (c == '\u067E' || c == '\u0686' || c == '\u0698' || c == '\u06AF') hasPersian = true;
                    if (c == '\u0679' || c == '\u0688' || c == '\u0691' || c == '\u06BA' || c == '\u06D2') hasUrdu = true;
                }
                if (c >= 0x0400 && c <= 0x04FF)
                {
                    hasCyrillic = true;
                    if (c == '\u0406' || c == '\u0407' || c == '\u0404' || c == '\u0490') hasUkrainian = true;
                }
                if (c >= 0x0370 && c <= 0x03FF) hasGreek = true;
                if (c >= 0x0590 && c <= 0x05FF) hasHebrew = true;
                if (c >= 0x0900 && c <= 0x097F) hasDevanagari = true;
                if (c >= 0x0E00 && c <= 0x0E7F) hasThai = true;
                if (c >= 0x4E00 && c <= 0x9FFF) hasHan = true;
                if ((c >= 0x3040 && c <= 0x30FF)) hasKana = true;
                if (c >= 0xAC00 && c <= 0xD7AF) hasHangul = true;
            }

            if (hasKana) return "ja";
            if (hasHangul) return "ko";
            if (hasHan) return "zh";
            if (hasArabic) return hasUrdu ? "ur" : (hasPersian ? "fa" : "ar");
            if (hasCyrillic) return hasUkrainian ? "uk" : "ru";
            if (hasGreek) return "el";
            if (hasHebrew) return "he";
            if (hasDevanagari) return "hi";
            if (hasThai) return "th";

            List<ColloquialToken> tokens = Tokenize(text);
            int frenchHints = 0;
            int spanishHints = 0;
            foreach (char letter in text.ToLowerInvariant())
            {
                if ("çœæàèùêâîôûëïÿ".IndexOf(letter) >= 0) frenchHints++;
                else if (letter == 'ñ' || letter == '¿' || letter == '¡') spanishHints++;
            }

            string bestCode = null;
            int bestScore = 0;
            foreach (KeyValuePair<string, HashSet<string>> language in LatinStopwords)
            {
                int hits = 0;
                foreach (ColloquialToken token in tokens)
                {
                    if (token.IsWord && language.Value.Contains(token.Folded)) hits++;
                }
                int score = hits * 3;
                if (language.Key == "fr") score += frenchHints * 2;
                else if (language.Key == "es") score += spanishHints * 3;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCode = language.Key;
                }
            }
            if (bestScore > 0) return bestCode;
            if (frenchHints > 0) return "fr";
            if (spanishHints > 0) return "es";
            return null;
        }

        public static string LanguageName(string code)
        {
            if (string.IsNullOrEmpty(code)) return "the source language";
            string normalized = NormalizeCode(code);
            if (normalized == "zh") return "Chinese";
            return LanguageCatalog.NameOf(code);
        }

        internal static string[] WordTokens(string phrase)
        {
            List<string> words = new List<string>();
            if (string.IsNullOrEmpty(phrase)) return words.ToArray();
            foreach (ColloquialToken token in Tokenize(phrase))
                if (token.IsWord) words.Add(token.Folded);
            return words.ToArray();
        }

        public static string Fold(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder builder = new StringBuilder(text.Length);
            foreach (char original in text)
            {
                char c = char.ToLowerInvariant(original);
                // Arabic selections can arrive with tashkeel. Matching ignores the
                // marks; the original text is still preserved for display.
                if (IsArabicMark(c) || c == '\u0640') continue;
                switch (c)
                {
                    case 'á': case 'à': case 'ä': case 'â': case 'ã': case 'å': c = 'a'; break;
                    case 'é': case 'è': case 'ë': case 'ê': c = 'e'; break;
                    case 'í': case 'ì': case 'ï': case 'î': c = 'i'; break;
                    case 'ó': case 'ò': case 'ö': case 'ô': case 'õ': c = 'o'; break;
                    case 'ú': case 'ù': case 'ü': case 'û': c = 'u'; break;
                    case 'ñ': c = 'n'; break;
                    case 'ç': c = 'c'; break;
                    case 'ß': c = 's'; break;
                    case '\u2019': c = '\''; break;
                }
                builder.Append(c);
            }
            return builder.ToString();
        }

        private static string SourceKey(string text)
        {
            StringBuilder builder = new StringBuilder();
            foreach (char c in Fold(text))
                if (char.IsLetterOrDigit(c)) builder.Append(c);
            return builder.ToString();
        }

        private static string Capitalize(string text)
        {
            char[] chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetter(chars[i])) continue;
                chars[i] = char.ToUpperInvariant(chars[i]);
                break;
            }
            return new string(chars);
        }

        private static List<ColloquialEntry> EntriesIn(string text, string source)
        {
            List<ColloquialEntry> result = new List<ColloquialEntry>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            List<ColloquialToken> tokens = Tokenize(text);
            List<int> words = WordIndexes(tokens);
            foreach (ColloquialEntry entry in Entries)
            {
                string[] phrase = source == "en" ? entry.EnglishWords : entry.SpanishWords;
                if (phrase.Length == 0) continue;
                bool found = false;
                for (int position = 0; position + phrase.Length <= words.Count && !found; position++)
                    found = MatchesAt(tokens, words, position, phrase);
                if (found) result.Add(entry);
            }

            result.Sort(delegate(ColloquialEntry a, ColloquialEntry b)
            {
                int aLength = (source == "en" ? a.EnglishWords : a.SpanishWords).Length;
                int bLength = (source == "en" ? b.EnglishWords : b.SpanishWords).Length;
                return bLength.CompareTo(aLength);
            });
            return result;
        }

        private static bool MatchesAt(List<ColloquialToken> tokens, List<int> words, int position, string[] phrase)
        {
            if (position + phrase.Length > words.Count) return false;

            for (int i = 0; i < phrase.Length; i++)
            {
                int tokenIndex = words[position + i];
                if (!string.Equals(tokens[tokenIndex].Folded, phrase[i], StringComparison.Ordinal)) return false;

                if (i > 0)
                {
                    int previous = words[position + i - 1];
                    for (int k = previous + 1; k < tokenIndex; k++)
                        if (!tokens[k].IsWhitespace) return false;
                }
            }
            return true;
        }

        private static List<ColloquialToken> Tokenize(string text)
        {
            List<ColloquialToken> tokens = new List<ColloquialToken>();
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c))
                {
                    int start = i;
                    while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                    tokens.Add(new ColloquialToken { Text = text.Substring(start, i - start), IsWhitespace = true });
                    continue;
                }

                if (IsWordCharacter(c))
                {
                    int start = i;
                    while (i < text.Length && IsWordCharacter(text[i])) i++;
                    string word = text.Substring(start, i - start);
                    tokens.Add(new ColloquialToken { Text = word, IsWord = true, Folded = Fold(word) });
                    continue;
                }

                int punctuationStart = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && !IsWordCharacter(text[i])) i++;
                tokens.Add(new ColloquialToken { Text = text.Substring(punctuationStart, i - punctuationStart) });
            }

            int wordIndex = 0;
            foreach (ColloquialToken token in tokens)
                if (token.IsWord) token.WordIndex = wordIndex++;
            return tokens;
        }

        private static List<int> WordIndexes(List<ColloquialToken> tokens)
        {
            List<int> words = new List<int>();
            for (int i = 0; i < tokens.Count; i++)
                if (tokens[i].IsWord) words.Add(i);
            return words;
        }

        private static bool IsWordCharacter(char c)
        {
            // Arabic combining marks are not letters, but they belong to the word
            // they modify. Splitting on them would turn one vocalized word into
            // several consonant fragments before Fold can remove the marks.
            return char.IsLetterOrDigit(c) || c == '\'' || c == '\u2019' || IsArabicMark(c);
        }

        private static bool IsArabicMark(char c)
        {
            return (c >= '\u0610' && c <= '\u061A') || (c >= '\u064B' && c <= '\u065F') ||
                   c == '\u0670' || (c >= '\u06D6' && c <= '\u06ED');
        }

        private static string NormalizeCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            string value = code.ToLowerInvariant();
            int dash = value.IndexOf('-');
            if (dash > 0) value = value.Substring(0, dash);
            return value;
        }

        private static List<ColloquialEntry> BuildEntries()
        {
            List<ColloquialEntry> entries = new List<ColloquialEntry>();

            Add(entries, "calla chingada", "shut the fuck up", "Vulgar imperative.",
                new string[] { "¡Calla chingada!", "Calla chingada, ya me hartaste." },
                new string[] { "Shut the fuck up!", "Shut the fuck up, I'm sick of you." });

            Add(entries, "no mames con mamar", "don't come at me with that bullshit", "Vulgar Mexican; 'don't give me that bullshit' or 'don't come at me'.",
                new string[] { "Calla chingada, no mames con mamar.", "No mames con mamar, ya me cansaste." },
                new string[] { "Shut the fuck up, don't come at me with that bullshit.", "Don't come at me with that bullshit, I'm tired of you." });

            Add(entries, "no mames", "no way", "Vulgar Mexican slang; disbelief or 'don't mess with me'.",
                new string[] { "¡No mames, güey!", "No mames, ¿en serio?", "No mames, qué chido." },
                new string[] { "No way, dude!", "You're kidding me, seriously?", "No way, that's so cool." });

            Add(entries, "no manches", "no way", "Euphemistic form of 'no mames'.",
                new string[] { "¡No manches, qué caro!", "No manches, se me olvidó." },
                new string[] { "No way, that's expensive!", "Don't mess with me, I forgot." });

            Add(entries, "qué caro", "that's expensive", "Common reaction to a high price.",
                new string[] { "¡Qué caro está todo!", "Qué caro el hotel." },
                new string[] { "Everything is so expensive!", "The hotel is really expensive." });

            Add(entries, "hijo de la chingada", "son of a bitch", "Vulgar.",
                new string[] { "Ese hijo de la chingada me robó.", "¡Hijo de la chingada!" },
                new string[] { "That son of a bitch robbed me.", "Son of a bitch!" });

            Add(entries, "de la chingada", "fucking terrible", "Vulgar intensifier.",
                new string[] { "El tráfico está de la chingada.", "Hace un calor de la chingada." },
                new string[] { "The traffic is fucking terrible.", "It's fucking hot." });

            Add(entries, "qué chingada", "what the fuck", "Vulgar.",
                new string[] { "¿Qué chingada pasó aquí?" },
                new string[] { "What the fuck happened here?" });

            Add(entries, "qué pedo", "what's up", "Mexican slang greeting or 'what's going on'.",
                new string[] { "¿Qué pedo, güey?", "¿Qué pedo contigo?" },
                new string[] { "What's up, dude?", "What's going on with you?" });

            Add(entries, "qué onda", "what's up", "Mexican slang greeting.",
                new string[] { "¿Qué onda, cómo estás?", "¿Qué onda con el trabajo?" },
                new string[] { "What's up, how are you?", "What's up with work?" });

            Add(entries, "güey", "dude", "Mexican slang; also 'idiot' depending on tone.",
                new string[] { "¿Qué haces, güey?", "No seas güey." },
                new string[] { "What are you doing, dude?", "Don't be an idiot." });

            Add(entries, "cabrón", "bastard", "Vulgar; can also mean 'dude' among friends.",
                new string[] { "Ese cabrón me robó.", "¿Qué haces, cabrón?" },
                new string[] { "That bastard robbed me.", "What are you doing, man?" });

            Add(entries, "pendejo", "idiot", "Vulgar.",
                new string[] { "No seas pendejo.", "Ese pendejo me mintió." },
                new string[] { "Don't be an idiot.", "That idiot lied to me." });

            Add(entries, "culero", "asshole", "Vulgar Mexican slang.",
                new string[] { "Ese culero no me ayudó.", "No seas culero." },
                new string[] { "That asshole didn't help me.", "Don't be an asshole." });

            Add(entries, "pinche", "fucking", "Vulgar intensifier in Mexican Spanish.",
                new string[] { "Pinche tráfico.", "Ese pinche ruido no me deja dormir." },
                new string[] { "Fucking traffic.", "That fucking noise won't let me sleep." });

            Add(entries, "chingón", "fucking awesome", "Vulgar Mexican slang; admiration.",
                new string[] { "Esa película estuvo chingona.", "Eres un chingón." },
                new string[] { "That movie was fucking awesome.", "You're a fucking badass." });

            Add(entries, "chingado", "fucking", "Vulgar Mexican adjective or intensifier.",
                new string[] { "Ese chingado ruido no me deja dormir.", "Está chingado." },
                new string[] { "That fucking noise won't let me sleep.", "It's fucked up." });

            Add(entries, "chido", "cool", "Mexican slang.",
                new string[] { "Está bien chido.", "Qué chido que viniste." },
                new string[] { "It's really cool.", "It's so cool that you came." });

            Add(entries, "verga", "fuck", "Vulgar; literally 'cock'.",
                new string[] { "¡Verga, se me hizo tarde!", "No me importa una verga." },
                new string[] { "Fuck, I'm late!", "I don't give a fuck." });

            Add(entries, "joder", "fuck", "Vulgar, especially Spain.",
                new string[] { "¡Joder, qué frío!", "No me jodas." },
                new string[] { "Fuck, it's cold!", "Don't fuck with me." });

            Add(entries, "coño", "fuck", "Vulgar, especially Spain.",
                new string[] { "¡Coño, se me olvidó!", "¿Qué coño quieres?" },
                new string[] { "Fuck, I forgot!", "What the fuck do you want?" });

            Add(entries, "hostia", "holy shit", "Vulgar, especially Spain.",
                new string[] { "¡Hostia, qué golpe!", "¡Hostia, no me digas!" },
                new string[] { "Holy shit, what a hit!", "Holy shit, don't tell me!" });

            Add(entries, "mierda", "shit", "Vulgar.",
                new string[] { "¡Mierda, perdí las llaves!", "Esto sabe a mierda." },
                new string[] { "Shit, I lost my keys!", "This tastes like shit." });

            Add(entries, "puta", "whore", "Vulgar; also used as an intensifier.",
                new string[] { "Esa puta me engañó.", "¡Puta madre!" },
                new string[] { "That whore cheated on me.", "Fuck!" });

            Add(entries, "follar", "fuck", "Vulgar, especially Spain.",
                new string[] { "Quiero follar contigo.", "No folles conmigo." },
                new string[] { "I want to fuck you.", "Don't fuck with me." });

            Add(entries, "perra", "bitch", "Vulgar.",
                new string[] { "Esa perra me traicionó.", "No seas perra." },
                new string[] { "That bitch betrayed me.", "Don't be a bitch." });

            Add(entries, "mamón", "jerk", "Vulgar Mexican slang.",
                new string[] { "Ese mamón nunca paga.", "No seas mamón." },
                new string[] { "That jerk never pays.", "Don't be a jerk." });

            Add(entries, "ojete", "asshole", "Vulgar Mexican slang.",
                new string[] { "Ese ojete me robó el lugar.", "No seas ojete." },
                new string[] { "That asshole took my spot.", "Don't be an asshole." });

            Add(entries, "chale", "damn", "Mexican slang; disappointment.",
                new string[] { "Chale, no puedo ir.", "Chale, qué pena." },
                new string[] { "Damn, I can't go.", "Damn, what a shame." });

            Add(entries, "órale", "alright", "Mexican slang; encouragement or surprise.",
                new string[] { "¡Órale, vámonos!", "Órale, no sabía eso." },
                new string[] { "Alright, let's go!", "Wow, I didn't know that." });

            Add(entries, "nel", "nope", "Mexican slang.",
                new string[] { "Nel, no voy a ir.", "Nel, eso no es cierto." },
                new string[] { "Nope, I'm not going.", "Nope, that's not true." });

            Add(entries, "simón", "yeah", "Mexican slang.",
                new string[] { "Simón, claro que sí.", "Simón, yo también." },
                new string[] { "Yeah, of course.", "Yeah, me too." });

            // More Mexican colloquialisms. These are deliberately phrase-level:
            // a bare word such as "padre" is too ambiguous to translate safely.
            Add(entries, "a huevo", "hell yeah", "Vulgar Mexican slang; enthusiastic agreement.",
                new string[] { "¡A huevo, nos vamos!", "A huevo que sí." },
                new string[] { "Hell yeah, we're going!", "Hell yeah, of course." });

            Add(entries, "de huevo", "definitely", "Mexican slang; 'of course' or 'for sure'.",
                new string[] { "De huevo voy a ir.", "De huevo que te aviso." },
                new string[] { "I'm definitely going.", "I'll definitely let you know." });

            Add(entries, "me vale madre", "I don't give a fuck", "Vulgar Mexican slang.",
                new string[] { "Me vale madre lo que digan.", "Me vale madre el tráfico." },
                new string[] { "I don't give a fuck what they say.", "I don't give a fuck about the traffic." });

            Add(entries, "vale verga", "it's fucked up", "Vulgar Mexican slang; something is bad or hopeless.",
                new string[] { "Ese trabajo vale verga.", "La situación vale verga." },
                new string[] { "That job is fucked up.", "The situation is fucked up." });

            Add(entries, "qué poca madre", "that's fucked up", "Vulgar Mexican expression of indignation.",
                new string[] { "Qué poca madre, me quedé sin dinero.", "Qué poca madre contigo." },
                new string[] { "That's fucked up, I ran out of money.", "That's fucked up of you." });

            Add(entries, "ni madres", "no way in hell", "Vulgar Mexican refusal.",
                new string[] { "Ni madres, no pienso ir.", "Ni madres te presto el coche." },
                new string[] { "No way in hell am I going.", "No way in hell am I lending you the car." });

            Add(entries, "a la verga", "fuck this", "Vulgar Mexican exclamation.",
                new string[] { "A la verga, ya me cansé.", "¡A la verga, qué susto!" },
                new string[] { "Fuck this, I'm tired.", "Fuck, what a scare!" });

            Add(entries, "hijo de puta", "son of a bitch", "Vulgar; stronger than 'hijo de la chingada' in some regions.",
                new string[] { "Ese hijo de puta me estafó.", "¡Hijo de puta!" },
                new string[] { "That son of a bitch scammed me.", "Son of a bitch!" });

            Add(entries, "chamba", "work", "Mexican slang for a job or shift.",
                new string[] { "Tengo mucha chamba hoy.", "¿Cómo va la chamba?" },
                new string[] { "I have a lot of work today.", "How's work going?" });

            Add(entries, "lana", "money", "Mexican slang.",
                new string[] { "No tengo lana.", "Se le acabó la lana." },
                new string[] { "I don't have money.", "He ran out of money." });

            Add(entries, "varo", "money", "Mexican slang, especially in the north.",
                new string[] { "¿Cuánto varo traes?", "No me queda varo." },
                new string[] { "How much money do you have?", "I have no money left." });

            Add(entries, "qué padre", "how cool", "Mexican slang; positive.",
                new string[] { "¡Qué padre que viniste!", "Qué padre está el lugar." },
                new string[] { "How cool that you came!", "The place is really cool." });

            Add(entries, "gacho", "lame", "Mexican slang; unpleasant or unfair.",
                new string[] { "Qué gacho lo que hiciste.", "Está gacho el clima." },
                new string[] { "What you did was lame.", "The weather is lousy." });

            Add(entries, "fresa", "preppy", "Mexican slang; posh or image-conscious.",
                new string[] { "Ese grupo es muy fresa.", "No seas fresa." },
                new string[] { "That group is very preppy.", "Don't be so posh." });

            Add(entries, "neta", "truth", "Mexican slang; 'for real' or 'the truth'.",
                new string[] { "¿Es neta lo que dices?", "La neta, no me gustó." },
                new string[] { "Is what you're saying for real?", "Honestly, I didn't like it." });

            Add(entries, "mande", "pardon?", "Mexican way of asking someone to repeat themselves.",
                new string[] { "¿Mande? No escuché.", "Mande, ¿cómo dijo?" },
                new string[] { "Pardon? I didn't hear.", "Pardon, what did you say?" });

            Add(entries, "ándale", "come on", "Mexican slang; encouragement or realization.",
                new string[] { "¡Ándale, tú puedes!", "Ándale, ya entendí." },
                new string[] { "Come on, you can do it!", "Oh, now I get it." });

            Add(entries, "no hay pedo", "no problem", "Mexican slang; mild, often friendly.",
                new string[] { "No hay pedo, yo te llevo.", "No hay pedo si llegas tarde." },
                new string[] { "No problem, I'll take you.", "No problem if you're late." });

            Add(entries, "está de huevos", "it's awesome", "Vulgar Mexican slang; very positive.",
                new string[] { "Esa banda está de huevos.", "La fiesta estuvo de huevos." },
                new string[] { "That band is awesome.", "The party was awesome." });

            // Peninsular Spanish.
            Add(entries, "gilipollas", "idiot", "Vulgar, especially Spain.",
                new string[] { "No seas gilipollas.", "Ese gilipollas me insultó." },
                new string[] { "Don't be an idiot.", "That idiot insulted me." });

            Add(entries, "capullo", "dickhead", "Vulgar, especially Spain.",
                new string[] { "Vaya capullo estás hecho.", "Ese capullo no me ayuda." },
                new string[] { "You're being a real dickhead.", "That dickhead won't help me." });

            Add(entries, "me cago en la leche", "for fuck's sake", "Vulgar Peninsular expression of frustration.",
                new string[] { "Me cago en la leche, se me olvidó.", "¡Me cago en la leche!" },
                new string[] { "For fuck's sake, I forgot.", "For fuck's sake!" });

            Add(entries, "tío", "dude", "Spain; informal address, not literally 'uncle' here.",
                new string[] { "¿Qué pasa, tío?", "Ese tío es mi amigo." },
                new string[] { "What's up, dude?", "That dude is my friend." });

            Add(entries, "tía", "chick", "Spain; informal for a woman, can be dismissive.",
                new string[] { "Esa tía es muy simpática.", "¿Quién es esa tía?" },
                new string[] { "That chick is really nice.", "Who is that chick?" });

            Add(entries, "me mola", "I like it", "Spain; 'molar' means to appeal to someone.",
                new string[] { "Me mola tu chaqueta.", "Esa canción me mola." },
                new string[] { "I like your jacket.", "I like that song." });

            Add(entries, "guay", "cool", "Spain; informal positive adjective.",
                new string[] { "Qué guay, vamos juntos.", "Eso suena muy guay." },
                new string[] { "How cool, let's go together.", "That sounds really cool." });

            Add(entries, "curro", "job", "Spain; informal for work or a job.",
                new string[] { "Tengo mucho curro esta semana.", "¿Cómo va el curro?" },
                new string[] { "I have a lot of work this week.", "How's the job going?" });

            Add(entries, "pasta", "money", "Spain; informal for money.",
                new string[] { "No tengo pasta para eso.", "Se gastó toda la pasta." },
                new string[] { "I don't have money for that.", "He spent all the money." });

            Add(entries, "flipo", "I'm amazed", "Spain; from 'flipar', to be stunned or amazed.",
                new string[] { "Flipo con tu paciencia.", "Flipo, no me lo puedo creer." },
                new string[] { "I'm amazed by your patience.", "I'm amazed, I can't believe it." });

            Add(entries, "venga", "come on", "Spain; encouragement or closing a conversation.",
                new string[] { "Venga, vámonos.", "Venga, hasta luego." },
                new string[] { "Come on, let's go.", "Alright, see you later." });

            Add(entries, "vale", "okay", "Spain; informal agreement.",
                new string[] { "Vale, nos vemos mañana.", "Vale, lo entiendo." },
                new string[] { "Okay, see you tomorrow.", "Okay, I understand." });

            Add(entries, "qué fuerte", "that's crazy", "Spain; reaction to something surprising or strong.",
                new string[] { "¡Qué fuerte, no me lo esperaba!", "Qué fuerte lo que pasó." },
                new string[] { "That's crazy, I didn't expect it!", "What happened is crazy." });

            Add(entries, "chorrada", "nonsense", "Spain; something silly or unimportant.",
                new string[] { "No digas chorradas.", "Eso es una chorrada." },
                new string[] { "Don't talk nonsense.", "That's nonsense." });

            Add(entries, "pringado", "loser", "Spain; someone who is unlucky or easily used.",
                new string[] { "No seas pringado.", "Ese pringado siempre paga." },
                new string[] { "Don't be a loser.", "That loser always pays." });

            Add(entries, "cutre", "crappy", "Spain; cheap or low quality.",
                new string[] { "El hotel era muy cutre.", "Qué sitio tan cutre." },
                new string[] { "The hotel was really crappy.", "What a crappy place." });

            Add(entries, "hostia puta", "holy fuck", "Vulgar, especially Spain.",
                new string[] { "¡Hostia puta, qué susto!", "Hostia puta, no me lo creo." },
                new string[] { "Holy fuck, what a scare!", "Holy fuck, I don't believe it." });

            Add(entries, "ni de coña", "no fucking way", "Vulgar, especially Spain.",
                new string[] { "Ni de coña voy a eso.", "Ni de coña te lo presto." },
                new string[] { "No fucking way am I going to that.", "No fucking way am I lending it to you." });

            Add(entries, "de puta madre", "fucking great", "Vulgar, especially Spain; very positive.",
                new string[] { "La cena estuvo de puta madre.", "Todo salió de puta madre." },
                new string[] { "Dinner was fucking great.", "Everything turned out fucking great." });

            Add(entries, "acojonante", "fucking amazing", "Vulgar, especially Spain.",
                new string[] { "La película fue acojonante.", "Ese concierto fue acojonante." },
                new string[] { "The movie was fucking amazing.", "That concert was fucking amazing." });

            // A second pass of common Mexican and Peninsular expressions. These
            // are reviewable renderings, not a dialect-wide dictionary.
            Add(entries, "no mames güey", "no way, dude", "Vulgar Mexican slang; disbelief.",
                new string[] { "¡No mames güey, se nos hizo tarde!", "No mames güey, qué caro." },
                new string[] { "No way, dude, we're late!", "No way, dude, that's expensive." });

            Add(entries, "vete a la verga", "go fuck yourself", "Vulgar Mexican dismissal.",
                new string[] { "Vete a la verga, no te aguanto.", "Vete a la verga de aquí." },
                new string[] { "Go fuck yourself, I can't stand you.", "Get the fuck out of here." });

            Add(entries, "me lleva la chingada", "I'm screwed", "Vulgar Mexican frustration.",
                new string[] { "Me lleva la chingada, perdí las llaves.", "¡Me lleva la chingada!" },
                new string[] { "I'm screwed, I lost the keys.", "Fuck my life!" });

            Add(entries, "está cabrón", "that's fucked up", "Vulgar Mexican; also 'it's brutal' about a situation.",
                new string[] { "Está cabrón el tráfico.", "Está cabrón lo que pasó." },
                new string[] { "The traffic is brutal.", "What happened is fucked up." });

            Add(entries, "no seas gacho", "don't be a jerk", "Mexican slang; unfair or mean.",
                new string[] { "No seas gacho, préstame el coche.", "No seas gacho con ella." },
                new string[] { "Don't be a jerk, lend me the car.", "Don't be a jerk to her." });

            Add(entries, "a poco", "seriously?", "Mexican; expresses doubt or surprise.",
                new string[] { "¿A poco no me crees?", "¿A poco ya se fueron?" },
                new string[] { "Seriously, you don't believe me?", "Seriously, they already left?" });

            Add(entries, "de volada", "right away", "Mexican slang; quickly.",
                new string[] { "Regreso de volada.", "Hazlo de volada." },
                new string[] { "I'll be right back.", "Do it right away." });

            Add(entries, "en chinga", "like crazy", "Vulgar Mexican; at full speed or nonstop.",
                new string[] { "Trabaja en chinga.", "Veníamos en chinga." },
                new string[] { "He works like crazy.", "We were rushing like crazy." });

            Add(entries, "qué oso", "how embarrassing", "Mexican slang; a social embarrassment.",
                new string[] { "¡Qué oso, me equivoqué de persona!", "Qué oso hablar así." },
                new string[] { "How embarrassing, I got the wrong person!", "It's embarrassing to talk like that." });

            Add(entries, "hacer el oso", "make a fool of yourself", "Mexican slang.",
                new string[] { "No hagas el oso en la fiesta.", "Hice el oso en la reunión." },
                new string[] { "Don't make a fool of yourself at the party.", "I made a fool of myself at the meeting." });

            Add(entries, "dar el avión", "string someone along", "Mexican slang; pretend to pay attention or keep a promise.",
                new string[] { "No me des el avión.", "Siempre me da el avión." },
                new string[] { "Don't string me along.", "He always strings me along." });

            Add(entries, "vaya tela", "what a mess", "Spain; reaction to an awkward or complicated situation.",
                new string[] { "Vaya tela con el tráfico.", "Vaya tela, otra vez." },
                new string[] { "What a mess with the traffic.", "What a mess, again." });

            Add(entries, "qué morro", "what nerve", "Spain; 'morro' is cheek or audacity.",
                new string[] { "¡Qué morro tienes!", "Qué morro, no pagó nada." },
                new string[] { "You've got some nerve!", "What nerve, he paid nothing." });

            Add(entries, "estar chupado", "be a piece of cake", "Spain; very easy.",
                new string[] { "El examen estuvo chupado.", "Está chupado, no te preocupes." },
                new string[] { "The exam was a piece of cake.", "It's a piece of cake, don't worry." });

            Add(entries, "dar palo", "be a drag", "Spain; something embarrassing or annoying to do.",
                new string[] { "Me da palo llamarlo.", "Da palo pedir ayuda." },
                new string[] { "It's a drag to call him.", "It's a drag to ask for help." });

            Add(entries, "hacer la pelota", "suck up", "Spain; flatter someone for advantage.",
                new string[] { "Siempre le hace la pelota al jefe.", "Deja de hacer la pelota." },
                new string[] { "He always sucks up to the boss.", "Stop sucking up." });

            Add(entries, "me cago en todo", "for fuck's sake", "Vulgar Peninsular frustration.",
                new string[] { "Me cago en todo, se me olvidó.", "¡Me cago en todo lo que se menea!" },
                new string[] { "For fuck's sake, I forgot.", "For fuck's sake!" });

            Add(entries, "quedarse de piedra", "be stunned", "Spain; literally 'turn to stone'.",
                new string[] { "Me quedé de piedra cuando lo vi.", "Se quedó de piedra con la noticia." },
                new string[] { "I was stunned when I saw it.", "She was stunned by the news." });

            Add(entries, "mogollón", "a lot", "Spain; informal quantity.",
                new string[] { "Hay un mogollón de gente.", "Me gusta un mogollón." },
                new string[] { "There are a lot of people.", "I like it a lot." });

            Add(entries, "a tope", "packed", "Spain; full to capacity or at maximum effort.",
                new string[] { "El metro va a tope.", "Estamos a tope de trabajo." },
                new string[] { "The metro is packed.", "We're swamped with work." });

            // Common inflected forms of the entries above, so a selection does not
            // have to be rewritten into the lemma before it can match.
            Add(entries, "está chupado", "it's a piece of cake", "Spain; very easy.",
                new string[] { "El examen está chupado.", "Está chupado, no te preocupes." },
                new string[] { "The exam is a piece of cake.", "It's a piece of cake, don't worry." });

            Add(entries, "me quedé de piedra", "I was stunned", "Spain; literally 'I turned to stone'.",
                new string[] { "Me quedé de piedra cuando lo vi.", "Me quedé de piedra con la noticia." },
                new string[] { "I was stunned when I saw it.", "I was stunned by the news." });

            Add(entries, "me da palo", "I can't be bothered", "Spain; something feels awkward or tiresome.",
                new string[] { "Me da palo llamarlo.", "Me da palo pedir ayuda." },
                new string[] { "I can't be bothered to call him.", "I can't be bothered to ask for help." });

            Add(entries, "no me des el avión", "don't string me along", "Mexican slang; don't give me the runaround.",
                new string[] { "No me des el avión, dime la verdad.", "No me des el avión." },
                new string[] { "Don't string me along, tell me the truth.", "Don't string me along." });

            Add(entries, "hice el oso", "I made a fool of myself", "Mexican slang; a public embarrassment.",
                new string[] { "Hice el oso en la reunión.", "Hice el oso delante de todos." },
                new string[] { "I made a fool of myself at the meeting.", "I made a fool of myself in front of everyone." });

            return entries;
        }

        private static void Add(List<ColloquialEntry> entries, string spanish, string english, string note,
            string[] exampleSpanish, string[] exampleEnglish)
        {
            entries.Add(new ColloquialEntry(spanish, english, note, exampleSpanish, exampleEnglish));
        }

        private static List<EnglishArabicEntry> BuildEnglishArabicEntries()
        {
            List<EnglishArabicEntry> entries = new List<EnglishArabicEntry>();

            AddEnglishArabic(entries, "shut the fuck up", "اخرس", "Vulgar; strong imperative.",
                new string[] { "Shut the fuck up and listen.", "Shut the fuck up, I'm done." },
                new string[] { "اخرس واسمع.", "اخرس، خلصت." });

            AddEnglishArabic(entries, "don't fuck with me", "لا تلعب معي", "Vulgar; do not mess with me.",
                new string[] { "Don't fuck with me today.", "Don't fuck with me, I'm serious." },
                new string[] { "لا تلعب معي اليوم.", "لا تلعب معي، أنا جاد." });

            AddEnglishArabic(entries, "what's up", "إيه الأخبار", "Egyptian greeting; literally 'what's the news?'.",
                new string[] { "What's up, my friend?", "What's up today?" },
                new string[] { "إيه الأخبار يا صاحبي؟", "إيه الأخبار النهارده؟" });

            AddEnglishArabic(entries, "how are you", "عامل إيه", "Egyptian, addressing a man.",
                new string[] { "How are you, Mohamed?", "How are you today?" },
                new string[] { "عامل إيه يا محمد؟", "عامل إيه النهارده؟" });

            AddEnglishArabic(entries, "what's this", "إيه ده", "Egyptian; also 'what is that?'.",
                new string[] { "What's this thing?", "What's this that happened?" },
                new string[] { "إيه ده الشيء؟", "إيه ده اللي حصل؟" });

            AddEnglishArabic(entries, "where are you going", "وين رايح", "Levantine/Gulf; literally 'where going'.",
                new string[] { "Where are you going now?", "Where are you going at this hour?" },
                new string[] { "وين رايح هلق؟", "وين رايح بهاد الوقت؟" });

            AddEnglishArabic(entries, "never mind", "معلش", "Egyptian; 'it's okay' or 'don't worry about it'.",
                new string[] { "Never mind, it turned out fine.", "Never mind, I'm sorry." },
                new string[] { "معلش، حصل خير.", "معلش، أنا آسف." });

            AddEnglishArabic(entries, "no way", "مستحيل", "Disbelief or refusal; literally 'impossible'.",
                new string[] { "No way, I don't believe it.", "No way am I going." },
                new string[] { "مستحيل، ما أصدق.", "مستحيل أروح." });

            AddEnglishArabic(entries, "come on", "يلا", "Encouragement or 'let's go'.",
                new string[] { "Come on, let's go.", "Come on, we're not late." },
                new string[] { "يلا بينا.", "يلا، متأخرناش." });

            AddEnglishArabic(entries, "enough", "خلاص", "'That's enough' or 'it's done'.",
                new string[] { "Enough, don't continue.", "Enough, the matter is over." },
                new string[] { "خلاص، لا تكمل.", "خلاص انتهى الموضوع." });

            AddEnglishArabic(entries, "son of a bitch", "ابن الكلب", "Vulgar Egyptian insult.",
                new string[] { "That son of a bitch took my money.", "You son of a bitch!" },
                new string[] { "ابن الكلب خد فلوسي.", "يا ابن الكلب!" });

            AddEnglishArabic(entries, "you idiot", "يا غبي", "Insulting; literally 'O stupid one'.",
                new string[] { "You idiot, that's wrong.", "You idiot, think before you speak." },
                new string[] { "يا غبي، ده غلط.", "يا غبي، فكر قبل ما تتكلم." });

            AddEnglishArabic(entries, "by the way", "على فكرة", "Conversational aside.",
                new string[] { "By the way, a message arrived for you.", "By the way, I'm busy tomorrow." },
                new string[] { "على فكرة، وصلت رسالة لك.", "على فكرة، أنا مشغول غدًا." });

            AddEnglishArabic(entries, "honestly", "بصراحة", "'Frankly'.",
                new string[] { "Honestly, I didn't like it.", "Honestly, you're right." },
                new string[] { "بصراحة، لم يعجبني.", "بصراحة، أنت محق." });

            AddEnglishArabic(entries, "don't worry", "ولا يهمك", "Levantine/Egyptian reassurance.",
                new string[] { "Don't worry, I'm with you.", "Don't worry, everything's fine." },
                new string[] { "ولا يهمك، أنا معك.", "ولا يهمك، كل شيء تمام." });

            AddEnglishArabic(entries, "I swear", "والله", "Emphasizes that something is true.",
                new string[] { "I swear I don't know.", "I swear you're telling the truth." },
                new string[] { "والله ما بعرف.", "والله إنك صادق." });

            AddEnglishArabic(entries, "okay", "تمام", "Agreement; 'all good'.",
                new string[] { "Okay, we'll meet tomorrow.", "Okay, I understand." },
                new string[] { "تمام، نلتقي غدًا.", "تمام، فهمت." });

            AddEnglishArabic(entries, "really", "عن جد", "Levantine; 'seriously?' or 'for real'.",
                new string[] { "Really? I don't believe it.", "For real, that's what happened?" },
                new string[] { "عن جد؟ ما بصدق.", "عن جد، هيك صار؟" });

            AddEnglishArabic(entries, "asshole", "عرص", "Vulgar Egyptian slang.",
                new string[] { "That asshole played me.", "Don't be an asshole." },
                new string[] { "العرص ده ضحك عليّا.", "بلاش تبقى عرص." });

            AddEnglishArabic(entries, "trash", "زبالة", "Literally garbage, also 'worthless'.",
                new string[] { "That talk is trash.", "The movie was trash." },
                new string[] { "الكلام ده زبالة.", "الفيلم كان زبالة." });

            AddEnglishArabic(entries, "go fuck yourself", "روح انقلع", "Vulgar Egyptian/Levantine dismissal.",
                new string[] { "Go fuck yourself, I'm done with you.", "Go fuck yourself and leave." },
                new string[] { "روح انقلع، خلصت منك.", "روح انقلع وامشي." });

            AddEnglishArabic(entries, "what do you think", "شو رايك", "Levantine; asks for an opinion.",
                new string[] { "What do you think we should do?", "What do you think of this?" },
                new string[] { "شو رايك نعمل؟", "شو رايك بهذا؟" });

            AddEnglishArabic(entries, "what happened", "شو صار", "Levantine; asks what occurred.",
                new string[] { "What happened here?", "What happened to you?" },
                new string[] { "شو صار هون؟", "شو صار معك؟" });

            AddEnglishArabic(entries, "good luck", "بالتوفيق", "MSA; wished before an effort.",
                new string[] { "Good luck on the exam.", "Good luck with your work." },
                new string[] { "بالتوفيق في الامتحان.", "بالتوفيق في شغلك." });

            AddEnglishArabic(entries, "watch out", "خد بالك", "Egyptian; warns of danger.",
                new string[] { "Watch out for the car.", "Watch out, the street is busy." },
                new string[] { "خد بالك من العربية.", "خد بالك، الشارع زحمة." });

            AddEnglishArabic(entries, "no problem", "ما في مشكلة", "Levantine; reassurance.",
                new string[] { "No problem, I'll handle it.", "No problem, we're done." },
                new string[] { "ما في مشكلة، أنا أتكفل.", "ما في مشكلة، خلصنا." });

            AddEnglishArabic(entries, "let's go", "يلا بينا", "Levantine/Egyptian; encouragement to leave.",
                new string[] { "Let's go, we're late.", "Let's go before it rains." },
                new string[] { "يلا بينا، متأخرناش.", "يلا بينا قبل ما تشتي." });

            AddEnglishArabic(entries, "excuse me", "لو سمحت", "Egyptian/MSA; polite interruption.",
                new string[] { "Excuse me, may I pass?", "Excuse me, I need help." },
                new string[] { "لو سمحت، ممكن أعدي؟", "لو سمحت، أحتاج مساعدة." });

            return entries;
        }

        private static void AddEnglishArabic(List<EnglishArabicEntry> entries, string english, string arabic,
            string note, string[] exampleEnglish, string[] exampleArabic)
        {
            entries.Add(new EnglishArabicEntry(english, arabic, note, exampleEnglish, exampleArabic));
        }

        private static List<ArabicColloquialEntry> BuildArabicEntries()
        {
            List<ArabicColloquialEntry> entries = new List<ArabicColloquialEntry>();

            // Egyptian Arabic.
            AddArabic(entries, "إيه ده", "what's this?", "Egyptian; also 'what is that?'.",
                new string[] { "إيه ده يا جماعة؟", "إيه ده اللي حصل؟" },
                new string[] { "What's this, everyone?", "What's this that happened?" });

            AddArabic(entries, "إيه الأخبار", "what's up?", "Egyptian greeting; literally 'what's the news?'.",
                new string[] { "إيه الأخبار يا صاحبي؟", "إيه الأخبار النهارده؟" },
                new string[] { "What's up, my friend?", "What's up today?" });

            AddArabic(entries, "عامل إيه", "how are you?", "Egyptian, addressing a man.",
                new string[] { "عامل إيه يا محمد؟", "عامل إيه في الشغل؟" },
                new string[] { "How are you, Mohamed?", "How are you at work?" });

            AddArabic(entries, "عاملة إيه", "how are you?", "Egyptian, addressing a woman.",
                new string[] { "عاملة إيه يا سارة؟", "عاملة إيه النهارده؟" },
                new string[] { "How are you, Sara?", "How are you today?" });

            AddArabic(entries, "معلش", "never mind", "Egyptian; 'it's okay' or 'don't worry about it'.",
                new string[] { "معلش، حصل خير.", "معلش، أنا آسف." },
                new string[] { "Never mind, it turned out fine.", "Never mind, I'm sorry." });

            AddArabic(entries, "حصل خير", "it turned out fine", "Egyptian; reassurance after a mishap.",
                new string[] { "معلش، حصل خير.", "حصل خير، متقلقش." },
                new string[] { "Never mind, it turned out fine.", "It turned out fine, don't worry." });

            AddArabic(entries, "يلا", "come on", "Egyptian/Levantine/Sudanese; also 'let's go'.",
                new string[] { "يلا بينا.", "يلا، متأخرناش." },
                new string[] { "Come on, let's go.", "Come on, we're not late." });

            AddArabic(entries, "كده", "like this", "Egyptian; 'thus' or 'in this way'.",
                new string[] { "اعملها كده.", "كده أحسن." },
                new string[] { "Do it like this.", "This way is better." });

            AddArabic(entries, "خالص", "at all", "Egyptian; also 'completely' or 'very'.",
                new string[] { "مش عارف خالص.", "ده غريب خالص." },
                new string[] { "I don't know at all.", "This is very strange." });

            AddArabic(entries, "حبيبي", "my friend", "Egyptian/Levantine; affectionate address, not literal romance.",
                new string[] { "إزيك يا حبيبي؟", "شكرًا يا حبيبي." },
                new string[] { "How are you, my friend?", "Thanks, my friend." });

            AddArabic(entries, "يا عم", "dude", "Egyptian; informal address, literally 'uncle'.",
                new string[] { "يا عم، استنى شوية.", "يا عم، إيه الحكاية؟" },
                new string[] { "Dude, wait a bit.", "Dude, what's the story?" });

            AddArabic(entries, "ابن الكلب", "son of a bitch", "Vulgar Egyptian insult.",
                new string[] { "ابن الكلب خد فلوسي.", "يا ابن الكلب!" },
                new string[] { "That son of a bitch took my money.", "You son of a bitch!" });

            AddArabic(entries, "عرص", "asshole", "Vulgar Egyptian slang.",
                new string[] { "العرص ده ضحك عليّا.", "بلاش تبقى عرص." },
                new string[] { "That asshole played me.", "Don't be an asshole." });

            AddArabic(entries, "زبالة", "trash", "Egyptian; literally garbage, also 'worthless'.",
                new string[] { "الكلام ده زبالة.", "الفيلم كان زبالة." },
                new string[] { "That talk is trash.", "The movie was trash." });

            // Levantine Arabic.
            AddArabic(entries, "شو الأخبار", "what's up?", "Levantine greeting; literally 'what's the news?'.",
                new string[] { "شو الأخبار يا صاحبي؟", "شو الأخبار اليوم؟" },
                new string[] { "What's up, my friend?", "What's up today?" });

            AddArabic(entries, "كيفك", "how are you?", "Levantine, addressing one person.",
                new string[] { "كيفك يا خي؟", "كيفك هلق؟" },
                new string[] { "How are you, brother?", "How are you now?" });

            AddArabic(entries, "وين رايح", "where are you going?", "Levantine/Gulf; literally 'where going'.",
                new string[] { "وين رايح هلق؟", "وين رايح بهاد الوقت؟" },
                new string[] { "Where are you going now?", "Where are you going at this hour?" });

            AddArabic(entries, "شو هاد", "what's this?", "Levantine; also 'what is that?'.",
                new string[] { "شو هاد الشي؟", "شو هاد الحكي؟" },
                new string[] { "What's this thing?", "What's this talk?" });

            AddArabic(entries, "خلص", "enough", "Levantine; also 'it's done'.",
                new string[] { "خلص، ما بدي أحكي.", "خلصنا من الشغل." },
                new string[] { "Enough, I don't want to talk.", "We finished work." });

            AddArabic(entries, "منيح", "good", "Levantine; 'okay' or 'well'.",
                new string[] { "أنا منيح، شكرًا.", "هاد منيح كتير." },
                new string[] { "I'm good, thanks.", "That's very good." });

            AddArabic(entries, "كتير", "a lot", "Levantine; also 'very'.",
                new string[] { "بحبك كتير.", "الشغل كتير اليوم." },
                new string[] { "I love you a lot.", "There's a lot of work today." });

            AddArabic(entries, "عن جد", "really?", "Levantine; 'seriously?' or 'for real'.",
                new string[] { "عن جد؟ ما بصدق.", "عن جد، هيك صار؟" },
                new string[] { "Really? I don't believe it.", "For real, that's what happened?" });

            AddArabic(entries, "والله", "I swear", "Levantine/MSA; used to emphasize truth.",
                new string[] { "والله ما بعرف.", "والله إنك صادق." },
                new string[] { "I swear I don't know.", "I swear you're telling the truth." });

            AddArabic(entries, "يا زلمة", "dude", "Levantine; informal address, literally 'O man'.",
                new string[] { "يا زلمة، شو صار؟", "يا زلمة، هاد غلط." },
                new string[] { "Dude, what happened?", "Dude, that's wrong." });

            AddArabic(entries, "ابن الحرام", "son of a bitch", "Vulgar Levantine insult.",
                new string[] { "ابن الحرام سرقني.", "يا ابن الحرام!" },
                new string[] { "That son of a bitch robbed me.", "You son of a bitch!" });

            AddArabic(entries, "شو بدك", "what do you want?", "Levantine; can sound blunt depending on tone.",
                new string[] { "شو بدك مني؟", "شو بدك هلق؟" },
                new string[] { "What do you want from me?", "What do you want now?" });

            // Gulf Arabic.
            AddArabic(entries, "وش أخبارك", "how are you?", "Gulf; literally 'what's your news?'.",
                new string[] { "وش أخبارك يا خوي؟", "وش أخبارك اليوم؟" },
                new string[] { "How are you, brother?", "How are you today?" });

            AddArabic(entries, "وش هذا", "what's this?", "Gulf; also 'what is that?'.",
                new string[] { "وش هذا الشي؟", "وش هذا الكلام؟" },
                new string[] { "What's this thing?", "What's this talk?" });

            AddArabic(entries, "خلاص", "enough", "Gulf/Sudanese/MSA; also 'it's done'.",
                new string[] { "خلاص، لا تكمل.", "خلاص انتهى الموضوع." },
                new string[] { "Enough, don't continue.", "Enough, the matter is over." });

            AddArabic(entries, "زين", "good", "Gulf; 'good' or 'okay'.",
                new string[] { "هذا زين.", "أنا زين، شكرًا." },
                new string[] { "This is good.", "I'm good, thanks." });

            AddArabic(entries, "وايد", "a lot", "Gulf; also 'very'.",
                new string[] { "أحبك وايد.", "الشغل وايد اليوم." },
                new string[] { "I love you a lot.", "There's a lot of work today." });

            AddArabic(entries, "يا ولد", "dude", "Gulf; informal address, literally 'O boy'.",
                new string[] { "يا ولد، شوف هذا.", "يا ولد، وين رايح؟" },
                new string[] { "Dude, look at this.", "Dude, where are you going?" });

            AddArabic(entries, "شلونك", "how are you?", "Gulf/Iraqi; informal greeting.",
                new string[] { "شلونك يا صاحبي؟", "شلونك اليوم؟" },
                new string[] { "How are you, my friend?", "How are you today?" });

            // Moroccan Arabic.
            AddArabic(entries, "لاباس", "how are you?", "Moroccan; also 'fine' or 'no problem'.",
                new string[] { "لاباس عليك؟", "أنا لاباس، شكرًا." },
                new string[] { "How are you?", "I'm fine, thanks." });

            AddArabic(entries, "بزاف", "a lot", "Moroccan; also 'very'.",
                new string[] { "كنحبك بزاف.", "الخدمة بزاف اليوم." },
                new string[] { "I love you a lot.", "There's a lot of work today." });

            AddArabic(entries, "دابا", "now", "Moroccan.",
                new string[] { "دابا نمشي.", "دابا فهمت." },
                new string[] { "I'm going now.", "Now I understand." });

            AddArabic(entries, "واخا", "okay", "Moroccan; agreement.",
                new string[] { "واخا، نمشي.", "واخا، ماشي مشكل." },
                new string[] { "Okay, let's go.", "Okay, no problem." });

            AddArabic(entries, "صافي", "enough", "Moroccan; also 'it's done'.",
                new string[] { "صافي، سالينا.", "صافي، ما بقاش." },
                new string[] { "Enough, we're done.", "Enough, there's none left." });

            AddArabic(entries, "كيفاش", "how?", "Moroccan.",
                new string[] { "كيفاش دخلتي؟", "كيفاش عرفتي؟" },
                new string[] { "How did you get in?", "How did you know?" });

            AddArabic(entries, "شنو", "what?", "Moroccan/Sudanese/Iraqi; also 'what is it?'.",
                new string[] { "شنو بغيتي؟", "شنو هاد؟" },
                new string[] { "What do you want?", "What's this?" });

            AddArabic(entries, "مزيان", "good", "Moroccan.",
                new string[] { "هاد مزيان.", "أنا مزيان، شكرًا." },
                new string[] { "This is good.", "I'm good, thanks." });

            AddArabic(entries, "خويا", "brother", "Moroccan; informal address for a friend.",
                new string[] { "كيفاش حالك يا خويا؟", "شكرًا يا خويا." },
                new string[] { "How are you, brother?", "Thanks, brother." });

            // Sudanese Arabic, plus the Gulf and Egyptian phrases it shares.
            AddArabic(entries, "ماشي", "going", "Sudanese/Gulf; present participle 'going'. Egyptian and Levantine use it for 'okay'.",
                new string[] { "أنا ماشي البيت.", "أنت ماشي الجيم قريب؟" },
                new string[] { "I'm going home.", "Are you going to the gym soon?" },
                Lahja.Sudanese, Lahja.Gulf);

            AddArabic(entries, "بعد بخلص من الشغل", "after I finish work", "Sudanese/Egyptian/Gulf; بعد here means 'later, afterwards', not MSA 'after'.",
                new string[] { "إن شاء الله بعد بخلص من الشغل اليوم.", "بعد بخلص من الشغل بجيك." },
                new string[] { "God willing, after I finish work today.", "After I finish work, I'll come to you." },
                Lahja.Sudanese, Lahja.Egyptian, Lahja.Gulf);

            AddArabic(entries, "قريب", "soon", "Sudanese/Gulf; also 'near'.",
                new string[] { "قريب نتلاقى.", "أنا جاي قريب." },
                new string[] { "We'll meet soon.", "I'm coming soon." },
                Lahja.Sudanese, Lahja.Gulf);

            AddArabic(entries, "هسي", "now", "Sudanese; 'right now'.",
                new string[] { "هسي بجي.", "هسي فهمت." },
                new string[] { "I'm coming now.", "Now I understand." },
                Lahja.Sudanese);

            AddArabic(entries, "زول", "guy", "Sudanese; a person, 'guy' or 'man'.",
                new string[] { "الزول ده صاحبي.", "الزول ده وين؟" },
                new string[] { "This guy is my friend.", "Where is this guy?" },
                Lahja.Sudanese);

            AddArabic(entries, "كيفنك", "how are you?", "Sudanese; informal greeting.",
                new string[] { "كيفنك يا صاحبي؟", "كيفنك اليوم؟" },
                new string[] { "How are you, my friend?", "How are you today?" },
                Lahja.Sudanese);

            AddArabic(entries, "الجيم", "the gym", "Sudanese/Gulf/Egyptian; loanword used across dialects.",
                new string[] { "أنا ماشي الجيم.", "الجيم قريب من البيت." },
                new string[] { "I'm going to the gym.", "The gym is close to home." },
                Lahja.Sudanese, Lahja.Gulf, Lahja.Egyptian);

            // Iraqi Arabic.
            AddArabic(entries, "شكو ماكو", "what's up?", "Iraqi; everyday greeting, literally 'what is there?'.",
                new string[] { "شكو ماكو يا صاحبي؟", "شكو ماكو اليوم؟" },
                new string[] { "What's up, my friend?", "What's up today?" },
                Lahja.Iraqi);

            AddArabic(entries, "ماكو مشكلة", "no problem", "Iraqi; ماكو is 'there is none'.",
                new string[] { "ماكو مشكلة، أنا معك.", "ماكو مشكلة، نلتقي غدًا." },
                new string[] { "No problem, I'm with you.", "No problem, we'll meet tomorrow." },
                Lahja.Iraqi);

            AddArabic(entries, "هسة", "now", "Iraqi; also written هسا.",
                new string[] { "هسة بجي.", "هسة فهمت." },
                new string[] { "I'm coming now.", "Now I understand." },
                Lahja.Iraqi);

            AddArabic(entries, "خوش", "good", "Iraqi; 'good' or 'well done'.",
                new string[] { "خوش شغل.", "هذا خوش شي." },
                new string[] { "Good work.", "This is a good thing." },
                Lahja.Iraqi);

            AddArabic(entries, "هواية", "a lot", "Iraqi; also 'very'.",
                new string[] { "أحبك هواية.", "الشغل هواية اليوم." },
                new string[] { "I love you a lot.", "There's a lot of work today." },
                Lahja.Iraqi);

            // Yemeni Arabic.
            AddArabic(entries, "ما شي مشكلة", "no problem", "Yemeni; ما شي is 'there is none'.",
                new string[] { "ما شي مشكلة، أنا معك.", "ما شي مشكلة، خلصنا." },
                new string[] { "No problem, I'm with you.", "No problem, we're done." },
                Lahja.Yemeni);

            AddArabic(entries, "حياك الله", "welcome", "Yemeni/Gulf/Hijazi/Sudanese; greeting or welcome.",
                new string[] { "حياك الله يا صاحبي.", "حياك الله، تفضل." },
                new string[] { "Welcome, my friend.", "Welcome, go ahead." },
                Lahja.Yemeni, Lahja.Gulf, Lahja.Hijazi, Lahja.Sudanese);

            // Hijazi Arabic.
            AddArabic(entries, "إيش أخبارك", "how are you?", "Hijazi/Gulf; literally 'what's your news?'.",
                new string[] { "إيش أخبارك يا صاحبي؟", "إيش أخبارك اليوم؟" },
                new string[] { "How are you, my friend?", "How are you today?" },
                Lahja.Hijazi, Lahja.Gulf);

            AddArabic(entries, "دحين", "now", "Hijazi; 'right now'.",
                new string[] { "دحين بجي.", "دحين فهمت." },
                new string[] { "I'm coming now.", "Now I understand." },
                Lahja.Hijazi);

            AddArabic(entries, "أبغى", "I want", "Hijazi; also 'I would like'.",
                new string[] { "أبغى أروح.", "أبغى هذا." },
                new string[] { "I want to go.", "I want this." },
                Lahja.Hijazi);

            AddArabic(entries, "ما أدري", "I don't know", "Hijazi/Gulf; casual.",
                new string[] { "ما أدري وش صار.", "ما أدري، والله." },
                new string[] { "I don't know what happened.", "I don't know, I swear." },
                Lahja.Hijazi, Lahja.Gulf);

            AddArabic(entries, "كذا", "like this", "Hijazi/Gulf; 'thus' or 'in this way'.",
                new string[] { "سوّها كذا.", "كذا أحسن." },
                new string[] { "Do it like this.", "This way is better." },
                Lahja.Hijazi, Lahja.Gulf);

            AddArabic(entries, "زحمة", "crowded", "Hijazi/Gulf/Egyptian; also 'traffic'.",
                new string[] { "الشارع زحمة اليوم.", "المكان زحمة." },
                new string[] { "The street is crowded today.", "The place is crowded." },
                Lahja.Hijazi, Lahja.Gulf, Lahja.Egyptian);

            // Everyday Levantine, Egyptian, Gulf, and Moroccan additions.
            AddArabic(entries, "هلق", "now", "Levantine; also written هلأ.",
                new string[] { "هلق بجي.", "هلق فهمت." },
                new string[] { "I'm coming now.", "Now I understand." },
                Lahja.Levantine);

            AddArabic(entries, "بدي", "I want", "Levantine; also 'I need'.",
                new string[] { "بدي أروح.", "بدي هذا." },
                new string[] { "I want to go.", "I want this." },
                Lahja.Levantine);

            AddArabic(entries, "عنجد", "really", "Levantine; 'seriously'.",
                new string[] { "عنجد؟ ما بصدق.", "عنجد، هيك صار؟" },
                new string[] { "Really? I don't believe it.", "For real, that's what happened?" },
                Lahja.Levantine);

            AddArabic(entries, "شوي", "a little", "Levantine/Gulf; 'a bit'.",
                new string[] { "استنى شوي.", "بحكي شوي عربي." },
                new string[] { "Wait a little.", "I speak a little Arabic." },
                Lahja.Levantine, Lahja.Gulf);

            AddArabic(entries, "يا خي", "brother", "Levantine; friendly address, literally 'O brother'.",
                new string[] { "شكرًا يا خي.", "كيفك يا خي؟" },
                new string[] { "Thanks, brother.", "How are you, brother?" },
                Lahja.Levantine);

            AddArabic(entries, "عايز", "I want", "Egyptian; also written عاوز.",
                new string[] { "عايز أروح.", "عايز هذا." },
                new string[] { "I want to go.", "I want this." },
                Lahja.Egyptian);

            AddArabic(entries, "أوي", "very", "Egyptian; intensifier after an adjective.",
                new string[] { "ده غريب أوي.", "أنا تعبان أوي." },
                new string[] { "This is very strange.", "I'm very tired." },
                Lahja.Egyptian);

            AddArabic(entries, "كمان", "also", "Egyptian/Levantine; 'too' or 'as well'.",
                new string[] { "أنا كمان جاي.", "عايز ده كمان." },
                new string[] { "I'm coming too.", "I want this as well." },
                Lahja.Egyptian, Lahja.Levantine);

            AddArabic(entries, "بص", "look", "Egyptian; attention-getter, 'look here'.",
                new string[] { "بص، عندي فكرة.", "بص على ده." },
                new string[] { "Look, I have an idea.", "Look at this." },
                Lahja.Egyptian);

            AddArabic(entries, "الحين", "now", "Gulf; 'right now'.",
                new string[] { "الحين بجي.", "الحين فهمت." },
                new string[] { "I'm coming now.", "Now I understand." },
                Lahja.Gulf);

            AddArabic(entries, "وش", "what", "Gulf/Hijazi; also 'what is'.",
                new string[] { "وش تبي؟", "وش صار؟" },
                new string[] { "What do you want?", "What happened?" },
                Lahja.Gulf, Lahja.Hijazi);

            AddArabic(entries, "أبا", "I want", "Gulf; also 'I would like'.",
                new string[] { "أبا أروح.", "أبا هذا." },
                new string[] { "I want to go.", "I want this." },
                Lahja.Gulf);

            AddArabic(entries, "بغيت", "I want", "Moroccan; also 'I would like'.",
                new string[] { "بغيت نمشي.", "بغيت هذا." },
                new string[] { "I want to go.", "I want this." },
                Lahja.Moroccan);

            AddArabic(entries, "شحال", "how much?", "Moroccan; asks a price or quantity.",
                new string[] { "شحال هذا؟", "شحال الكيلو؟" },
                new string[] { "How much is this?", "How much is a kilo?" },
                Lahja.Moroccan);

            AddArabic(entries, "فين", "where?", "Moroccan; also 'where is'.",
                new string[] { "فين كاينة؟", "فين مشيت؟" },
                new string[] { "Where is it?", "Where did you go?" },
                Lahja.Moroccan);

            AddArabic(entries, "بسلامة", "goodbye", "Moroccan; said on leaving.",
                new string[] { "بسلامة، نلتقي غدًا.", "بسلامة يا خويا." },
                new string[] { "Goodbye, we'll meet tomorrow.", "Goodbye, brother." },
                Lahja.Moroccan);

            // Tunisian, Algerian, and Libyan Arabic.
            AddArabic(entries, "برشا", "a lot", "Tunisian; also 'very'.",
                new string[] { "نحبك برشا.", "الخدمة برشا اليوم." },
                new string[] { "I love you a lot.", "There's a lot of work today." },
                Lahja.Tunisian);

            AddArabic(entries, "باهي", "okay", "Tunisian/Libyan; 'good' or 'fine'.",
                new string[] { "باهي، نمشي.", "باهي، أنا موافق." },
                new string[] { "Okay, let's go.", "Okay, I agree." },
                Lahja.Tunisian, Lahja.Libyan);

            AddArabic(entries, "شكون", "who?", "Tunisian/Moroccan; asks who.",
                new string[] { "شكون هذا؟", "شكون قال هذا؟" },
                new string[] { "Who is this?", "Who said this?" },
                Lahja.Tunisian, Lahja.Moroccan);

            AddArabic(entries, "علاش", "why?", "Tunisian/Moroccan; asks why.",
                new string[] { "علاش ما جيتش؟", "علاش هذا؟" },
                new string[] { "Why didn't you come?", "Why this?" },
                Lahja.Tunisian, Lahja.Moroccan);

            AddArabic(entries, "واش", "what?", "Algerian/Moroccan; also 'is it that'.",
                new string[] { "واش راك؟", "واش هذا؟" },
                new string[] { "How are you?", "What is this?" },
                Lahja.Algerian, Lahja.Moroccan);

            AddArabic(entries, "هلبا", "a lot", "Libyan; also 'very'.",
                new string[] { "نحبك هلبا.", "الخدمة هلبا اليوم." },
                new string[] { "I love you a lot.", "There's a lot of work today." },
                Lahja.Libyan);

            AddArabic(entries, "نبي", "I want", "Libyan; also 'I would like'.",
                new string[] { "نبي نمشي.", "نبي هذا." },
                new string[] { "I want to go.", "I want this." },
                Lahja.Libyan);

            // Shared MSA expressions that are common in dialect speech.
            AddArabic(entries, "يا سلام", "wow", "MSA; admiration or delight.",
                new string[] { "يا سلام، إيه الجمال ده!", "يا سلام على الكلام." },
                new string[] { "Wow, what beauty!" , "Wow, what fine words." });

            AddArabic(entries, "ما شاء الله", "wow, good", "MSA; appreciation, often for something impressive.",
                new string[] { "ما شاء الله، شغلك ممتاز.", "ما شاء الله عليك." },
                new string[] { "Wow, your work is excellent.", "Good for you." });

            AddArabic(entries, "إن شاء الله", "God willing", "MSA; used for future plans.",
                new string[] { "إن شاء الله نلتقي غدًا.", "إن شاء الله خير." },
                new string[] { "God willing, we'll meet tomorrow.", "God willing, it will be fine." });

            AddArabic(entries, "الحمد لله", "thank God", "MSA; common reply about one's state.",
                new string[] { "الحمد لله، أنا بخير.", "الحمد لله على كل حال." },
                new string[] { "Thank God, I'm fine.", "Thank God in any case." });

            AddArabic(entries, "يا رجل", "dude", "MSA; informal address, literally 'O man'.",
                new string[] { "يا رجل، ما هذا الكلام؟", "يا رجل، استمر." },
                new string[] { "Dude, what is this talk?", "Dude, keep going." });

            AddArabic(entries, "كفى", "enough", "MSA; 'that's enough'.",
                new string[] { "كفى، لا أريد المزيد.", "كفى جدالًا." },
                new string[] { "Enough, I don't want more.", "Enough arguing." });

            AddArabic(entries, "تبًا", "damn", "MSA; mild expletive.",
                new string[] { "تبًا، نسيت المفاتيح.", "تبًا لك." },
                new string[] { "Damn, I forgot the keys.", "Damn you." });

            AddArabic(entries, "اللعنة", "damn it", "MSA; strong expletive.",
                new string[] { "اللعنة، فاتني القطار.", "اللعنة على هذا اليوم." },
                new string[] { "Damn it, I missed the train.", "Damn this day." });

            AddArabic(entries, "على فكرة", "by the way", "MSA; conversational aside.",
                new string[] { "على فكرة، وصلت رسالة لك.", "على فكرة، أنا مشغول غدًا." },
                new string[] { "By the way, a message arrived for you.", "By the way, I'm busy tomorrow." });

            AddArabic(entries, "بصراحة", "honestly", "MSA; 'frankly'.",
                new string[] { "بصراحة، لم يعجبني.", "بصراحة، أنت محق." },
                new string[] { "Honestly, I didn't like it.", "Honestly, you're right." });

            AddArabic(entries, "اخرس", "shut up", "MSA; strong/rude imperative, understood across dialects.",
                new string[] { "اخرس، لا أريد أن أسمعك.", "اخرسوا قليلًا." },
                new string[] { "Shut up, I don't want to hear you.", "Shut up for a moment." });

            AddArabic(entries, "يا حمار", "you idiot", "MSA; insulting, literally 'O donkey', used across dialects.",
                new string[] { "يا حمار، انتبه!", "يا حمار، ده مش شغلك." },
                new string[] { "You idiot, watch out!", "You idiot, that's not your business." });

            AddArabic(entries, "يا غبي", "you idiot", "MSA; insulting, literally 'O stupid one'.",
                new string[] { "يا غبي، ده غلط.", "يا غبي، فكر قبل ما تتكلم." },
                new string[] { "You idiot, that's wrong.", "You idiot, think before you speak." });

            AddArabic(entries, "شو القصة", "what's going on?", "Levantine; literally 'what's the story?'.",
                new string[] { "شو القصة هون؟", "شو القصة معك؟" },
                new string[] { "What's going on here?", "What's going on with you?" });

            AddArabic(entries, "ولا يهمك", "don't worry", "Levantine/Egyptian; reassurance.",
                new string[] { "ولا يهمك، أنا معك.", "ولا يهمك، كل شيء تمام." },
                new string[] { "Don't worry, I'm with you.", "Don't worry, everything's fine." });

            AddArabic(entries, "تمام", "perfect", "Egyptian/Levantine; 'okay' or 'all good'.",
                new string[] { "تمام، نلتقي غدًا.", "كل شيء تمام." },
                new string[] { "Perfect, we'll meet tomorrow.", "Everything's fine." });

            // A second pass focused on everyday Egyptian, Levantine, Gulf,
            // Moroccan, and shared MSA expressions. Entries are matched as whole
            // phrases; a partially covered sentence falls through to the model.
            AddArabic(entries, "إزيك", "how are you?", "Egyptian; informal greeting.",
                new string[] { "إزيك يا صاحبي؟", "إزيك النهارده؟" },
                new string[] { "How are you, my friend?", "How are you today?" });

            AddArabic(entries, "إيه يا عم", "what's up, dude", "Egyptian; informal address, literally 'what, O uncle?'.",
                new string[] { "إيه يا عم، إنت فين؟", "إيه يا عم، ما تيجي هنا." },
                new string[] { "What's up, dude, where are you?", "Come on, dude, come here." });

            AddArabic(entries, "يا باشا", "boss", "Egyptian; respectful but informal address.",
                new string[] { "شكرًا يا باشا.", "اتفضل يا باشا." },
                new string[] { "Thank you, boss.", "Go ahead, boss." });

            AddArabic(entries, "كده كده", "so-so", "Egyptian; 'same either way' or 'not great'.",
                new string[] { "الدنيا كده كده.", "كده كده، مفيش فرق." },
                new string[] { "Life is so-so.", "So-so, there's no difference." });

            AddArabic(entries, "على طول", "right away", "Egyptian; also 'straight ahead', depending on context.",
                new string[] { "هوصل على طول.", "امشي على طول." },
                new string[] { "I'll arrive right away.", "Walk straight ahead." });

            AddArabic(entries, "مش فارقة", "doesn't matter", "Egyptian; 'it makes no difference'.",
                new string[] { "مش فارقة معايا.", "مش فارقة، خلاص." },
                new string[] { "It doesn't matter to me.", "It doesn't matter, forget it." });

            AddArabic(entries, "ما علينا", "never mind", "Egyptian/Levantine; let it go.",
                new string[] { "ما علينا، خلاص.", "ما علينا من الكلام." },
                new string[] { "Never mind, it's over.", "Never mind the talk." });

            AddArabic(entries, "خد بالك", "watch out", "Egyptian; also 'take care'.",
                new string[] { "خد بالك من الشارع.", "خد بالك من نفسك." },
                new string[] { "Watch out for the street.", "Take care of yourself." });

            AddArabic(entries, "بجد", "seriously", "Egyptian; 'for real'.",
                new string[] { "بجد؟ ما أصدق.", "بجد، أنا مش زعلان." },
                new string[] { "Seriously? I don't believe it.", "Seriously, I'm not upset." });

            AddArabic(entries, "جامد", "awesome", "Egyptian; literally 'solid', also 'intense'.",
                new string[] { "الفيلم ده جامد.", "شغلك جامد." },
                new string[] { "That movie is awesome.", "Your work is awesome." });

            AddArabic(entries, "زي الفل", "great", "Egyptian; literally 'like jasmine', very positive.",
                new string[] { "كل حاجة زي الفل.", "الجو زي الفل." },
                new string[] { "Everything is great.", "The weather is great." });

            AddArabic(entries, "دلوقتي", "now", "Egyptian; 'right now'.",
                new string[] { "أنا جاي دلوقتي.", "دلوقتي فهمت." },
                new string[] { "I'm coming now.", "Now I understand." });

            AddArabic(entries, "الصراحة", "honestly", "Egyptian/MSA; 'frankly speaking'.",
                new string[] { "الصراحة، مش عاجبني.", "الصراحة، أنت صح." },
                new string[] { "Honestly, I don't like it.", "Honestly, you're right." });

            AddArabic(entries, "شو رايك", "what do you think?", "Levantine; asks for an opinion.",
                new string[] { "شو رايك نروح؟", "شو رايك بهذا؟" },
                new string[] { "What do you think, shall we go?", "What do you think of this?" });

            AddArabic(entries, "على راسي", "at your service", "Levantine; literally 'on my head', willing to oblige.",
                new string[] { "تكرم، على راسي.", "طلبك على راسي." },
                new string[] { "You're welcome, at your service.", "Consider your request done." });

            AddArabic(entries, "من عيني", "with pleasure", "Levantine; literally 'from my eye', eager agreement.",
                new string[] { "من عيني، حاضر.", "من عيني، يا صاحبي." },
                new string[] { "With pleasure, I'll do it.", "With pleasure, my friend." });

            AddArabic(entries, "الله يعطيك العافية", "thanks for your effort", "Levantine/Gulf/MSA; appreciation for someone's work.",
                new string[] { "الله يعطيك العافية يا خي.", "الله يعطيك العافية على الشغل." },
                new string[] { "Thanks for your effort, brother.", "Thanks for your effort on the work." });

            AddArabic(entries, "بلا مؤاخذة", "excuse me", "Levantine; polite interruption.",
                new string[] { "بلا مؤاخذة، ممكن أعدي؟", "بلا مؤاخذة، أنا مش قاصد." },
                new string[] { "Excuse me, may I pass?", "Excuse me, I didn't mean it." });

            AddArabic(entries, "ما تزعل", "don't be upset", "Levantine; reassurance.",
                new string[] { "ما تزعل، كل شيء تمام.", "ما تزعل مني." },
                new string[] { "Don't be upset, everything is fine.", "Don't be upset with me." });

            AddArabic(entries, "تكرم", "you're welcome", "Levantine; also 'as you wish'.",
                new string[] { "تكرم، هذا واجب.", "تكرم عينك." },
                new string[] { "You're welcome, it's my duty.", "As you wish." });

            AddArabic(entries, "يلا بينا", "let's go", "Levantine/Egyptian; encouragement to leave.",
                new string[] { "يلا بينا نروح.", "يلا بينا، متأخرناش." },
                new string[] { "Let's go.", "Let's go, we're not late." });

            AddArabic(entries, "شو صار", "what happened?", "Levantine; asks what occurred.",
                new string[] { "شو صار هون؟", "شو صار معك؟" },
                new string[] { "What happened here?", "What happened to you?" });

            AddArabic(entries, "ما في مشكلة", "no problem", "Levantine; reassurance.",
                new string[] { "ما في مشكلة، أنا معك.", "ما في مشكلة، خلصنا." },
                new string[] { "No problem, I'm with you.", "No problem, we're done." });

            AddArabic(entries, "وش رايك", "what do you think?", "Gulf; asks for an opinion.",
                new string[] { "وش رايك نطلع؟", "وش رايك بالمشروع؟" },
                new string[] { "What do you think, shall we go out?", "What do you think of the project?" });

            AddArabic(entries, "ما عليك", "don't worry", "Gulf; also 'leave it to me'.",
                new string[] { "ما عليك، أنا أتكفل.", "ما عليك من كلام الناس." },
                new string[] { "Don't worry, I'll handle it.", "Don't worry about what people say." });

            AddArabic(entries, "أبشر", "consider it done", "Gulf; willing agreement, literally 'good news'.",
                new string[] { "أبشر، راح أساعدك.", "أبشر يا طويل العمر." },
                new string[] { "Consider it done, I'll help you.", "Consider it done, sir." });

            AddArabic(entries, "عساك بخير", "hope you're well", "Gulf/Yemeni; concerned greeting.",
                new string[] { "عساك بخير يا خوي؟", "عساك بخير بعد السفر." },
                new string[] { "Hope you're well, brother?", "Hope you're well after the trip." });

            AddArabic(entries, "يا طويل العمر", "sir", "Gulf; respectful address, literally 'O long-lived one'.",
                new string[] { "يا طويل العمر، ممكن سؤال؟", "شكرًا يا طويل العمر." },
                new string[] { "Sir, may I ask a question?", "Thank you, sir." });

            AddArabic(entries, "شنو الأخبار", "what's up?", "Moroccan; literally 'what's the news?'.",
                new string[] { "شنو الأخبار يا خويا؟", "شنو الأخبار اليوم؟" },
                new string[] { "What's up, brother?", "What's up today?" });

            AddArabic(entries, "كيداير", "how are you?", "Moroccan; informal greeting.",
                new string[] { "كيداير يا صاحبي؟", "كيداير اليوم؟" },
                new string[] { "How are you, my friend?", "How are you today?" });

            AddArabic(entries, "ماشي مشكل", "no problem", "Moroccan; reassurance.",
                new string[] { "ماشي مشكل، نلتقي غدًا.", "ماشي مشكل، أنا معك." },
                new string[] { "No problem, we'll meet tomorrow.", "No problem, I'm with you." });

            AddArabic(entries, "دير بالك", "watch out", "Moroccan; also 'take care'.",
                new string[] { "دير بالك على راسك.", "دير بالك من الطريق." },
                new string[] { "Take care of yourself.", "Watch out for the road." });

            AddArabic(entries, "الله يخليك", "please", "Moroccan/MSA; polite request, literally 'may God keep you'.",
                new string[] { "الله يخليك، ساعدني.", "الله يخليك، ما تنساني." },
                new string[] { "Please, help me.", "Please, don't forget me." });

            AddArabic(entries, "بالصحة", "enjoy your meal", "Moroccan; said before eating.",
                new string[] { "بالصحة والراحة.", "بالصحة، كول." },
                new string[] { "Enjoy your meal.", "Enjoy, eat." });

            AddArabic(entries, "لو سمحت", "excuse me", "Egyptian/MSA; polite request or interruption.",
                new string[] { "لو سمحت، ممكن أعدي؟", "لو سمحت، ساعدني." },
                new string[] { "Excuse me, may I pass?", "Please, help me." });

            AddArabic(entries, "روح انقلع", "go fuck yourself", "Vulgar Egyptian/Levantine dismissal.",
                new string[] { "روح انقلع، مش عايز أشوفك.", "روح انقلع من هنا." },
                new string[] { "Go fuck yourself, I don't want to see you.", "Get the fuck out of here." });

            AddArabic(entries, "أنا تعبان", "I'm tired", "Egyptian; also 'I'm worn out'.",
                new string[] { "أنا تعبان، عايز أنام.", "أنا تعبان من الشغل." },
                new string[] { "I'm tired, I want to sleep.", "I'm tired from work." });

            AddArabic(entries, "بكام", "how much?", "Egyptian; asks a price.",
                new string[] { "بكام ده؟", "بكام الكيلو؟" },
                new string[] { "How much is this?", "How much is a kilo?" });

            AddArabic(entries, "لا حول ولا قوة إلا بالله", "there is no strength except with God", "MSA; said at hardship or resignation.",
                new string[] { "لا حول ولا قوة إلا بالله، شنو صار؟", "لا حول ولا قوة إلا بالله." },
                new string[] { "There is no strength except with God, what happened?", "There is no strength except with God." });

            AddArabic(entries, "سبحان الله", "glory be to God", "MSA; admiration or surprise.",
                new string[] { "سبحان الله، شو حلو.", "سبحان الله على كل حال." },
                new string[] { "Glory be to God, how beautiful.", "Glory be to God in any case." });

            AddArabic(entries, "الله أكبر", "God is greatest", "MSA; exclamation of joy or shock.",
                new string[] { "الله أكبر، الفريق فاز.", "الله أكبر، هذا خير." },
                new string[] { "God is greatest, the team won.", "God is greatest, this is good." });

            AddArabic(entries, "بالتوفيق", "good luck", "MSA; wished before an effort.",
                new string[] { "بالتوفيق في الامتحان.", "بالتوفيق في شغلك." },
                new string[] { "Good luck on the exam.", "Good luck with your work." });

            AddArabic(entries, "طيب", "okay", "Egyptian/MSA; agreement or 'fine'.",
                new string[] { "طيب، نلتقي غدًا.", "طيب، أنا موافق." },
                new string[] { "Okay, we'll meet tomorrow.", "Okay, I agree." });

            return entries;
        }

        private static void AddArabic(List<ArabicColloquialEntry> entries, string arabic, string english,
            string note, string[] exampleArabic, string[] exampleEnglish, params Lahja[] lahjaat)
        {
            entries.Add(new ArabicColloquialEntry(arabic, english, note, exampleArabic, exampleEnglish, lahjaat));
        }

        private static HashSet<string> BuildArabicMarkers()
        {
            string[] markers = new string[]
            {
                "يلا", "معلش", "كده", "خالص", "حبيبي", "عرص", "زبالة", "شو", "كيفك",
                "وين", "رايح", "خلص", "منيح", "كتير", "والله", "زلمة", "بدك", "وش",
                "خلاص", "زين", "وايد", "شلونك", "لاباس", "بزاف", "دابا", "واخا", "صافي",
                "كيفاش", "شنو", "مزيان", "خويا", "سلام", "اللعنة", "تبًا", "اخرس", "حمار",
                "غبي", "تمام", "طيب", "أوكي", "اوكي", "ماشي", "درب", "وليد", "إزيك", "باشا",
                "فارقة", "علينا", "بجد", "جامد", "دلوقتي", "الصراحة", "رايك", "راسي", "مؤاخذة",
                "تزعل", "تكرم", "بينا", "عساك", "طويل", "كيداير", "يخليك", "بالصحة", "سبحان",
                "أكبر", "بالتوفيق", "تعبان", "بكام", "انقلع", "سمحت"
            };
            return new HashSet<string>(markers, StringComparer.Ordinal);
        }

        private static HashSet<string> BuildMarkers()
        {
            string[] markers = new string[]
            {
                "fuck", "fucking", "fucked", "shit", "bullshit", "bitch", "asshole", "bastard",
                "damn", "crap", "dude", "gonna", "wanna", "ain't", "y'all",
                "mames", "manches", "chingada", "chingon", "chingón", "cabron", "cabrón",
                "pendejo", "culero", "verga", "joder", "coño", "hostia", "mierda", "puta",
                "follar", "perra", "mamon", "mamón", "ojete", "pinche", "chido", "chale",
                "orale", "órale", "nel", "simon", "simón", "guey", "güey", "pedo", "onda",
                "huevo", "madre", "chamba", "lana", "varo", "gacho", "fresa", "neta", "mande",
                "ándale", "andale", "gilipollas", "capullo", "tio", "tío", "tia", "tía", "mola",
                "guay", "curro", "pasta", "flipo", "venga", "fuerte", "chorrada", "pringado",
                "cutre", "coña", "acojonante", "volada", "chinga", "mogollón", "chupado", "morro",
                "merde", "putain", "scheisse", "scheiße", "verdammt", "cazzo", "merda",
                "porra", "caralho", "kurwa", "chuj", "amk", "sik", "anjing", "kontol", "dit"
            };
            HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string marker in markers) set.Add(Fold(marker));
            return set;
        }

        private static Dictionary<string, HashSet<string>> BuildStopwords()
        {
            Dictionary<string, HashSet<string>> map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            map["es"] = Set("el la los las de del que y en un una es son no con para por se su al como mas más pero esto esta este muy ya hay me te lo le les nos nosotros vosotros ellos ellas yo tú él ella ser estar hacer hecho dice puede debe cuando donde porque desde durante antes después todavía siempre nunca nada nadie cada cual cuales nuestro vuestro también tampoco sino entre sobre bajo hacia hasta sin qué quién cómo cuándo dónde cuál");
            map["en"] = Set("the and is are was were to of in that it you i don't dont fuck shit bitch ass damn hello this with for have not no yes what movie she he they we");
            map["fr"] = Set("le la les des est et pas que qui dans pour avec je tu il elle putain merde un une du de en ce cette ces cet sur au aux mon ma mes ton ta tes son sa ses notre votre leur nous vous ils elles on mais ou où donc or ni car très tout tous toute bien aussi plus moins sans sous entre vers chez être avoir faire fait dit peut doit comme quand comment pourquoi parce depuis pendant avant après encore toujours jamais rien personne chaque quel quelle quels quelles lui moi toi soi y ne si oui non dont");
            map["de"] = Set("der die das und ist nicht ein eine mit fur für auf ich du er sie scheisse verdammt wie geht es dir mir dich sich haben sind war werden kann muss soll will auch aber oder noch nur schon sehr mehr wenn weil dass dieser diese dieses mein dein sein ihre unser euer kein keine zum zur vom beim im am an dem den des");
            map["pt"] = Set("o a os as de que e em um uma nao não com para por voce você merda porra como está bom dia boa noite obrigado obrigada vocês nós eles elas isso isto aqui ali sim mas ou quando onde porque muito bem também pelo pela dos das nos nas");
            map["it"] = Set("il la gli le di che e in un una non con per sono cazzo merda come stai sei buongiorno buonasera grazie prego ciao io tu lui lei noi voi loro questo questa quello quella del della nel nella al alla è");
            map["nl"] = Set("de het een en is niet van in op ik je dat kut shit");
            map["pl"] = Set("i w na z do nie jest to sie się kurwa chuj");
            map["tr"] = Set("bir ve bu da de icin için degil değil amk sik");
            map["id"] = Set("yang dan di ke dari ini itu tidak aku kamu anjing kontol");
            map["vi"] = Set("va và la là cua của khong không toi tôi ban bạn dit địt");
            map["ro"] = Set("si și este nu cu de la in în dracului");
            map["ca"] = Set("el la els les de del que i en un una és són no amb per seu al com més però aquest aquesta molt ja hi em et ho li ens");
            map["gl"] = Set("o a os as de do da que e en un unha é son non con por se seu ao como mais pero este esta moi xa hai me te lle nos");
            map["sv"] = Set("och att det en ett är inte som på för med av den de har jag du han hon vi ni sig till från om men eller så kan ska");
            map["da"] = Set("og at det en et er ikke som på for med af den de har jeg du han hun vi sig til fra om men eller så kan skal");
            map["no"] = Set("og at det en et er ikke som på for med av den de har jeg du han hun vi dere seg til fra om men eller så kan skal");
            map["fi"] = Set("ja on ei se että hän me te he ovat oli mutta tai kun niin kuin myös vain jo nyt minä sinä");
            map["hu"] = Set("a az egy és nem hogy van volt lesz meg de vagy ha már még csak mint ki mi ez azt");
            map["cs"] = Set("a je se na v že to s z do o k i jako ale nebo když už jen tak aby byl byla");
            map["sk"] = Set("a je sa na v že to s z do o k aj ako alebo keď už len tak aby bol bola");
            map["hr"] = Set("je i u se na da su za od s kao ali ili ako kada već samo tako biti bio bila");
            map["sr"] = Set("je i u se na da su za od sa kao ali ili ako kada već samo tako biti bio bila");
            map["bs"] = Set("je i u se na da su za od s kao ali ili ako kada već samo tako biti bio bila");
            map["sl"] = Set("je in v se na da so za od s kot ali če že samo tako biti bil bila");
            map["et"] = Set("ja on ei see et mis ma ei aga või kui juba ainult nii olema oli");
            map["lt"] = Set("ir yra ne kad į iš su bet arba kaip jau tik taip būti buvo");
            map["lv"] = Set("un ir ne uz no ar bet vai kā jau tikai tā būt bija");
            map["is"] = Set("og er ekki sem á fyrir með af það hann hún við þið þeir til frá um en eða svo");
            map["af"] = Set("die en is nie van in op ek jy hy sy ons julle hulle dit wat met vir maar of");
            map["sw"] = Set("na ya ni kwa katika kuwa hii hiyo yeye sisi nyinyi wao lakini au kama");
            map["tl"] = Set("ang ng sa mga ay at hindi ito iyon siya kami tayo kayo sila pero o kung");
            map["ms"] = Set("yang dan di ke dari ini itu tidak aku kamu dia kami kita mereka tetapi atau");
            map["eo"] = Set("la kaj estas ne en de al mi vi li ŝi ni ili sed aŭ ke kiel");
            map["la"] = Set("et in est non ad de cum quod qui quae sed aut ut si");
            return map;
        }

        private static HashSet<string> Set(string words)
        {
            HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string word in words.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                set.Add(Fold(word));
            return set;
        }
    }
}
