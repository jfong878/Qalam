// Headless quality comparison for Qalam's online and offline translation paths.
//
// This is intentionally a small, fixed set of sentences with authored reference
// translations. It is a diagnostic, not a benchmark claiming to measure all of
// translation quality: token overlap rewards literal output, so the generated
// report keeps every output side by side for review.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Qalam
{
    internal sealed class CompareCase
    {
        public string Id;
        public string Source;
        public string Target;
        public string Text;
        public string Reference;
        public string Category;
    }

    internal sealed class CompareResult
    {
        public CompareCase Case;
        public string OnlineText = "";
        public string OfflineText = "";
        public string OnlineEngine = "";
        public string OfflineEngine = "";
        public string OnlineError = "";
        public string OfflineError = "";
        public long OnlineMs;
        public long OfflineMs;
        public double OnlineF1;
        public double OfflineF1;
        public bool OnlineScript;
        public bool OfflineScript;
    }

    internal static class TranslationCompare
    {
        public static int Run(string[] args)
        {
            string output = Arg(args, "--out");
            if (string.IsNullOrEmpty(output)) output = Path.Combine(Paths.AppDir, "selftest", "compare");
            Directory.CreateDirectory(output);

            string endpoint = Arg(args, "--endpoint");
            if (string.IsNullOrEmpty(endpoint)) endpoint = "http://127.0.0.1:11434";
            string model = Arg(args, "--model");

            Config config = new Config();
            TranslationService online = new TranslationService();
            online.ConfigureProvider(true, endpoint, model, "online");
            TranslationService offline = new TranslationService();
            offline.ConfigureProvider(true, endpoint, model, "offline");

            List<CompareCase> cases = BuildCases();
            List<CompareResult> results = new List<CompareResult>();
            StringBuilder log = new StringBuilder();
            log.AppendLine("Qalam online vs offline translation comparison");
            log.AppendLine("endpoint=" + endpoint);
            log.AppendLine("model=" + (string.IsNullOrEmpty(model) ? "auto" : model));
            log.AppendLine("cases=" + cases.Count);
            log.AppendLine();

            foreach (CompareCase item in cases)
            {
                CompareResult result = new CompareResult();
                result.Case = item;

                Stopwatch watch = Stopwatch.StartNew();
                try
                {
                    TranslationOutcome outcome = online.TranslateAsync(item.Text, item.Target)
                        .GetAwaiter().GetResult();
                    result.OnlineText = outcome == null ? "" : outcome.Text;
                    result.OnlineEngine = outcome == null ? "" : outcome.Engine;
                }
                catch (Exception ex)
                {
                    result.OnlineError = ex.Message;
                }
                watch.Stop();
                result.OnlineMs = watch.ElapsedMilliseconds;

                watch = Stopwatch.StartNew();
                try
                {
                    TranslationOutcome outcome = offline.TranslateAsync(item.Text, item.Target)
                        .GetAwaiter().GetResult();
                    result.OfflineText = outcome == null ? "" : outcome.Text;
                    result.OfflineEngine = outcome == null ? "" : outcome.Engine;
                }
                catch (Exception ex)
                {
                    result.OfflineError = ex.Message;
                }
                watch.Stop();
                result.OfflineMs = watch.ElapsedMilliseconds;

                result.OnlineF1 = Score(result.OnlineText, item.Reference);
                result.OfflineF1 = Score(result.OfflineText, item.Reference);
                result.OnlineScript = ScriptMatches(result.OnlineText, item.Target);
                result.OfflineScript = ScriptMatches(result.OfflineText, item.Target);
                results.Add(result);

                log.AppendLine(item.Id + " " + item.Source + "->" + item.Target + " [" + item.Category + "]");
                log.AppendLine("  source:  " + item.Text);
                log.AppendLine("  ref:     " + item.Reference);
                log.AppendLine("  online:  " + Format(result.OnlineText, result.OnlineError) +
                               "  f1=" + result.OnlineF1.ToString("0.00") + "  " + result.OnlineMs + "ms");
                log.AppendLine("  offline: " + Format(result.OfflineText, result.OfflineError) +
                               "  f1=" + result.OfflineF1.ToString("0.00") + "  " + result.OfflineMs + "ms");
                log.AppendLine();
            }

            string markdown = BuildMarkdown(results, endpoint, model, offline.LocalModelName);
            File.WriteAllText(Path.Combine(output, "compare.md"), markdown, Utf8.NoBom);
            File.WriteAllText(Path.Combine(output, "compare.txt"), log.ToString(), Utf8.NoBom);
            File.WriteAllText(Path.Combine(output, "compare.json"), BuildJson(results), Utf8.NoBom);

            Console.WriteLine(log.ToString());
            Console.WriteLine("Wrote " + Path.Combine(output, "compare.md"));
            return 0;
        }

        private static List<CompareCase> BuildCases()
        {
            List<CompareCase> cases = new List<CompareCase>();
            Add(cases, "standard-1", "en", "ar", "The student wrote the lesson at school this morning.",
                "\u0643\u062a\u0628 \u0627\u0644\u0637\u0627\u0644\u0628 \u0627\u0644\u062f\u0631\u0633 \u0641\u064a \u0627\u0644\u0645\u062f\u0631\u0633\u0629 \u0647\u0630\u0627 \u0627\u0644\u0635\u0628\u0627\u062d.", "standard");
            Add(cases, "standard-2", "ar", "en", "\u0643\u062a\u0628 \u0627\u0644\u0637\u0627\u0644\u0628 \u0627\u0644\u062f\u0631\u0633 \u0641\u064a \u0627\u0644\u0645\u062f\u0631\u0633\u0629 \u0647\u0630\u0627 \u0627\u0644\u0635\u0628\u0627\u062d.",
                "The student wrote the lesson at school this morning.", "standard");
            Add(cases, "standard-3", "es", "en", "El estudiante escribi\u00f3 la lecci\u00f3n en la escuela esta ma\u00f1ana.",
                "The student wrote the lesson at school this morning.", "standard");
            Add(cases, "standard-4", "en", "es", "The student wrote the lesson at school this morning.",
                "El estudiante escribi\u00f3 la lecci\u00f3n en la escuela esta ma\u00f1ana.", "standard");
            Add(cases, "standard-5", "en", "ar", "I will call you tomorrow after work.",
                "\u0633\u0623\u062a\u0635\u0644 \u0628\u0643 \u063a\u062f\u064b\u0627 \u0628\u0639\u062f \u0627\u0644\u0639\u0645\u0644.", "standard");
            Add(cases, "standard-6", "en", "ar", "Please close the window before you leave.",
                "\u064a\u0631\u062c\u0649 \u0625\u063a\u0644\u0627\u0642 \u0627\u0644\u0646\u0627\u0641\u0630\u0629 \u0642\u0628\u0644 \u0623\u0646 \u062a\u063a\u0627\u062f\u0631.", "standard");
            Add(cases, "standard-7", "ar", "en", "\u0623\u062d\u062a\u0627\u062c \u0625\u0644\u0649 \u0634\u0631\u0627\u0621 \u0628\u0639\u0636 \u0627\u0644\u062e\u0628\u0632 \u0645\u0646 \u0627\u0644\u0633\u0648\u0642.",
                "I need to buy some bread from the market.", "standard");
            Add(cases, "standard-8", "en", "es", "It's raining, so take an umbrella.",
                "Est\u00e1 lloviendo, as\u00ed que lleva un paraguas.", "standard");
            Add(cases, "standard-9", "en", "ar", "The meeting starts at nine in the morning.",
                "\u064a\u0628\u062f\u0623 \u0627\u0644\u0627\u062c\u062a\u0645\u0627\u0639 \u0641\u064a \u0627\u0644\u062a\u0627\u0633\u0639\u0629 \u0635\u0628\u0627\u062d\u064b\u0627.", "standard");
            Add(cases, "slang-1", "es", "en", "No mames, ese cabr\u00f3n me rob\u00f3 el chingado lugar.",
                "No way, that bastard stole my fucking spot.", "slang");
            Add(cases, "slang-2", "en", "ar", "Shut the fuck up, don't fuck with me.",
                "\u0627\u062e\u0631\u0633\u060c \u0644\u0627 \u062a\u0644\u0639\u0628 \u0645\u0639\u064a.", "slang");
            Add(cases, "slang-3", "es", "en", "\u00a1No manches, qu\u00e9 caro!",
                "No way, that's expensive!", "slang");
            Add(cases, "slang-4", "es", "en", "Ese g\u00fcey es bien chido.",
                "That dude is really cool.", "slang");
            Add(cases, "dialect-1", "ar", "en", "\u0634\u0648 \u0623\u062e\u0628\u0627\u0631\u0643 \u0627\u0644\u064a\u0648\u0645\u061f",
                "How are you today?", "dialect");
            Add(cases, "dialect-2", "ar", "en", "\u0625\u064a\u0647 \u062f\u0647\u061f",
                "What's this?", "dialect");
            Add(cases, "dialect-3", "ar", "en", "\u0648\u064a\u0646 \u0631\u0627\u064a\u062d\u061f",
                "Where are you going?", "dialect");
            Add(cases, "dialect-4", "ar", "en", "\u0645\u0639\u0644\u0634\u060c \u062d\u0635\u0644 \u062e\u064a\u0631.",
                "Never mind, it turned out fine.", "dialect");
            return cases;
        }

        private static void Add(List<CompareCase> cases, string id, string source, string target,
            string text, string reference, string category)
        {
            CompareCase item = new CompareCase();
            item.Id = id;
            item.Source = source;
            item.Target = target;
            item.Text = text;
            item.Reference = reference;
            item.Category = category;
            cases.Add(item);
        }

        private static string BuildMarkdown(List<CompareResult> results, string endpoint, string model, string resolvedModel)
        {
            StringBuilder md = new StringBuilder();
            md.AppendLine("# Qalam online vs offline translation quality");
            md.AppendLine();
            md.AppendLine("Generated by `Qalam.exe --compare` on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ".");
            md.AppendLine();
            md.AppendLine("- Both paths use the curated phrasebook first; that is why known slang and dialect lines can take 1-20 ms in either mode.");
            md.AppendLine("- Online: Google endpoints with MyMemory fallback (mode `online`).");
            md.AppendLine("- Offline: local Ollama at `" + endpoint + "` (mode `offline`).");
            md.AppendLine("- Model: " + (string.IsNullOrEmpty(resolvedModel)
                ? (string.IsNullOrEmpty(model) ? "auto-selected (unresolved)" : "`" + model + "` (requested)")
                : "`" + resolvedModel + "`" + (string.IsNullOrEmpty(model) ? " (auto-selected)" : "")) + ".");
            md.AppendLine("- Score: token F1 against an authored reference. It rewards literal overlap and is not a human adequacy score.");
            md.AppendLine();

            double onlineF1 = 0;
            double offlineF1 = 0;
            long onlineMs = 0;
            long offlineMs = 0;
            int onlineScript = 0;
            int offlineScript = 0;
            int onlineOk = 0;
            int offlineOk = 0;
            foreach (CompareResult result in results)
            {
                onlineF1 += result.OnlineF1;
                offlineF1 += result.OfflineF1;
                onlineMs += result.OnlineMs;
                offlineMs += result.OfflineMs;
                if (result.OnlineScript) onlineScript++;
                if (result.OfflineScript) offlineScript++;
                if (string.IsNullOrEmpty(result.OnlineError) && result.OnlineText.Length > 0) onlineOk++;
                if (string.IsNullOrEmpty(result.OfflineError) && result.OfflineText.Length > 0) offlineOk++;
            }
            int count = Math.Max(1, results.Count);
            md.AppendLine("## Summary");
            md.AppendLine();
            md.AppendLine("| Path | Mean token F1 | Script-correct | Usable | Mean latency |");
            md.AppendLine("| --- | ---: | ---: | ---: | ---: |");
            md.AppendLine("| Online | " + (onlineF1 / count).ToString("0.000") + " | " + onlineScript + "/" + results.Count +
                          " | " + onlineOk + "/" + results.Count + " | " + (onlineMs / count) + " ms |");
            md.AppendLine("| Offline | " + (offlineF1 / count).ToString("0.000") + " | " + offlineScript + "/" + results.Count +
                          " | " + offlineOk + "/" + results.Count + " | " + (offlineMs / count) + " ms |");
            md.AppendLine();

            md.AppendLine("## Results");
            md.AppendLine();
            md.AppendLine("| Case | Pair | Source | Reference | Online | F1 | Offline | F1 |");
            md.AppendLine("| --- | --- | --- | --- | --- | ---: | --- | ---: |");
            foreach (CompareResult result in results)
            {
                md.Append("| ").Append(result.Case.Id).Append(" | ")
                  .Append(result.Case.Source).Append(" -> ").Append(result.Case.Target).Append(" | ")
                  .Append(Cell(result.Case.Text)).Append(" | ")
                  .Append(Cell(result.Case.Reference)).Append(" | ")
                  .Append(Cell(Format(result.OnlineText, result.OnlineError))).Append(" | ")
                  .Append(result.OnlineF1.ToString("0.00")).Append(" | ")
                  .Append(Cell(Format(result.OfflineText, result.OfflineError))).Append(" | ")
                  .Append(result.OfflineF1.ToString("0.00")).AppendLine(" |");
            }
            md.AppendLine();
            md.AppendLine("## Reading the numbers");
            md.AppendLine();
            md.AppendLine("Online is usually more fluent on standard prose. The offline path is deterministic once the model is loaded, keeps text on this machine, and follows the curated phrasebook for known slang and dialect expressions. Its weakness is vocabulary and dialect coverage outside that table, and CPU latency is much higher.");
            md.AppendLine();
            md.AppendLine("This set is small and the references are authored for Qalam, so treat the differences as directional. Use the visible outputs above to judge register, not only the F1 column.");
            return md.ToString();
        }

        private static string BuildJson(List<CompareResult> results)
        {
            List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
            foreach (CompareResult result in results)
            {
                Dictionary<string, object> row = new Dictionary<string, object>();
                row["id"] = result.Case.Id;
                row["source"] = result.Case.Source;
                row["target"] = result.Case.Target;
                row["category"] = result.Case.Category;
                row["text"] = result.Case.Text;
                row["reference"] = result.Case.Reference;
                row["online"] = result.OnlineText;
                row["online_error"] = result.OnlineError;
                row["online_engine"] = result.OnlineEngine;
                row["online_ms"] = result.OnlineMs;
                row["online_f1"] = result.OnlineF1;
                row["offline"] = result.OfflineText;
                row["offline_error"] = result.OfflineError;
                row["offline_engine"] = result.OfflineEngine;
                row["offline_ms"] = result.OfflineMs;
                row["offline_f1"] = result.OfflineF1;
                rows.Add(row);
            }
            return new JavaScriptSerializer().Serialize(rows);
        }

        private static string Format(string text, string error)
        {
            if (!string.IsNullOrEmpty(error)) return "ERROR: " + error;
            return string.IsNullOrEmpty(text) ? "(empty)" : text;
        }

        private static string Cell(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }

        private static double Score(string hypothesis, string reference)
        {
            List<string> a = Tokens(hypothesis);
            List<string> b = Tokens(reference);
            if (a.Count == 0 || b.Count == 0) return 0;
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string token in b)
            {
                int n;
                counts.TryGetValue(token, out n);
                counts[token] = n + 1;
            }
            int common = 0;
            foreach (string token in a)
            {
                int n;
                if (!counts.TryGetValue(token, out n) || n <= 0) continue;
                common++;
                counts[token] = n - 1;
            }
            double precision = (double)common / a.Count;
            double recall = (double)common / b.Count;
            return precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);
        }

        private static List<string> Tokens(string text)
        {
            List<string> tokens = new List<string>();
            if (string.IsNullOrEmpty(text)) return tokens;
            string normalized = ArabicText.StripDiacritics(text).ToLowerInvariant().Normalize(NormalizationForm.FormKC);
            StringBuilder word = new StringBuilder();
            foreach (char c in normalized)
            {
                if (char.IsLetterOrDigit(c))
                {
                    word.Append(c);
                    continue;
                }
                if (word.Length > 0)
                {
                    tokens.Add(word.ToString());
                    word.Length = 0;
                }
            }
            if (word.Length > 0) tokens.Add(word.ToString());
            return tokens;
        }

        private static bool ScriptMatches(string text, string target)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string code = target == null ? "" : target.ToLowerInvariant();
            if (code.StartsWith("ar") || code == "fa" || code == "ur") return ArabicText.ContainsArabic(text);
            if (code == "he") return HasRange(text, 0x0590, 0x05FF);
            if (code == "ru" || code == "uk") return HasRange(text, 0x0400, 0x04FF);
            if (code == "el") return HasRange(text, 0x0370, 0x03FF);
            if (code == "hi") return HasRange(text, 0x0900, 0x097F);
            if (code == "th") return HasRange(text, 0x0E00, 0x0E7F);
            if (code == "ko") return HasRange(text, 0xAC00, 0xD7AF);
            if (code == "ja") return HasRange(text, 0x3040, 0x30FF) || HasRange(text, 0x4E00, 0x9FFF);
            if (code == "zh") return HasRange(text, 0x4E00, 0x9FFF);
            return HasRange(text, 0x0041, 0x024F) || HasRange(text, 0x1E00, 0x1EFF);
        }

        private static bool HasRange(string text, int start, int end)
        {
            foreach (char c in text)
                if (c >= start && c <= end) return true;
            return false;
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
