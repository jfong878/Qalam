// Grammar and conjugation lookup from open, attributable data.
//
// Kaikki.org publishes a machine-readable view of English Wiktionary under
// CC BY-SA. A word page carries the part of speech, IPA, short definitions and
// the inflected forms that Wiktionary extracted for that lemma. Qalam parses
// only those fields, keeps a small in-memory cache, and always labels the
// source and license in the popup.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Qalam
{
    internal sealed class GrammarForm
    {
        public string Value;
        public string[] Tags;
    }

    internal sealed class GrammarTense
    {
        public string Name;
        public List<GrammarForm> Forms = new List<GrammarForm>();
        public string[] Labels;
        // Zero keeps the compact single-line table used by the Latin-script
        // languages. Arabic sets a small group size so a mixed-script row does
        // not wrap in the middle of a person/form pair.
        public int FormsPerLine;
    }

    internal sealed class GrammarInfo
    {
        public string Word;
        public string Language;
        public string PartOfSpeech;
        public string Pronunciation;
        public List<string> Definitions = new List<string>();
        public List<string> NonFinite = new List<string>();
        public List<GrammarTense> Tenses = new List<GrammarTense>();
        public string SourceUrl;
        public string Error;

        // True only for a result loaded from the on-disk cache. It is deliberately
        // not serialized back into the cache file.
        [ScriptIgnore]
        public bool FromCache;

        public bool HasContent
        {
            get { return Definitions.Count > 0 || Tenses.Count > 0; }
        }

        public bool HasConjugation
        {
            get { return Tenses.Count > 0; }
        }
    }

    internal sealed class GrammarCacheEntry
    {
        public GrammarInfo Info;
        public DateTime SavedUtc;
    }

    internal sealed class GrammarCacheFile
    {
        public int Version = 1;
        public Dictionary<string, GrammarCacheEntry> Entries =
            new Dictionary<string, GrammarCacheEntry>(StringComparer.OrdinalIgnoreCase);
    }

    internal static class GrammarSelfTest
    {
        public static int Run(string[] args)
        {
            string output = null;
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase)) output = args[i + 1];
            if (string.IsNullOrEmpty(output)) output = Path.Combine(Paths.AppDir, "selftest", "grammar.txt");

            StringBuilder log = new StringBuilder();
            int exit = 0;
            GrammarService service = new GrammarService();
            string[,] words = new string[,]
            {
                { "hablar", "es" }, { "comer", "es" }, { "parler", "fr" }, { "student", "en" },
                { "\u0643\u062a\u0628", "ar" }, { "\u0643\u064e\u062a\u064e\u0628\u064e", "ar" }
            };
            for (int i = 0; i < words.GetLength(0); i++)
            {
                string word = words[i, 0];
                string language = words[i, 1];
                try
                {
                    GrammarInfo info = service.LookupAsync(word, language).GetAwaiter().GetResult();
                    log.AppendLine(word + " [" + language + "] has=" + info.HasContent +
                                   " pos=" + info.PartOfSpeech + " defs=" + info.Definitions.Count +
                                   " tenses=" + info.Tenses.Count);
                    foreach (string definition in info.Definitions)
                        log.AppendLine("  def: " + definition);
                    if (info.NonFinite.Count > 0)
                        log.AppendLine("  nonfinite: " + string.Join(" | ", info.NonFinite.ToArray()));
                    foreach (GrammarTense tense in info.Tenses)
                    {
                        StringBuilder forms = new StringBuilder();
                        for (int f = 0; f < tense.Forms.Count; f++)
                        {
                            if (tense.Forms[f] == null) continue;
                            if (forms.Length > 0) forms.Append(" | ");
                            string label = tense.Labels != null && f < tense.Labels.Length ? tense.Labels[f] + " " : "";
                            forms.Append(label).Append(tense.Forms[f].Value);
                        }
                        log.AppendLine("  " + tense.Name + ": " + forms.ToString());
                    }
                }
                catch (Exception ex)
                {
                    exit = 1;
                    log.AppendLine(word + " FAIL " + ex.Message);
                }
            }

            // A fresh service exercises the on-disk cache rather than the
            // in-memory dictionary of the service that performed the lookups.
            try
            {
                GrammarService cachedService = new GrammarService();
                GrammarInfo cached = cachedService.LookupAsync("hablar", "es", false)
                    .GetAwaiter().GetResult();
                log.AppendLine("cache_path=" + GrammarService.CacheFilePath);
                log.AppendLine("cache_count=" + cachedService.CachedCount);
                log.AppendLine("cache_has=" + (cached != null && cached.HasContent) +
                               " from_disk=" + (cached != null && cached.FromCache));
                if (cached == null || !cached.HasContent || !cached.FromCache) exit = 1;
            }
            catch (Exception ex)
            {
                exit = 1;
                log.AppendLine("cache_FAIL=" + ex.Message);
            }

            string outputDir = Path.GetDirectoryName(Path.GetFullPath(output));
            if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);
            File.WriteAllText(output, log.ToString(), Utf8.NoBom);
            Console.WriteLine(log.ToString());
            return exit;
        }
    }

    internal static class GrammarText
    {
        public static string Summary(GrammarInfo info)
        {
            if (info == null) return "";
            StringBuilder text = new StringBuilder();
            if (!string.IsNullOrEmpty(info.Pronunciation))
                text.Append(info.Pronunciation).Append("\n");
            if (info.Definitions.Count > 0)
            {
                for (int i = 0; i < info.Definitions.Count && i < 3; i++)
                {
                    if (i > 0) text.Append("\n");
                    text.Append("\u2022 ").Append(info.Definitions[i]);
                }
                text.Append("\n");
            }
            if (info.NonFinite.Count > 0)
                text.Append(string.Join("  \u00b7  ", info.NonFinite.ToArray())).Append("\n");
            foreach (GrammarTense tense in info.Tenses)
            {
                text.Append(tense.Name).Append(": ");
                bool first = true;
                int shown = 0;
                for (int i = 0; i < tense.Forms.Count; i++)
                {
                    if (tense.Forms[i] == null) continue;
                    if (!first)
                    {
                        if (tense.FormsPerLine > 0 && shown % tense.FormsPerLine == 0)
                            text.Append("\n  ");
                        else
                            text.Append("  \u00b7  ");
                    }
                    string label = tense.Labels != null && i < tense.Labels.Length ? tense.Labels[i] + " " : "";
                    text.Append(label).Append(tense.Forms[i].Value);
                    first = false;
                    shown++;
                }
                text.Append("\n");
            }
            return text.ToString().Trim();
        }
    }

    internal sealed class GrammarService
    {
        private sealed class HeadingSection
        {
            public string Name;
            public string Html;
        }

        private const int CacheVersion = 1;
        private const int MaxCacheEntries = 400;

        private readonly HttpClient _http;
        private readonly object _gate = new object();
        private readonly Dictionary<string, GrammarCacheEntry> _entries =
            new Dictionary<string, GrammarCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private bool _cacheLoaded;
        private bool _cacheDirty;

        public GrammarService()
        {
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromSeconds(15);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Qalam/1.0 (local dictionary lookup)");
            _http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
        }

        public static string CacheFilePath
        {
            get { return Path.Combine(Paths.DataDir, "grammar-cache.json"); }
        }

        public int CachedCount
        {
            get
            {
                EnsureCacheLoaded();
                lock (_gate)
                {
                    int count = 0;
                    foreach (GrammarCacheEntry entry in _entries.Values)
                        if (entry != null && entry.Info != null && entry.Info.HasContent) count++;
                    return count;
                }
            }
        }

        public Task<GrammarInfo> LookupAsync(string word, string language)
        {
            return LookupAsync(word, language, true);
        }

        public async Task<GrammarInfo> LookupAsync(string word, string language, bool allowNetwork)
        {
            if (string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(language)) return null;

            string clean = CleanWord(word);
            if (clean.Length < 2 || clean.Length > 50) return null;

            string code = NormalizeCode(language);
            string languageName = Colloquial.LanguageName(code);
            string key = CacheKey(code, clean);

            EnsureCacheLoaded();
            lock (_gate)
            {
                GrammarCacheEntry cached;
                if (_entries.TryGetValue(key, out cached) && cached != null && cached.Info != null)
                {
                    if (!allowNetwork) cached.Info.FromCache = true;
                    return cached.Info;
                }
            }

            if (!allowNetwork) return null;

            GrammarInfo info = null;
            string[] candidates = CandidateWords(clean);
            foreach (string candidate in candidates)
            {
                try
                {
                    string url = BuildUrl(languageName, candidate);
                    string html = await _http.GetStringAsync(url).ConfigureAwait(false);
                    info = ParseHtml(html, clean, languageName, url);
                    if (info != null && info.HasContent) break;
                    info = null;
                }
                catch (HttpRequestException)
                {
                    // A missing page is a normal answer for an unknown word; try
                    // the next casing before giving up.
                    info = null;
                }
                catch (Exception ex)
                {
                    Logger.Write("grammar " + languageName + ": " + ex.Message);
                    info = null;
                }
            }

            if (info == null)
            {
                info = new GrammarInfo();
                info.Word = clean;
                info.Language = languageName;
                info.Error = "No open-dictionary entry found.";
            }

            info.FromCache = false;
            lock (_gate)
            {
                _entries[key] = new GrammarCacheEntry { Info = info, SavedUtc = DateTime.UtcNow };
                if (info.HasContent)
                {
                    _cacheDirty = true;
                    TrimCache();
                }
            }
            if (info.HasContent) SaveCache();
            return info;
        }

        // Returns the most recent cached entry for a word in any language. This
        // is used when translation failed and the source language is therefore
        // unknown, and as the offline-mode fallback.
        public GrammarInfo LookupCached(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return null;
            string clean = CleanWord(word);
            if (clean.Length < 2 || clean.Length > 50) return null;

            EnsureCacheLoaded();
            string normalized = StripArabicDiacritics(clean).ToLowerInvariant();
            lock (_gate)
            {
                GrammarInfo best = null;
                DateTime bestSaved = DateTime.MinValue;
                foreach (KeyValuePair<string, GrammarCacheEntry> pair in _entries)
                {
                    GrammarCacheEntry entry = pair.Value;
                    if (entry == null || entry.Info == null || !entry.Info.HasContent) continue;
                    int bar = pair.Key.IndexOf('|');
                    string candidateWord = bar >= 0 ? pair.Key.Substring(bar + 1) : pair.Key;
                    if (!string.Equals(candidateWord, normalized, StringComparison.OrdinalIgnoreCase)) continue;
                    if (best == null || entry.SavedUtc > bestSaved)
                    {
                        best = entry.Info;
                        bestSaved = entry.SavedUtc;
                    }
                }
                if (best != null) best.FromCache = true;
                return best;
            }
        }

        private static string CacheKey(string code, string word)
        {
            return (code == null ? "" : code) + "|" +
                   StripArabicDiacritics(word).ToLowerInvariant();
        }

        private void EnsureCacheLoaded()
        {
            lock (_gate)
            {
                if (_cacheLoaded) return;
                _cacheLoaded = true;
                try
                {
                    string path = CacheFilePath;
                    if (!File.Exists(path)) return;
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = 8 * 1024 * 1024;
                    GrammarCacheFile file = serializer.Deserialize<GrammarCacheFile>(json);
                    if (file == null || file.Entries == null) return;
                    if (file.Version != CacheVersion)
                    {
                        Logger.Write("grammar cache ignored: version " + file.Version);
                        return;
                    }

                    foreach (KeyValuePair<string, GrammarCacheEntry> pair in file.Entries)
                    {
                        if (pair.Value == null || pair.Value.Info == null || !pair.Value.Info.HasContent) continue;
                        pair.Value.Info.FromCache = true;
                        _entries[pair.Key] = pair.Value;
                    }
                    TrimCache();
                    Logger.Write("grammar cache loaded: " + _entries.Count);
                }
                catch (Exception ex)
                {
                    Logger.Write("grammar cache load: " + ex.Message);
                }
            }
        }

        // Caller must hold _gate.
        private void TrimCache()
        {
            if (_entries.Count <= MaxCacheEntries) return;
            List<KeyValuePair<string, GrammarCacheEntry>> ordered =
                new List<KeyValuePair<string, GrammarCacheEntry>>(_entries);
            ordered.Sort(delegate(KeyValuePair<string, GrammarCacheEntry> a,
                                   KeyValuePair<string, GrammarCacheEntry> b)
            {
                return a.Value.SavedUtc.CompareTo(b.Value.SavedUtc);
            });
            int remove = _entries.Count - MaxCacheEntries;
            for (int i = 0; i < remove; i++) _entries.Remove(ordered[i].Key);
        }

        private void SaveCache()
        {
            if (!_cacheDirty) return;
            lock (_gate)
            {
                if (!_cacheDirty) return;
                GrammarCacheFile file = new GrammarCacheFile();
                file.Version = CacheVersion;
                foreach (KeyValuePair<string, GrammarCacheEntry> pair in _entries)
                {
                    if (pair.Value == null || pair.Value.Info == null || !pair.Value.Info.HasContent) continue;
                    file.Entries[pair.Key] = pair.Value;
                }

                try
                {
                    Directory.CreateDirectory(Paths.DataDir);
                    string path = CacheFilePath;
                    string temp = path + ".tmp";
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = 8 * 1024 * 1024;
                    File.WriteAllText(temp, serializer.Serialize(file), Utf8.NoBom);
                    if (File.Exists(path))
                    {
                        try { File.Replace(temp, path, null); }
                        catch
                        {
                            File.Copy(temp, path, true);
                            File.Delete(temp);
                        }
                    }
                    else
                    {
                        File.Move(temp, path);
                    }
                    _cacheDirty = false;
                    Logger.Write("grammar cache saved: " + file.Entries.Count);
                }
                catch (Exception ex)
                {
                    Logger.Write("grammar cache save: " + ex.Message);
                }
            }
        }

        private static string CleanWord(string word)
        {
            string value = word.Trim();
            value = value.Trim('\'', '"', '\u201c', '\u201d', '.', ',', ';', ':', '!', '?', '\u061f', '\u060c');
            if (value.IndexOf(' ') >= 0 || value.IndexOf('\t') >= 0 || value.IndexOf('\n') >= 0) return "";
            foreach (char c in value)
                if (!char.IsLetter(c) && c != '-' && c != '\'' && !IsArabicMark(c)) return "";
            return value;
        }

        private static string[] CandidateWords(string word)
        {
            List<string> candidates = new List<string>();
            AddCandidate(candidates, word);
            AddCandidate(candidates, word.ToLowerInvariant());
            string bare = StripArabicDiacritics(word);
            AddCandidate(candidates, bare);
            AddCandidate(candidates, bare.ToLowerInvariant());
            if (word.Length > 0)
                AddCandidate(candidates, char.ToUpperInvariant(word[0]) + word.Substring(1));
            return candidates.ToArray();
        }

        private static void AddCandidate(List<string> candidates, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (!candidates.Contains(value)) candidates.Add(value);
        }

        private static bool IsArabicMark(char c)
        {
            return (c >= '\u0610' && c <= '\u061A') || (c >= '\u064B' && c <= '\u065F') ||
                   c == '\u0670' || (c >= '\u06D6' && c <= '\u06ED');
        }

        private static string StripArabicDiacritics(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;
            StringBuilder value = new StringBuilder(word.Length);
            foreach (char c in word)
            {
                // Harakat, Tanwīn, shadda, sukun, superscript alef and the
                // Quranic annotation range are not part of a Kaikki page key.
                if (IsArabicMark(c)) continue;
                value.Append(c);
            }
            return value.ToString();
        }

        private static string BuildUrl(string languageName, string word)
        {
            string lower = word.ToLowerInvariant();
            string first = lower.Substring(0, 1);
            string prefix = lower.Length > 1 ? lower.Substring(0, 2) : first;
            return "https://kaikki.org/dictionary/" + Uri.EscapeDataString(languageName) +
                   "/meaning/" + Uri.EscapeDataString(first) + "/" + Uri.EscapeDataString(prefix) +
                   "/" + Uri.EscapeDataString(lower) + ".html";
        }

        private static GrammarInfo ParseHtml(string html, string word, string languageName, string url)
        {
            if (string.IsNullOrEmpty(html)) return null;

            GrammarInfo info = new GrammarInfo();
            info.Word = word;
            info.Language = languageName;
            info.SourceUrl = url;

            // Wiktionary pages can have several parts of speech. A verb page
            // often starts with a noun sense (Spanish comer = food), so choose
            // the section by heading rather than taking the first one.
            List<HeadingSection> sections = BuildSections(html);
            HeadingSection section = ChooseSection(sections);
            string sectionHtml = section == null ? html : section.Html;
            if (section != null) info.PartOfSpeech = section.Name;

            Match ipa = Regex.Match(sectionHtml, @"<span class=""infolabel"">IPA</span>\s*:\s*([^<]+)",
                RegexOptions.IgnoreCase);
            if (ipa.Success) info.Pronunciation = WebUtility.HtmlDecode(ipa.Groups[1].Value).Trim();

            int listStart = sectionHtml.IndexOf("<ol", StringComparison.OrdinalIgnoreCase);
            if (listStart >= 0)
            {
                int listEnd = sectionHtml.IndexOf("</ol>", listStart, StringComparison.OrdinalIgnoreCase);
                if (listEnd > listStart)
                {
                    string list = sectionHtml.Substring(listStart, listEnd - listStart);
                    MatchCollection glosses = Regex.Matches(list, @"<span class=""gloss"">(.*?)</span>",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    foreach (Match gloss in glosses)
                    {
                        string definition = StripHtml(gloss.Groups[1].Value);
                        if (string.IsNullOrEmpty(definition)) continue;
                        if (!info.Definitions.Contains(definition)) info.Definitions.Add(definition);
                        if (info.Definitions.Count >= 4) break;
                    }
                }
            }

            Match forms = Regex.Match(sectionHtml,
                @"<span class=""infolabel"">Forms</span>\s*:\s*(.*?)</span>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!forms.Success) return info.Definitions.Count > 0 ? info : null;

            List<GrammarForm> parsed = ParseForms(forms.Groups[1].Value);
            if (parsed.Count == 0) return info.Definitions.Count > 0 ? info : null;

            bool haveInfinitive = false;
            bool haveGerund = false;
            bool haveParticiple = false;
            bool haveVerbalNoun = false;
            bool haveActiveParticiple = false;
            bool havePassiveParticiple = false;
            foreach (GrammarForm form in parsed)
            {
                if (HasTag(form, "infinitive") && !haveInfinitive)
                {
                    info.NonFinite.Add("infinitive: " + form.Value);
                    haveInfinitive = true;
                }
                else if (HasTag(form, "gerund") && !haveGerund)
                {
                    info.NonFinite.Add("gerund: " + form.Value);
                    haveGerund = true;
                }
                else if (HasTag(form, "participle") && HasTag(form, "past") && !haveParticiple)
                {
                    // Wiktionary lists masculine, feminine, singular and plural
                    // participles, then combined forms. The first one is the
                    // citation form and is enough for the compact card.
                    info.NonFinite.Add("past participle: " + form.Value);
                    haveParticiple = true;
                }
                else if (HasTag(form, "noun-from-verb") && !haveVerbalNoun)
                {
                    info.NonFinite.Add("verbal noun: " + form.Value);
                    haveVerbalNoun = true;
                }
                else if (HasTag(form, "participle") && HasTag(form, "active") &&
                         !HasTag(form, "past") && !haveActiveParticiple)
                {
                    info.NonFinite.Add("active participle: " + form.Value);
                    haveActiveParticiple = true;
                }
                else if (HasTag(form, "participle") && HasTag(form, "passive") &&
                         !HasTag(form, "past") && !havePassiveParticiple)
                {
                    info.NonFinite.Add("passive participle: " + form.Value);
                    havePassiveParticiple = true;
                }
            }

            string[] labels = Pronouns(languageName);
            if (languageName == "Arabic")
            {
                AddArabicTenses(info, parsed);
                return info;
            }

            AddTense(info, parsed, labels, "Present", new string[] { "present", "indicative" });
            AddTense(info, parsed, labels, "Preterite", new string[] { "preterite", "indicative" });
            AddTense(info, parsed, labels, "Imperfect", new string[] { "imperfect", "indicative" });
            AddTense(info, parsed, labels, "Future", new string[] { "future", "indicative" });
            AddTense(info, parsed, labels, "Conditional", new string[] { "conditional", "indicative" });
            AddTense(info, parsed, labels, "Present subjunctive", new string[] { "present", "subjunctive" });
            AddTense(info, parsed, labels, "Imperfect subjunctive", new string[] { "imperfect", "subjunctive" });
            AddTense(info, parsed, labels, "Imperative", new string[] { "imperative" });

            return info;
        }

        private static void AddArabicTenses(GrammarInfo info, List<GrammarForm> forms)
        {
            // Keep the English person label first. These lines mix scripts, and
            // an Arabic-first line would let the bidi algorithm reverse the whole
            // row; an English anchor keeps the table in reading order while the
            // Arabic pronoun and form still shape correctly.
            string[] pastLabels = new string[]
            {
                "I (\u0623\u0646\u0627)", "you m. (\u0623\u0646\u062a\u064e)", "you f. (\u0623\u0646\u062a\u0650)",
                "he (\u0647\u0648)", "she (\u0647\u064a)", "we (\u0646\u062d\u0646)",
                "you pl. m. (\u0623\u0646\u062a\u0645)", "they m. (\u0647\u0645)", "they f. (\u0647\u0646\u0651)"
            };
            string[] persons = new string[]
            {
                "first-person", "second-person", "second-person", "third-person",
                "third-person", "first-person", "second-person", "third-person", "third-person"
            };
            string[] genders = new string[]
            {
                "masculine", "masculine", "feminine", "masculine",
                "feminine", "masculine", "masculine", "masculine", "feminine"
            };
            string[] numbers = new string[]
            {
                "singular", "singular", "singular", "singular",
                "singular", "plural", "plural", "plural", "plural"
            };

            AddArabicTense(info, forms, "Past (\u0627\u0644\u0645\u0627\u0636\u064a)",
                new string[] { "perfective" }, pastLabels, persons, genders, numbers);
            AddArabicTense(info, forms, "Present (\u0627\u0644\u0645\u0636\u0627\u0631\u0639)",
                new string[] { "imperfective", "indicative" }, pastLabels, persons, genders, numbers);

            string[] imperativeLabels = new string[]
            {
                "you m. (\u0623\u0646\u062a\u064e)", "you f. (\u0623\u0646\u062a\u0650)",
                "you pl. m. (\u0623\u0646\u062a\u0645)", "you pl. f. (\u0623\u0646\u062a\u0646\u0651)"
            };
            string[] imperativePersons = new string[] { "second-person", "second-person", "second-person", "second-person" };
            string[] imperativeGenders = new string[] { "masculine", "feminine", "masculine", "feminine" };
            string[] imperativeNumbers = new string[] { "singular", "singular", "plural", "plural" };
            AddArabicTense(info, forms, "Imperative (\u0627\u0644\u0623\u0645\u0631)",
                new string[] { "imperative" }, imperativeLabels, imperativePersons, imperativeGenders, imperativeNumbers);
        }

        private static void AddArabicTense(GrammarInfo info, List<GrammarForm> forms, string name,
            string[] required, string[] labels, string[] persons, string[] genders, string[] numbers)
        {
            GrammarTense tense = new GrammarTense();
            tense.Name = name;
            tense.Labels = labels;
            tense.FormsPerLine = labels.Length > 4 ? 3 : 2;
            for (int i = 0; i < labels.Length; i++)
            {
                GrammarForm form = FindArabicForm(forms, required, persons[i], genders[i], numbers[i]);
                tense.Forms.Add(form);
            }

            bool any = false;
            foreach (GrammarForm form in tense.Forms) if (form != null) { any = true; break; }
            if (any) info.Tenses.Add(tense);
        }

        private static GrammarForm FindArabicForm(List<GrammarForm> forms, string[] required,
            string person, string gender, string number)
        {
            foreach (GrammarForm form in forms)
            {
                if (!HasAll(form, required)) continue;
                if (!HasTag(form, person) || !HasTag(form, gender) || !HasTag(form, number)) continue;
                if (HasTag(form, "passive")) continue;
                return form;
            }
            return null;
        }

        private static List<HeadingSection> BuildSections(string html)
        {
            List<HeadingSection> sections = new List<HeadingSection>();
            MatchCollection headings = Regex.Matches(html, @"<h2[^>]*>(.*?)</h2>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            for (int i = 0; i < headings.Count; i++)
            {
                Match heading = headings[i];
                int start = heading.Index + heading.Length;
                int end = i + 1 < headings.Count ? headings[i + 1].Index : html.Length;
                if (end <= start) continue;
                HeadingSection section = new HeadingSection();
                section.Name = StripHtml(heading.Groups[1].Value);
                section.Html = html.Substring(start, end - start);
                sections.Add(section);
            }
            return sections;
        }

        private static HeadingSection ChooseSection(List<HeadingSection> sections)
        {
            HeadingSection verb = null;
            HeadingSection noun = null;
            HeadingSection adjective = null;
            HeadingSection first = null;
            foreach (HeadingSection section in sections)
            {
                string name = section.Name == null ? "" : section.Name.ToLowerInvariant();
                if (name.IndexOf("inflected") >= 0 || name.IndexOf("alternative") >= 0 ||
                    name.IndexOf("etymology") >= 0 || name.IndexOf("pronunciation") >= 0)
                    continue;
                if (first == null) first = section;
                if (name == "verb" && verb == null) verb = section;
                else if (name == "noun" && noun == null) noun = section;
                else if (name == "adjective" && adjective == null) adjective = section;
            }
            // A verb section is the most useful choice for a word lookup: it
            // carries the conjugation table. Fall back through the common
            // nominal categories, then the first substantive section.
            if (verb != null) return verb;
            if (noun != null) return noun;
            if (adjective != null) return adjective;
            return first;
        }

        private static List<GrammarForm> ParseForms(string html)
        {
            List<GrammarForm> forms = new List<GrammarForm>();
            // Spanish and most Latin-script entries link every form. Arabic and
            // several other scripts carry the same "value [tags]" list as plain
            // text, so accept both shapes. A value never contains a comma; the
            // tags do.
            string clean = Regex.Replace(html, @"\s+", " ");
            MatchCollection matches = Regex.Matches(clean,
                @"(?:<a\s+[^>]*>(.*?)</a>|([^,<>\[\]]+?))\s*\[([^\]]*)\]",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match match in matches)
            {
                string rawValue = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                string value = StripHtml(rawValue);
                if (string.IsNullOrEmpty(value) || string.Equals(value, "no-table-tags", StringComparison.OrdinalIgnoreCase))
                    continue;
                string rawTags = match.Groups[3].Value;
                if (string.IsNullOrEmpty(rawTags)) continue;
                string[] tags = rawTags.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < tags.Length; i++) tags[i] = tags[i].Trim().ToLowerInvariant();
                GrammarForm form = new GrammarForm();
                form.Value = value;
                form.Tags = tags;
                forms.Add(form);
            }
            return forms;
        }

        private static void AddTense(GrammarInfo info, List<GrammarForm> forms, string[] labels,
            string name, string[] required)
        {
            GrammarTense tense = new GrammarTense();
            tense.Name = name;
            tense.Labels = labels;

            string[] persons = new string[] { "first-person", "second-person", "third-person" };
            string[] numbers = new string[] { "singular", "plural" };
            for (int n = 0; n < numbers.Length; n++)
            {
                for (int p = 0; p < persons.Length; p++)
                {
                    GrammarForm form = FindForm(forms, required, persons[p], numbers[n]);
                    if (form != null) tense.Forms.Add(form);
                    else tense.Forms.Add(null);
                }
            }

            bool any = false;
            foreach (GrammarForm form in tense.Forms) if (form != null) { any = true; break; }
            if (any) info.Tenses.Add(tense);
        }

        private static GrammarForm FindForm(List<GrammarForm> forms, string[] required, string person, string number)
        {
            GrammarForm fallback = null;
            foreach (GrammarForm form in forms)
            {
                if (!HasAll(form, required)) continue;
                if (!HasTag(form, person) || !HasTag(form, number)) continue;
                if (HasTag(form, "vos-form")) continue;

                bool formal = HasTag(form, "formal");
                if (!formal && !HasTag(form, "negative")) return form;
                if (fallback == null) fallback = form;
            }
            return fallback;
        }

        private static bool HasAll(GrammarForm form, string[] required)
        {
            foreach (string tag in required)
                if (!HasTag(form, tag)) return false;
            return true;
        }

        private static bool HasTag(GrammarForm form, string tag)
        {
            foreach (string value in form.Tags)
                if (string.Equals(value, tag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string[] Pronouns(string languageName)
        {
            switch (languageName)
            {
                case "Spanish": return new string[] { "yo", "tú", "él/ella", "nosotros", "vosotros", "ellos/ellas" };
                case "French": return new string[] { "je", "tu", "il/elle", "nous", "vous", "ils/elles" };
                case "Italian": return new string[] { "io", "tu", "lui/lei", "noi", "voi", "loro" };
                case "Portuguese": return new string[] { "eu", "tu", "ele/ela", "nós", "vós", "eles/elas" };
                case "German": return new string[] { "ich", "du", "er/sie/es", "wir", "ihr", "sie" };
                case "English": return new string[] { "I", "you", "he/she/it", "we", "you", "they" };
                case "Arabic": return new string[] { "أنا", "أنت", "هو/هي", "نحن", "أنتم", "هم" };
                default: return new string[] { "1s", "2s", "3s", "1p", "2p", "3p" };
            }
        }

        private static string NormalizeCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            string value = code.ToLowerInvariant();
            int dash = value.IndexOf('-');
            if (dash > 0) value = value.Substring(0, dash);
            return value;
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            string value = Regex.Replace(html, @"(?s)<script.*?</script>", " ");
            value = Regex.Replace(value, @"(?s)<style.*?</style>", " ");
            value = Regex.Replace(value, @"(?s)<[^>]+>", " ");
            value = WebUtility.HtmlDecode(value);
            value = Regex.Replace(value, @"\s+", " ").Trim();
            return value;
        }
    }
}
