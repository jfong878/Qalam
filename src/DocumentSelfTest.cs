// Headless verification path for document translation. This is intentionally
// separate from the popup selftest so failing network calls do not affect it.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Qalam
{
    internal static class DocumentSelfTest
    {
        public static int Run(string[] args)
        {
            string input = Arg(args, "--input");
            string output = Arg(args, "--output");
            string target = Arg(args, "--target");
            string reportPath = Arg(args, "--report");
            if (string.IsNullOrEmpty(reportPath)) reportPath = output + ".report.txt";

            StringBuilder log = new StringBuilder();
            int exitCode = 0;
            try
            {
                if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output))
                    throw new ArgumentException("--input and --output are required");
                if (string.IsNullOrEmpty(target)) target = "ar";

                Config config = new Config();
                TranslationService translator = new TranslationService();
                DocumentTranslationService service = new DocumentTranslationService(config, translator);
                string source = service.DetectSourceAsync(input, CancellationToken.None).GetAwaiter().GetResult();
                log.AppendLine("input=" + input);
                log.AppendLine("output=" + output);
                log.AppendLine("source=" + source);
                log.AppendLine("target=" + target);

                // A headless stand-in for the review dialog: it proves edits
                // survive into the written document without needing a UI.
                string reviewMarker = Arg(args, "--review-marker");
                Func<IList<DocumentSegment>, bool> review = null;
                if (!string.IsNullOrEmpty(reviewMarker))
                {
                    log.AppendLine("review_marker=" + reviewMarker);
                    review = delegate(IList<DocumentSegment> segments)
                    {
                        foreach (DocumentSegment segment in segments)
                            segment.Translation = (segment.Translation ?? "").TrimEnd() + " " + reviewMarker;
                        return true;
                    };
                }

                Stopwatch watch = Stopwatch.StartNew();
                InlineProgress progress = new InlineProgress(delegate(DocumentProgress p)
                {
                    log.AppendLine("progress=" + p.Percent + "% " + p.Detail);
                });
                DocumentTranslationReport report = service.TranslateAsync(
                    input, output, target, source, progress, CancellationToken.None, review).GetAwaiter().GetResult();
                watch.Stop();
                log.AppendLine("translated=" + report.Translated);
                log.AppendLine("units=" + report.Units);
                log.AppendLine("pages=" + report.Pages);
                log.AppendLine("skipped=" + report.Skipped);
                log.AppendLine("warnings=" + report.Warnings.Count);
                foreach (string warning in report.Warnings) log.AppendLine("warning=" + warning);
                log.AppendLine("elapsed_ms=" + watch.ElapsedMilliseconds);
                log.AppendLine("exists=" + File.Exists(output));
            }
            catch (Exception ex)
            {
                exitCode = 1;
                log.AppendLine("FAIL=" + ex);
            }

            string reportDir = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(reportDir)) Directory.CreateDirectory(reportDir);
            File.WriteAllText(reportPath, log.ToString(), Utf8.NoBom);
            try { Console.WriteLine(log.ToString()); } catch { }
            return exitCode;
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return null;
        }

        private sealed class InlineProgress : IProgress<DocumentProgress>
        {
            private readonly Action<DocumentProgress> _callback;

            public InlineProgress(Action<DocumentProgress> callback)
            {
                _callback = callback;
            }

            public void Report(DocumentProgress value)
            {
                if (_callback != null) _callback(value);
            }
        }
    }
}
