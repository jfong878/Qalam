// Qalam - local open-corpus concordance.
//
// Packs are plain TSV files in %LOCALAPPDATA%\Qalam\concordance. Each pack is
// directional: a row is a source sentence and its aligned target sentence.
// The builder streams TMX files (including gzip-compressed OPUS downloads) and
// can write both directions. No network access is used at query time.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;

namespace Qalam
{
    internal sealed class ConcordancePair
    {
        public string Source;
        public string Target;
        public string SourceNorm;
        public string TargetNorm;
        public int SourceWords;
        public string Corpus;
        public string License;
    }

    internal sealed class ConcordanceHit
    {
        public ConcordancePair Pair;
        public double Score;
        public bool Exact;
        // 0..1 spoken-register estimate; see Register.cs.
        public double Register;
    }

    internal static class ConcordanceText
    {
        private static readonly char[] WordSeparators = new char[] { ' ', '\t', '\r', '\n' };

        public static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            string text = WebUtility.HtmlDecode(value);
            StringBuilder builder = new StringBuilder(text.Length);
            bool pendingSpace = false;
            foreach (char c in text)
            {
                if (c == '\r' || c == '\n' || c == '\t' || c == '\u00a0' || c == '\u200b')
                {
                    pendingSpace = true;
                    continue;
                }
                if (c == '\u266a' || c == '\u266b') continue;
                if (char.IsControl(c))
                {
                    pendingSpace = true;
                    continue;
                }
                if (pendingSpace && builder.Length > 0 && builder[builder.Length - 1] != ' ')
                    builder.Append(' ');
                pendingSpace = false;
                builder.Append(c);
            }

            string result = builder.ToString().Trim();
            // Subtitle dialogue often starts with a dash. It is punctuation,
            // not part of the sentence being illustrated.
            while (result.StartsWith("- ", StringComparison.Ordinal) ||
                   result.StartsWith("\u2013 ", StringComparison.Ordinal) ||
                   result.StartsWith("\u2014 ", StringComparison.Ordinal))
            {
                result = result.Substring(2).TrimStart();
            }
            return result.Length == 0 ? null : result;
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            string text = ArabicText.StripDiacritics(WebUtility.HtmlDecode(value));

            bool nonAscii = false;
            foreach (char c in text)
            {
                if (c > 0x7F)
                {
                    nonAscii = true;
                    break;
                }
            }
            if (nonAscii) text = text.Normalize(NormalizationForm.FormD);

            StringBuilder builder = new StringBuilder(text.Length);
            bool pendingSpace = false;
            foreach (char c in text)
            {
                // Combining marks start at U+0300. Skipping the category lookup
                // for ASCII is a measurable part of loading a large pack.
                if (c >= 0x0300)
                {
                    UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                    if (category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark)
                        continue;
                }

                if (char.IsLetterOrDigit(c))
                {
                    if (pendingSpace && builder.Length > 0) builder.Append(' ');
                    builder.Append(char.ToLowerInvariant(c));
                    pendingSpace = false;
                }
                else
                {
                    pendingSpace = true;
                }
            }
            return builder.ToString();
        }

        public static List<string> Tokens(string normalized)
        {
            List<string> tokens = new List<string>();
            if (string.IsNullOrEmpty(normalized)) return tokens;
            foreach (string token in normalized.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries))
                if (token.Length >= 2) tokens.Add(token);
            return tokens;
        }

        public static int WordCount(string value)
        {
            return WordCountClean(Clean(value));
        }

        public static int WordCountClean(string clean)
        {
            if (string.IsNullOrEmpty(clean)) return 0;
            return clean.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        public static bool HasLetter(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char c in value)
                if (char.IsLetter(c)) return true;
            return false;
        }

        public static bool HasToken(string normalized, string token)
        {
            if (string.IsNullOrEmpty(normalized) || string.IsNullOrEmpty(token)) return false;
            if (ArabicText.ContainsArabic(normalized) || ArabicText.ContainsArabic(token))
                return normalized.IndexOf(token, StringComparison.Ordinal) >= 0;
            if (string.Equals(normalized, token, StringComparison.Ordinal)) return true;
            if (normalized.StartsWith(token + " ", StringComparison.Ordinal)) return true;
            if (normalized.EndsWith(" " + token, StringComparison.Ordinal)) return true;
            return normalized.IndexOf(" " + token + " ", StringComparison.Ordinal) >= 0;
        }

        public static string Lang(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            string lang = value.Trim().ToLowerInvariant();
            int cut = lang.IndexOfAny(new char[] { '-', '_' });
            if (cut > 0) lang = lang.Substring(0, cut);
            return lang.Length >= 2 && lang.Length <= 3 ? lang : null;
        }

        public static string ArabicStem(string token)
        {
            if (string.IsNullOrEmpty(token) || !ArabicText.ContainsArabic(token)) return token;
            string value = token;
            // Conjunction and preposition+article prefixes are the common
            // reason a query word and an inflected corpus word differ.
            if (value.Length > 3 && (value[0] == '\u0648' || value[0] == '\u0641'))
                value = value.Substring(1);
            if (value.Length > 3 &&
                (value.StartsWith("\u0628\u0627\u0644", StringComparison.Ordinal) ||
                 value.StartsWith("\u0643\u0627\u0644", StringComparison.Ordinal) ||
                 value.StartsWith("\u0644\u0627\u0644", StringComparison.Ordinal)))
                value = value.Substring(3);
            else if (value.Length > 3 && value.StartsWith("\u0644\u0644", StringComparison.Ordinal))
                value = value.Substring(2);
            else if (value.Length > 3 && value.StartsWith("\u0627\u0644", StringComparison.Ordinal))
                value = value.Substring(2);
            return value.Length >= 2 ? value : token;
        }

        public static string Join(string separator, List<string> values)
        {
            if (values == null || values.Count == 0) return "";
            return string.Join(separator, values.ToArray());
        }
    }

    internal sealed class ConcordancePack : IConcordancePack
    {
        public string SourceLang { get; set; }
        public string TargetLang { get; set; }
        public string FilePath { get; set; }
        public string Corpus { get; set; }
        public string License { get; set; }
        public readonly List<ConcordancePair> Pairs = new List<ConcordancePair>();

        public int PairCount { get { return Pairs.Count; } }
        public readonly Dictionary<string, List<int>> Index =
            new Dictionary<string, List<int>>(StringComparer.Ordinal);

        public void Add(ConcordancePair pair)
        {
            int id = Pairs.Count;
            Pairs.Add(pair);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string token in ConcordanceText.Tokens(pair.SourceNorm))
            {
                string key = ConcordanceText.ArabicStem(token);
                if (!seen.Add(key)) continue;
                List<int> ids;
                if (!Index.TryGetValue(key, out ids))
                {
                    ids = new List<int>();
                    Index[key] = ids;
                }
                ids.Add(id);
            }
        }

        public List<ConcordanceHit> Search(string queryNorm, List<string> queryTokens, List<string> hintTokens, int max)
        {
            List<ConcordanceHit> hits = new List<ConcordanceHit>();
            if (queryTokens.Count == 0 || max <= 0) return hits;

            List<int> smallest = null;
            int present = 0;
            foreach (string token in queryTokens)
            {
                List<int> ids;
                if (!Index.TryGetValue(token, out ids) || ids.Count == 0) continue;
                present++;
                if (smallest == null || ids.Count < smallest.Count) smallest = ids;
            }
            if (smallest == null) return hits;

            int threshold = queryTokens.Count <= 3
                ? queryTokens.Count
                : (int)Math.Ceiling(queryTokens.Count * 0.6);
            if (present < threshold) return hits;

            int inspected = 0;
            int candidateLimit = Math.Max(4000, Math.Min(smallest.Count, 25000));
            foreach (int id in smallest)
            {
                if (++inspected > candidateLimit) break;
                ConcordancePair pair = Pairs[id];

                int matched = 0;
                foreach (string token in queryTokens)
                    if (ConcordanceText.HasToken(pair.SourceNorm, token)) matched++;
                if (matched < threshold) continue;

                bool exact = queryNorm.Length >= 3 &&
                             pair.SourceNorm.IndexOf(queryNorm, StringComparison.Ordinal) >= 0;
                // A two- or three-word query should not match on one common word
                // merely because the other word is absent from this pack.
                if (!exact && queryTokens.Count <= 3 && matched < queryTokens.Count) continue;

                double score = matched * 100.0;
                if (exact) score += 500.0;

                int words = pair.SourceWords;
                if (words >= 4 && words <= 18) score += 30.0;
                else score -= Math.Abs(words - 7) * 2.0;
                if (pair.Source.Length > 160) score -= 10.0;

                if (hintTokens != null && hintTokens.Count > 0)
                {
                    // Target normalization is only needed for the candidates
                    // that survive the source-side filter.
                    string targetNorm = pair.TargetNorm;
                    if (targetNorm == null)
                    {
                        targetNorm = ConcordanceText.Normalize(pair.Target);
                        pair.TargetNorm = targetNorm;
                    }
                    int hintHits = 0;
                    foreach (string token in hintTokens)
                    {
                        if (ConcordanceText.HasToken(targetNorm, token)) hintHits++;
                        if (hintHits >= 4) break;
                    }
                    score += hintHits * 12.0;
                }

                ConcordanceHit hit = new ConcordanceHit();
                hit.Pair = pair;
                hit.Score = score;
                hit.Exact = exact;
                hits.Add(hit);
            }

            hits.Sort(delegate(ConcordanceHit a, ConcordanceHit b)
            {
                int compare = b.Score.CompareTo(a.Score);
                if (compare != 0) return compare;
                compare = a.Pair.Source.Length.CompareTo(b.Pair.Source.Length);
                if (compare != 0) return compare;
                return string.Compare(a.Pair.Source, b.Pair.Source, StringComparison.Ordinal);
            });

            Register.Rerank(hits, max, SourceLang, TargetLang, queryTokens);

            if (hits.Count > max) hits.RemoveRange(max, hits.Count - max);
            return hits;
        }

    }

    internal sealed class ConcordanceStore
    {
        private const int MaxPairsPerPack = 3000000;
        private static readonly object SharedGate = new object();
        private static ConcordanceStore _shared;

        private sealed class PackInfo
        {
            public string Path;
            public string Source;
            public string Target;
            public string Corpus;
            public string License;
        }

        private readonly object _gate = new object();
        private readonly List<string> _directories = new List<string>();
        private readonly List<IConcordancePack> _packs = new List<IConcordancePack>();
        private readonly HashSet<string> _loadedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private List<PackInfo> _infos;
        private bool _allLoaded;

        public static ConcordanceStore Shared
        {
            get
            {
                lock (SharedGate)
                {
                    if (_shared == null)
                    {
                        List<string> directories = new List<string>();
                        directories.Add(Path.Combine(Paths.DataDir, "concordance"));
                        string beside = Path.Combine(Paths.AppDir, "concordance");
                        if (!string.Equals(beside, directories[0], StringComparison.OrdinalIgnoreCase))
                            directories.Add(beside);
                        // Packs load lazily per language pair on the first
                        // query, so starting the tray app stays immediate.
                        _shared = new ConcordanceStore(directories.ToArray());
                    }
                    return _shared;
                }
            }
        }

        public ConcordanceStore(params string[] directories)
        {
            if (directories == null) return;
            foreach (string directory in directories)
            {
                if (string.IsNullOrEmpty(directory)) continue;
                if (!_directories.Contains(directory)) _directories.Add(directory);
            }
        }

        public int PairCount
        {
            get { lock (_gate) { return CountPairs(); } }
        }

        public int PackCount { get { lock (_gate) { return _packs.Count; } } }

        private int CountPairs()
        {
            int count = 0;
            foreach (IConcordancePack pack in _packs) count += pack.PairCount;
            return count;
        }

        public void Load()
        {
            lock (_gate)
            {
                if (_allLoaded) return;
                Stopwatch watch = Stopwatch.StartNew();
                Scan();
                int before = CountPairs();
                foreach (PackInfo info in _infos)
                {
                    if (_loadedPaths.Contains(info.Path)) continue;
                    try { LoadPack(info); }
                    catch (Exception ex)
                    {
                        Logger.Write("concordance: load " + Path.GetFileName(info.Path) + ": " + ex.Message);
                    }
                }
                _allLoaded = true;
                watch.Stop();
                Logger.Write("concordance: packs=" + _packs.Count + " pairs=" + (CountPairs() - before) +
                             " ms=" + watch.ElapsedMilliseconds + " dirs=" + _directories.Count + " all=true");
            }
        }

        private void Scan()
        {
            if (_infos != null) return;
            _infos = new List<PackInfo>();
            foreach (string directory in _directories)
            {
                if (!Directory.Exists(directory)) continue;
                string[] files;
                try { files = Directory.GetFiles(directory, "*.tsv"); }
                catch (Exception ex)
                {
                    Logger.Write("concordance: list " + directory + ": " + ex.Message);
                    continue;
                }
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                {
                    try
                    {
                        PackInfo info = ReadHeader(file);
                        if (info != null) _infos.Add(info);
                    }
                    catch (Exception ex)
                    {
                        Logger.Write("concordance: header " + Path.GetFileName(file) + ": " + ex.Message);
                    }
                }
            }
        }

        private void EnsurePair(string source, string target)
        {
            if (_allLoaded) return;
            Scan();
            Stopwatch watch = Stopwatch.StartNew();
            int before = CountPairs();
            // OPUS uses "zh" while the translator and UI distinguish zh-CN and
            // zh-TW. They share the same local Chinese pack when present.
            string packTarget = target != null && target.StartsWith("zh-", StringComparison.OrdinalIgnoreCase)
                ? "zh" : target;
            foreach (PackInfo info in _infos)
            {
                if (!string.Equals(info.Source, source, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(info.Target, packTarget, StringComparison.OrdinalIgnoreCase)) continue;
                if (_loadedPaths.Contains(info.Path)) continue;
                try { LoadPack(info); }
                catch (Exception ex)
                {
                    Logger.Write("concordance: load " + Path.GetFileName(info.Path) + ": " + ex.Message);
                }
            }
            watch.Stop();
            int loaded = CountPairs() - before;
            if (loaded != 0)
                Logger.Write("concordance: pair=" + source + "-" + target + " packs=" + _packs.Count +
                             " loaded=" + loaded + " ms=" + watch.ElapsedMilliseconds + " all=false");
        }

        private PackInfo ReadHeader(string path)
        {
            string stem = Path.GetFileNameWithoutExtension(path);
            string inferredSource = null;
            string inferredTarget = null;
            int dash = stem.IndexOf('-');
            if (dash > 0)
            {
                inferredSource = ConcordanceText.Lang(stem.Substring(0, dash));
                inferredTarget = ConcordanceText.Lang(stem.Substring(dash + 1));
            }

            string sourceLang = inferredSource;
            string targetLang = inferredTarget;
            string corpus = "Open corpus";
            string license = "";

            using (StreamReader header = new StreamReader(path, Encoding.UTF8, true))
            {
                string line;
                int headerLines = 0;
                while ((line = header.ReadLine()) != null && headerLines < 32)
                {
                    if (line.Length == 0) continue;
                    if (line[0] != '#')
                    {
                        headerLines++;
                        continue;
                    }
                    int tab = line.IndexOf('\t');
                    if (tab <= 0) continue;
                    string key = line.Substring(1, tab - 1).Trim().ToLowerInvariant();
                    string value = line.Substring(tab + 1).Trim();
                    if (key == "source") sourceLang = ConcordanceText.Lang(value);
                    else if (key == "target") targetLang = ConcordanceText.Lang(value);
                    else if (key == "corpus" && value.Length > 0) corpus = value;
                    else if (key == "license" && value.Length > 0) license = value;
                }
            }

            if (sourceLang == null || targetLang == null || sourceLang == targetLang) return null;

            PackInfo info = new PackInfo();
            info.Path = path;
            info.Source = sourceLang;
            info.Target = targetLang;
            info.Corpus = corpus;
            info.License = license;
            return info;
        }

        private void LoadPack(PackInfo info)
        {
            string path = info.Path;
            string sourceLang = info.Source;
            string targetLang = info.Target;
            string corpus = info.Corpus;
            string license = info.License;

            string indexPath = path + ".qidx";
            if (File.Exists(indexPath))
            {
                try
                {
                    DiskConcordancePack disk = new DiskConcordancePack(path, indexPath);
                    _loadedPaths.Add(path);
                    _packs.Add(disk);
                    Logger.Write("concordance: " + Path.GetFileName(path) + " qidx=" + disk.PairCount);
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Write("concordance: qidx " + Path.GetFileName(indexPath) + ": " + ex.Message);
                }
            }

            ConcordancePack pack = new ConcordancePack();
            pack.SourceLang = sourceLang;
            pack.TargetLang = targetLang;
            pack.FilePath = path;
            pack.Corpus = corpus;
            pack.License = license;

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            int skipped = 0;
            foreach (string line in File.ReadLines(path, Encoding.UTF8))
            {
                if (line.Length == 0) continue;
                int tab = line.IndexOf('\t');
                if (line[0] == '#')
                {
                    // A data sentence can begin with a hashtag. Only known
                    // header keys are metadata; everything else is a row.
                    if (tab <= 0) continue;
                    string key = line.Substring(1, tab - 1).Trim().ToLowerInvariant();
                    if (key == "source" || key == "target" || key == "corpus" ||
                        key == "license" || key == "url" || key == "built" ||
                        key == "pairs" || key == "qalam-concordance")
                        continue;
                }
                if (tab <= 0) { skipped++; continue; }

                string source = ConcordanceText.Clean(line.Substring(0, tab));
                string target = ConcordanceText.Clean(line.Substring(tab + 1));
                if (!Usable(source, target)) { skipped++; continue; }

                string sourceNorm = ConcordanceText.Normalize(source);
                if (sourceNorm.Length < 4 || !seen.Add(sourceNorm)) { skipped++; continue; }

                ConcordancePair pair = new ConcordancePair();
                pair.Source = source;
                pair.Target = target;
                pair.SourceNorm = sourceNorm;
                pair.SourceWords = ConcordanceText.WordCountClean(source);
                pair.TargetNorm = null;
                pair.Corpus = corpus;
                pair.License = license;
                pack.Add(pair);

                if (pack.Pairs.Count >= MaxPairsPerPack) break;
            }

            _loadedPaths.Add(path);
            if (pack.Pairs.Count == 0) return;
            _packs.Add(pack);
            if (skipped > 0)
                Logger.Write("concordance: " + Path.GetFileName(path) + " loaded=" + pack.Pairs.Count +
                             " skipped=" + skipped);
        }

        private static bool Usable(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return false;
            if (!ConcordanceText.HasLetter(source) || !ConcordanceText.HasLetter(target)) return false;
            if (source.Length < 4 || target.Length < 2) return false;
            if (source.Length > 400 || target.Length > 500) return false;
            // Subtitle timing rows are not examples.
            if (source.IndexOf("-->", StringComparison.Ordinal) >= 0 ||
                target.IndexOf("-->", StringComparison.Ordinal) >= 0) return false;
            if (source.IndexOf("00:", StringComparison.Ordinal) >= 0 ||
                target.IndexOf("00:", StringComparison.Ordinal) >= 0) return false;
            int sourceWords = ConcordanceText.WordCountClean(source);
            int targetWords = ConcordanceText.WordCountClean(target);
            if (sourceWords == 0 || sourceWords > 40) return false;
            if (targetWords == 0 || targetWords > 48) return false;
            return true;
        }

        public List<ExamplePair> Search(string text, string sourceLang, string targetLang, int max, string hint)
        {
            string source = ConcordanceText.Lang(sourceLang);
            string target = ConcordanceText.Lang(targetLang);
            List<ExamplePair> results = new List<ExamplePair>();
            if (source == null || target == null || source == target || max <= 0) return results;

            IConcordancePack[] packs;
            lock (_gate)
            {
                EnsurePair(source, target);
                packs = _packs.ToArray();
            }

            string queryNorm = ConcordanceText.Normalize(text);
            List<string> queryTokens = DistinctStems(ConcordanceText.Tokens(queryNorm));
            if (queryTokens.Count == 0) return results;

            List<string> hintTokens = new List<string>();
            if (!string.IsNullOrEmpty(hint))
            {
                foreach (string token in DistinctStems(ConcordanceText.Tokens(ConcordanceText.Normalize(hint))))
                    if (token.Length >= 3 && !HintStopwords.Contains(token)) hintTokens.Add(token);
            }

            Dictionary<string, ConcordanceHit> best =
                new Dictionary<string, ConcordanceHit>(StringComparer.Ordinal);
            foreach (IConcordancePack pack in packs)
            {
                if (!string.Equals(pack.SourceLang, source, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(pack.TargetLang, target, StringComparison.OrdinalIgnoreCase)) continue;

                foreach (ConcordanceHit hit in pack.Search(queryNorm, queryTokens, hintTokens, max * 3))
                {
                    ConcordanceHit existing;
                    if (!best.TryGetValue(hit.Pair.SourceNorm, out existing) || hit.Score > existing.Score)
                        best[hit.Pair.SourceNorm] = hit;
                }
            }

            List<ConcordanceHit> ordered = new List<ConcordanceHit>(best.Values);
            ordered.Sort(delegate(ConcordanceHit a, ConcordanceHit b)
            {
                int compare = b.Score.CompareTo(a.Score);
                if (compare != 0) return compare;
                compare = a.Pair.Source.Length.CompareTo(b.Pair.Source.Length);
                if (compare != 0) return compare;
                return string.Compare(a.Pair.Source, b.Pair.Source, StringComparison.Ordinal);
            });

            foreach (ConcordanceHit hit in ordered)
            {
                if (results.Count >= max) break;
                ExamplePair example = new ExamplePair();
                example.Source = hit.Pair.Source;
                example.Target = hit.Pair.Target;
                example.Term = text;
                example.FromConcordance = true;
                example.Origin = hit.Pair.Corpus;
                example.License = hit.Pair.License;
                example.Register = hit.Register;
                results.Add(example);
            }
            return results;
        }

        private static List<string> DistinctStems(List<string> values)
        {
            List<string> unique = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values)
            {
                string stem = ConcordanceText.ArabicStem(value);
                if (seen.Add(stem)) unique.Add(stem);
            }
            return unique;
        }

        private static readonly HashSet<string> HintStopwords = new HashSet<string>(
            new string[]
            {
                "the", "and", "for", "with", "that", "this", "you", "are", "was", "have", "not",
                "but", "from", "they", "his", "her", "she", "him", "its", "our", "who", "what",
                "when", "where", "how", "why", "all", "can", "will", "just", "out", "get", "got",
                "one", "two", "too", "very", "into", "over", "then", "than", "now", "your",
                "el", "la", "los", "las", "un", "una", "unos", "unas", "de", "del", "al", "que",
                "y", "o", "en", "con", "por", "para", "es", "son", "era", "como", "pero", "mas",
                "si", "no", "se", "su", "sus", "le", "les", "lo", "me", "te", "nos", "os", "mi",
                "mis", "tu", "tus", "este", "esta", "esto", "estos", "estas", "eso", "esa", "esos",
                "esas", "yo", "ella", "ellos", "ellas", "usted", "ustedes", "muy", "ya", "tambien",
                "solo", "tan", "asi", "aqui", "alli", "hay", "tiene", "tienen", "ser", "estar",
                "tener", "hacer", "ir", "ver", "dar", "saber", "decir", "poder", "querer", "venir"
            },
            StringComparer.Ordinal);
    }

    internal static class ConcordanceBuilder
    {
        private sealed class Options
        {
            public string Input;
            public bool Tmx;
            public string From;
            public string To;
            public string OutDir;
            public string Corpus;
            public string License;
            public string Url;
            public int Limit = 20000;
            public bool Sample;
            public bool Both;
            public bool Index;
            public int MinWords = 2;
            public int MaxWords = 24;
            public int MaxChars = 220;
            public int Seed = 12345;
            public string Suffix = "";
            public readonly List<string> Contains = new List<string>();
        }

        private sealed class Collector
        {
            private readonly Options _options;
            private readonly List<ConcordancePair> _pairs = new List<ConcordancePair>();
            private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
            private readonly Random _random;
            private int _accepted;

            public Collector(Options options)
            {
                _options = options;
                _random = new Random(options.Seed);
            }

            public bool IsFull
            {
                get { return !_options.Sample && _pairs.Count >= _options.Limit; }
            }

            public List<ConcordancePair> Pairs { get { return _pairs; } }
            public int Accepted { get { return _accepted; } }

            public bool TryAdd(string source, string target)
            {
                source = ConcordanceText.Clean(source);
                target = ConcordanceText.Clean(target);
                if (!Good(source, target, _options)) return false;

                string sourceNorm = ConcordanceText.Normalize(source);
                if (sourceNorm.Length < 4 || !_seen.Add(sourceNorm)) return false;

                ConcordancePair pair = new ConcordancePair();
                pair.Source = source;
                pair.Target = target;
                pair.SourceNorm = sourceNorm;
                pair.TargetNorm = ConcordanceText.Normalize(target);
                _accepted++;

                if (_pairs.Count < _options.Limit)
                {
                    _pairs.Add(pair);
                    return true;
                }
                if (!_options.Sample) return false;

                // Reservoir sampling keeps a prefix download representative
                // instead of taking the first N lines of one subtitle.
                int replace = _random.Next(_accepted);
                if (replace < _pairs.Count) _pairs[replace] = pair;
                return true;
            }

            private static bool Good(string source, string target, Options options)
            {
                if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return false;
                if (!ConcordanceText.HasLetter(source) || !ConcordanceText.HasLetter(target)) return false;
                if (source.Length < 4 || target.Length < 2) return false;
                if (source.Length > options.MaxChars) return false;
                if (target.Length > options.MaxChars + 60) return false;

                int sourceWords = ConcordanceText.WordCount(source);
                int targetWords = ConcordanceText.WordCount(target);
                if (sourceWords < options.MinWords || sourceWords > options.MaxWords) return false;
                if (targetWords < 2 || targetWords > options.MaxWords + 8) return false;

                if (source.IndexOf("http://", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (source.IndexOf("https://", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (source.IndexOf("www.", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (source.IndexOf('<') >= 0 || source.IndexOf('>') >= 0) return false;
                if (target.IndexOf('<') >= 0 || target.IndexOf('>') >= 0) return false;
                if (source.IndexOf("-->", StringComparison.Ordinal) >= 0 ||
                    target.IndexOf("-->", StringComparison.Ordinal) >= 0) return false;
                if (source.IndexOf("00:", StringComparison.Ordinal) >= 0 ||
                    target.IndexOf("00:", StringComparison.Ordinal) >= 0) return false;

                // A line made only of punctuation or symbols is not an example.
                int letters = 0;
                foreach (char c in source) if (char.IsLetter(c)) letters++;
                if (letters < 3) return false;

                if (options.Contains.Count > 0)
                {
                    string sourceNorm = ConcordanceText.Normalize(source);
                    string targetNorm = ConcordanceText.Normalize(target);
                    bool found = false;
                    foreach (string term in options.Contains)
                    {
                        if (sourceNorm.IndexOf(term, StringComparison.Ordinal) >= 0 ||
                            targetNorm.IndexOf(term, StringComparison.Ordinal) >= 0)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found) return false;
                }
                return true;
            }
        }

        public static int Run(string[] args)
        {
            Options options = new Options();
            options.Input = Arg(args, "--tmx");
            options.Tmx = options.Input != null;
            if (options.Input == null)
            {
                options.Input = Arg(args, "--tsv");
                options.Tmx = false;
            }
            if (string.IsNullOrEmpty(options.Input) || !File.Exists(options.Input))
            {
                Console.WriteLine("concordance: input not found. Use --tmx FILE or --tsv FILE.");
                return 2;
            }

            options.From = ConcordanceText.Lang(Arg(args, "--from"));
            options.To = ConcordanceText.Lang(Arg(args, "--to"));
            options.OutDir = Arg(args, "--out");
            if (string.IsNullOrEmpty(options.OutDir)) options.OutDir = Path.Combine(Paths.DataDir, "concordance");
            options.Corpus = Arg(args, "--corpus");
            if (string.IsNullOrEmpty(options.Corpus)) options.Corpus = DefaultCorpusName(options.Input);
            options.License = Arg(args, "--license") ?? "";
            options.Url = Arg(args, "--url") ?? "";
            options.Limit = Math.Max(100, Math.Min(3000000, IntArg(args, "--limit", 20000)));
            options.MinWords = Math.Max(1, IntArg(args, "--min-words", 2));
            options.MaxWords = Math.Max(options.MinWords, IntArg(args, "--max-words", 24));
            options.MaxChars = Math.Max(40, IntArg(args, "--max-chars", 220));
            options.Seed = IntArg(args, "--seed", 12345);
            options.Sample = Has(args, "--sample");
            options.Both = Has(args, "--both");
            options.Index = Has(args, "--index");
            options.Suffix = CleanSuffix(Arg(args, "--suffix"));
            string contains = Arg(args, "--contains");
            if (!string.IsNullOrEmpty(contains))
            {
                foreach (string part in contains.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string term = ConcordanceText.Normalize(part);
                    if (term.Length > 0 && !options.Contains.Contains(term)) options.Contains.Add(term);
                }
            }

            Stopwatch watch = Stopwatch.StartNew();
            Collector collector = new Collector(options);
            bool truncated = false;
            int read = 0;

            try
            {
                if (options.Tmx) ReadTmx(options, collector, ref read, ref truncated);
                else ReadTsv(options, collector, ref read);
            }
            catch (Exception ex)
            {
                WriteLog(options, "failed: " + ex.Message, 0, read, truncated, watch);
                Console.WriteLine("concordance: " + ex.Message);
                return 1;
            }

            if (options.From == null || options.To == null)
            {
                WriteLog(options, "failed: could not determine the language pair; pass --from and --to.",
                         0, read, truncated, watch);
                Console.WriteLine("concordance: pass --from and --to (for example --from en --to es).");
                return 2;
            }
            if (options.From == options.To)
            {
                WriteLog(options, "failed: --from and --to are the same language.", 0, read, truncated, watch);
                return 2;
            }

            List<ConcordancePair> pairs = collector.Pairs;
            if (pairs.Count == 0)
            {
                WriteLog(options, "no usable pairs after filtering.", 0, read, truncated, watch);
                Console.WriteLine("concordance: no usable pairs after filtering.");
                return 1;
            }

            Directory.CreateDirectory(options.OutDir);
            string forward = WritePack(options.OutDir, options.From, options.To, options.Corpus,
                                       options.License, options.Url, pairs, false, options.Suffix);
            string forwardIndex = null;
            if (options.Index)
                forwardIndex = ConcordanceDiskIndex.Write(forward, pairs, false, options.From,
                                                          options.To, options.Corpus, options.License);
            string reverse = null;
            string reverseIndex = null;
            if (options.Both)
            {
                reverse = WritePack(options.OutDir, options.To, options.From, options.Corpus,
                                    options.License, options.Url, pairs, true, options.Suffix);
                if (options.Index)
                    reverseIndex = ConcordanceDiskIndex.Write(reverse, pairs, true, options.To,
                                                              options.From, options.Corpus, options.License);
            }

            watch.Stop();
            string summary = "corpus=" + options.Corpus + " from=" + options.From + " to=" + options.To +
                             " read=" + read + " accepted=" + collector.Accepted + " written=" + pairs.Count +
                             " truncated=" + truncated + " ms=" + watch.ElapsedMilliseconds;
            WriteLog(options, summary, pairs.Count, read, truncated, watch);
            Console.WriteLine("concordance: " + summary);
            Console.WriteLine("concordance: " + forward);
            if (forwardIndex != null) Console.WriteLine("concordance: " + forwardIndex);
            if (reverse != null) Console.WriteLine("concordance: " + reverse);
            if (reverseIndex != null) Console.WriteLine("concordance: " + reverseIndex);
            return 0;
        }

        private static void ReadTmx(Options options, Collector collector, ref int read, ref bool truncated)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Ignore;
            settings.XmlResolver = null;
            settings.IgnoreComments = true;
            settings.IgnoreProcessingInstructions = true;
            settings.IgnoreWhitespace = false;

            string headerSource = null;
            try
            {
                using (Stream stream = OpenMaybeGzip(options.Input))
                using (XmlReader reader = XmlReader.Create(stream, settings))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element) continue;

                        if (reader.LocalName == "header")
                        {
                            headerSource = ConcordanceText.Lang(reader.GetAttribute("srclang"));
                            continue;
                        }
                        if (reader.LocalName != "tu") continue;

                        Dictionary<string, string> segs = ReadTu(reader);
                        read++;
                        if (segs.Count < 2) continue;

                        if (options.From == null && headerSource != null) options.From = headerSource;
                        if (options.From == null)
                        {
                            foreach (string key in segs.Keys)
                            {
                                options.From = key;
                                break;
                            }
                        }
                        if (options.To == null)
                        {
                            foreach (string key in segs.Keys)
                            {
                                if (string.Equals(key, options.From, StringComparison.OrdinalIgnoreCase)) continue;
                                options.To = key;
                                break;
                            }
                        }
                        if (options.From == null || options.To == null) continue;

                        string source;
                        string target;
                        if (!segs.TryGetValue(options.From, out source)) continue;
                        if (!segs.TryGetValue(options.To, out target)) continue;
                        if (!collector.TryAdd(source, target)) continue;

                        if (collector.IsFull) break;
                    }
                }
            }
            catch (XmlException ex)
            {
                // A bounded HTTP range download ends in the middle of an XML
                // document. Keep every complete <tu> already parsed.
                truncated = true;
                Logger.Write("concordance: input ended early: " + ex.Message);
                if (collector.Pairs.Count == 0) throw;
            }
            catch (InvalidDataException ex)
            {
                // GZipStream reports the same bounded download as a corrupt
                // member rather than an XML error. The parsed prefix is still
                // useful, so keep it when something was accepted.
                truncated = true;
                Logger.Write("concordance: gzip ended early: " + ex.Message);
                if (collector.Pairs.Count == 0) throw;
            }
        }

        private static void ReadTsv(Options options, Collector collector, ref int read)
        {
            foreach (string line in File.ReadLines(options.Input, Encoding.UTF8))
            {
                if (line.Length == 0) continue;
                if (line[0] == '#')
                {
                    // A pack can be fed back in as a starting point. Honour its
                    // language header instead of relying on the file name.
                    int headerTab = line.IndexOf('\t');
                    if (headerTab > 0)
                    {
                        string key = line.Substring(1, headerTab - 1).Trim().ToLowerInvariant();
                        string value = line.Substring(headerTab + 1).Trim();
                        if (key == "source" && options.From == null) options.From = ConcordanceText.Lang(value);
                        else if (key == "target" && options.To == null) options.To = ConcordanceText.Lang(value);
                    }
                    continue;
                }
                int tab = line.IndexOf('\t');
                if (tab <= 0) continue;
                read++;
                if (collector.TryAdd(line.Substring(0, tab), line.Substring(tab + 1)) && collector.IsFull)
                    break;
            }

            if (options.From == null || options.To == null)
            {
                string stem = Path.GetFileNameWithoutExtension(options.Input);
                int dash = stem.IndexOf('-');
                if (dash > 0)
                {
                    if (options.From == null) options.From = ConcordanceText.Lang(stem.Substring(0, dash));
                    if (options.To == null) options.To = ConcordanceText.Lang(stem.Substring(dash + 1));
                }
            }
        }

        private static Dictionary<string, string> ReadTu(XmlReader reader)
        {
            Dictionary<string, string> segs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (reader.IsEmptyElement) return segs;

            int depth = reader.Depth;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth &&
                    reader.LocalName == "tu") break;
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "tuv") continue;

                string lang = reader.GetAttribute("lang", "http://www.w3.org/XML/1998/namespace");
                if (lang == null) lang = reader.GetAttribute("xml:lang");
                lang = ConcordanceText.Lang(lang);
                string text = ReadTuv(reader);
                if (lang == null || text == null) continue;
                if (!segs.ContainsKey(lang)) segs[lang] = text;
            }
            return segs;
        }

        private static string ReadTuv(XmlReader reader)
        {
            if (reader.IsEmptyElement) return null;
            int depth = reader.Depth;
            string text = null;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth &&
                    reader.LocalName == "tuv") break;
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "seg") continue;
                text = ReadSeg(reader);
            }
            return text;
        }

        private static string ReadSeg(XmlReader reader)
        {
            if (reader.IsEmptyElement) return "";
            int depth = reader.Depth;
            StringBuilder text = new StringBuilder();
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth &&
                    reader.LocalName == "seg") break;
                if (reader.NodeType == XmlNodeType.Text ||
                    reader.NodeType == XmlNodeType.CDATA ||
                    reader.NodeType == XmlNodeType.SignificantWhitespace ||
                    reader.NodeType == XmlNodeType.Whitespace)
                    text.Append(reader.Value);
            }
            return text.ToString();
        }

        private static Stream OpenMaybeGzip(string path)
        {
            FileStream file = File.OpenRead(path);
            int first = file.ReadByte();
            int second = file.ReadByte();
            file.Position = 0;
            if (first == 0x1f && second == 0x8b)
                return new GZipStream(file, CompressionMode.Decompress);
            return file;
        }

        private static string WritePack(string directory, string from, string to, string corpus,
                                        string license, string url, List<ConcordancePair> pairs, bool reverse,
                                        string suffix)
        {
            string name = from + "-" + to + (string.IsNullOrEmpty(suffix) ? "" : "-" + suffix) + ".tsv";
            string path = Path.Combine(directory, name);
            string temp = path + ".tmp";
            using (StreamWriter writer = new StreamWriter(temp, false, Utf8.NoBom))
            {
                writer.WriteLine("#qalam-concordance\t1");
                writer.WriteLine("#source\t" + from);
                writer.WriteLine("#target\t" + to);
                writer.WriteLine("#corpus\t" + OneLine(corpus));
                if (!string.IsNullOrEmpty(license)) writer.WriteLine("#license\t" + OneLine(license));
                if (!string.IsNullOrEmpty(url)) writer.WriteLine("#url\t" + OneLine(url));
                writer.WriteLine("#built\t" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ",
                                                                 CultureInfo.InvariantCulture));
                writer.WriteLine("#pairs\t" + pairs.Count);
                foreach (ConcordancePair pair in pairs)
                {
                    string source = reverse ? pair.Target : pair.Source;
                    string target = reverse ? pair.Source : pair.Target;
                    writer.WriteLine(OneLine(source) + "\t" + OneLine(target));
                }
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
            return path;
        }

        private static string OneLine(string value)
        {
            if (value == null) return "";
            return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        private static string CleanSuffix(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char c in value)
                if (char.IsLetterOrDigit(c) || c == '-') builder.Append(char.ToLowerInvariant(c));
            string cleaned = builder.ToString().Trim('-');
            return cleaned.Length > 24 ? cleaned.Substring(0, 24) : cleaned;
        }

        private static string DefaultCorpusName(string path)
        {
            string name = Path.GetFileName(path);
            if (name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 3);
            if (name.EndsWith(".tmx", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);
            if (name.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);
            return name.Length == 0 ? "Open corpus" : name;
        }

        private static void WriteLog(Options options, string summary, int written, int read,
                                     bool truncated, Stopwatch watch)
        {
            try
            {
                Directory.CreateDirectory(options.OutDir);
                string path = Path.Combine(options.OutDir, "build-concordance.log");
                StringBuilder log = new StringBuilder();
                log.AppendLine("input=" + options.Input);
                log.AppendLine("format=" + (options.Tmx ? "tmx" : "tsv"));
                log.AppendLine("from=" + (options.From ?? "?"));
                log.AppendLine("to=" + (options.To ?? "?"));
                log.AppendLine("limit=" + options.Limit);
                log.AppendLine("sample=" + options.Sample);
                log.AppendLine("both=" + options.Both);
                log.AppendLine("suffix=" + options.Suffix);
                log.AppendLine("contains=" + ConcordanceText.Join(",", options.Contains));
                log.AppendLine("read=" + read);
                log.AppendLine("written=" + written);
                log.AppendLine("truncated=" + truncated);
                log.AppendLine("ms=" + watch.ElapsedMilliseconds);
                log.AppendLine("summary=" + summary);
                File.WriteAllText(path, log.ToString(), Utf8.NoBom);
            }
            catch { }
        }

        private static bool Has(string[] args, string name)
        {
            return args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        }

        private static int IntArg(string[] args, string name, int fallback)
        {
            string value = Arg(args, name);
            int parsed;
            return value != null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }

    internal static class ConcordanceTool
    {
        public static int RunQuery(string[] args)
        {
            string query = Arg(args, "--query");
            if (query == null) query = Arg(args, "--q");
            string from = ConcordanceText.Lang(Arg(args, "--from"));
            string to = ConcordanceText.Lang(Arg(args, "--to"));
            string directory = Arg(args, "--dir");
            string output = Arg(args, "--out");
            if (string.IsNullOrEmpty(output))
                output = Path.Combine(Path.GetTempPath(), "qalam-concordance-query.txt");

            if (string.IsNullOrEmpty(query) || from == null || to == null)
            {
                Write(output, "usage: --findconcordance --query TEXT --from LANG --to LANG [--dir DIR]");
                return 2;
            }

            ConcordanceStore store = string.IsNullOrEmpty(directory)
                ? ConcordanceStore.Shared
                : new ConcordanceStore(directory);
            // Search loads only the packs for this language pair.
            List<ExamplePair> results = store.Search(query, from, to, 8, Arg(args, "--hint"));

            StringBuilder log = new StringBuilder();
            log.AppendLine("query=" + query);
            log.AppendLine("from=" + from + " to=" + to);
            log.AppendLine("packs=" + store.PackCount + " pairs=" + store.PairCount);
            foreach (ExamplePair example in results)
                log.AppendLine((example.Origin ?? "corpus") + ": " + example.Source + " => " + example.Target);

            Write(output, log.ToString());
            Console.WriteLine(log.ToString());
            return results.Count > 0 ? 0 : 1;
        }

        private static void Write(string path, string text)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(path, text, Utf8.NoBom);
                Console.WriteLine("concordance report: " + path);
            }
            catch (Exception ex)
            {
                Console.WriteLine("concordance report failed: " + ex.Message);
            }
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }

    internal static class ConcordanceSelfTest
    {
        public static int Run(string[] args)
        {
            string output = Arg(args, "--out");
            if (string.IsNullOrEmpty(output))
                output = Path.Combine(Path.GetTempPath(), "qalam-concordance-selftest");
            Directory.CreateDirectory(output);

            StringBuilder log = new StringBuilder();
            int exitCode = 0;
            try
            {
                string spanishTmx = Path.Combine(output, "fixture-es-en.tmx");
                File.WriteAllText(spanishTmx, SpanishFixture, Utf8.NoBom);
                string arabicTmx = Path.Combine(output, "fixture-ar-en.tmx");
                File.WriteAllText(arabicTmx, ArabicFixture, Utf8.NoBom);

                string packs = Path.Combine(output, "packs");
                if (Directory.Exists(packs)) Directory.Delete(packs, true);
                Directory.CreateDirectory(packs);

                int first = ConcordanceBuilder.Run(new string[]
                {
                    "--tmx", spanishTmx, "--from", "es", "--to", "en", "--out", packs,
                    "--corpus", "Fixture subtitles", "--license", "CC0", "--both", "--limit", "100", "--index"
                });
                int second = ConcordanceBuilder.Run(new string[]
                {
                    "--tmx", arabicTmx, "--from", "ar", "--to", "en", "--out", packs,
                    "--corpus", "Fixture subtitles", "--license", "CC0", "--both", "--limit", "100", "--index"
                });
                log.AppendLine("build_es_en=" + first);
                log.AppendLine("build_ar_en=" + second);
                if (first != 0 || second != 0) exitCode = 1;

                ConcordanceStore store = new ConcordanceStore(packs);
                store.Load();
                log.AppendLine("packs=" + store.PackCount + " pairs=" + store.PairCount);
                if (store.PackCount != 4) exitCode = 1;

                List<ExamplePair> phrase = store.Search("Calla chingada, no mames con mamar.", "es", "en", 4, null);
                Check(log, "exact_phrase", phrase.Count > 0 &&
                      phrase[0].Source.IndexOf("Calla chingada", StringComparison.OrdinalIgnoreCase) >= 0);
                if (phrase.Count > 0)
                {
                    log.AppendLine("  " + phrase[0].Source + " => " + phrase[0].Target);
                    if (!phrase[0].FromConcordance) exitCode = 1;
                    if (string.IsNullOrEmpty(phrase[0].Origin)) exitCode = 1;
                }

                List<ExamplePair> slang = store.Search("no mames", "es", "en", 4, null);
                Check(log, "slang_phrase", slang.Count > 0 &&
                      slang[0].Source.IndexOf("no mames", StringComparison.OrdinalIgnoreCase) >= 0);
                if (slang.Count > 0) log.AppendLine("  " + slang[0].Source + " => " + slang[0].Target);

                List<ExamplePair> vocalized = store.Search("\u0625\u0650\u0632\u0650\u064a\u0643", "ar", "en", 4, null);
                Check(log, "vocalized_arabic", vocalized.Count > 0 &&
                      vocalized[0].Source.IndexOf("\u0625\u0632\u064a\u0643", StringComparison.Ordinal) >= 0);
                if (vocalized.Count > 0) log.AppendLine("  " + vocalized[0].Source + " => " + vocalized[0].Target);

                List<ExamplePair> arabicPrefix = store.Search("\u0643\u062a\u0627\u0628", "ar", "en", 4, null);
                Check(log, "arabic_prefix_book", arabicPrefix.Count > 0 &&
                      arabicPrefix[0].Source.IndexOf("\u0627\u0644\u0643\u062a\u0627\u0628", StringComparison.Ordinal) >= 0);
                List<ExamplePair> arabicSchool = store.Search("\u0645\u062f\u0631\u0633\u0629", "ar", "en", 4, null);
                Check(log, "arabic_prefix_school", arabicSchool.Count > 0 &&
                      arabicSchool[0].Source.IndexOf("\u0627\u0644\u0645\u062f\u0631\u0633\u0629", StringComparison.Ordinal) >= 0);

                List<ExamplePair> reverse = store.Search("what's up", "en", "es", 4, null);
                Check(log, "reverse_pack", reverse.Count > 0);

                List<ExamplePair> missing = store.Search("zzzz qqqq", "es", "en", 4, null);
                Check(log, "missing_returns_none", missing.Count == 0);

                List<ExamplePair> hint = store.Search("no mames", "es", "en", 4,
                    "Shut the fuck up, don't come at me with that bullshit.");
                Check(log, "hint_ranks_relevant", hint.Count > 0 &&
                      hint[0].Source.IndexOf("Calla chingada", StringComparison.OrdinalIgnoreCase) >= 0);
                if (hint.Count > 0) log.AppendLine("  hint: " + hint[0].Source);
            }
            catch (Exception ex)
            {
                exitCode = 1;
                log.AppendLine("FAIL: " + ex);
            }

            string logPath = Path.Combine(output, "concordancetest.log");
            File.WriteAllText(logPath, log.ToString(), Utf8.NoBom);
            Console.WriteLine(log.ToString());
            Console.WriteLine("concordance test report: " + logPath);
            return exitCode;
        }

        private static void Check(StringBuilder log, string name, bool ok)
        {
            log.AppendLine((ok ? "ok " : "FAIL ") + name);
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        private const string SpanishFixture =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n" +
            "<tmx version=\"1.4\"><header srclang=\"es\" adminlang=\"en\" segtype=\"sentence\" " +
            "creationtool=\"Qalam test\" datatype=\"PlainText\" /><body>\n" +
            "  <tu><tuv xml:lang=\"es\"><seg>Calla chingada, no mames con mamar.</seg></tuv>" +
            "<tuv xml:lang=\"en\"><seg>Shut the fuck up, don't come at me with that bullshit.</seg></tuv></tu>\n" +
            "  <tu><tuv xml:lang=\"es\"><seg>No mames, ese cabr\u00f3n me rob\u00f3 el chingado lugar.</seg></tuv>" +
            "<tuv xml:lang=\"en\"><seg>No way, that bastard stole my fucking spot.</seg></tuv></tu>\n" +
            "  <tu><tuv xml:lang=\"es\"><seg>\u00bfQu\u00e9 onda, g\u00fcey?</seg></tuv>" +
            "<tuv xml:lang=\"en\"><seg>What's up, dude?</seg></tuv></tu>\n" +
            "  <tu><tuv xml:lang=\"es\"><seg>Me lleva la chingada.</seg></tuv>" +
            "<tuv xml:lang=\"en\"><seg>I'm screwed.</seg></tuv></tu>\n" +
            "  <tu><tuv xml:lang=\"es\"><seg>Hijo de la chingada, otra vez.</seg></tuv>" +
            "<tuv xml:lang=\"en\"><seg>Son of a bitch, not again.</seg></tuv></tu>\n" +
            "  <tu><tuv xml:lang=\"es\"><seg>No mames.</seg></tuv>" +
            "<tuv xml:lang=\"en\"><seg>No way.</seg></tuv></tu>\n" +
            "</body></tmx>\n";

        private const string ArabicFixture =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n" +
            "<tmx version=\"1.4\"><header srclang=\"ar\" adminlang=\"en\" segtype=\"sentence\" " +
            "creationtool=\"Qalam test\" datatype=\"PlainText\" /><body>\n" +
            "  <tu><tuv xml:lang=\"ar\"><seg>\u0625\u0632\u064a\u0643 \u064a\u0627 \u0628\u0627\u0634\u0627\u061f</seg></tuv>" +
            "<tuv xml:lang=\"en\"><seg>How are you, boss?</seg></tuv></tu>\n" +
            "  <tu><tuv xml:lang=\"ar\"><seg>\u0625\u0650\u0632\u0650\u064a\u0643\u060c \u0639\u0627\u0645\u0644 \u0625\u064a\u0647\u061f</seg></tuv>" +
            "<tuv xml:lang=\"en\"><seg>How are you, what's up?</seg></tuv></tu>\n" +
            "  <tu><tuv xml:lang=\"ar\"><seg>\u0643\u062a\u0628 \u0627\u0644\u0637\u0627\u0644\u0628 \u0627\u0644\u062f\u0631\u0633.</seg></tuv>" +
            "<tuv xml:lang=\"en\"><seg>The student wrote the lesson.</seg></tuv></tu>\n" +
            "  <tu><tuv xml:lang=\"ar\"><seg>\u0627\u0644\u0643\u062a\u0627\u0628 \u0639\u0644\u0649 \u0627\u0644\u0637\u0627\u0648\u0644\u0629.</seg></tuv>" +
            "<tuv xml:lang=\"en\"><seg>The book is on the table.</seg></tuv></tu>\n" +
            "  <tu><tuv xml:lang=\"ar\"><seg>\u0630\u0647\u0628\u062a \u0625\u0644\u0649 \u0627\u0644\u0645\u062f\u0631\u0633\u0629.</seg></tuv>" +
            "<tuv xml:lang=\"en\"><seg>I went to school.</seg></tuv></tu>\n" +
            "  <tu><tuv xml:lang=\"ar\"><seg>\u0645\u0634 \u0641\u0627\u0631\u0642\u0629.</seg></tuv>" +
            "<tuv xml:lang=\"en\"><seg>It doesn't matter.</seg></tuv></tu>\n" +
            "</body></tmx>\n";
    }
}
