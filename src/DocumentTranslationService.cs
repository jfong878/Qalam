// Chooses the DOCX or PDF path, keeps the original untouched, and returns a
// report suitable for the tray result dialog.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Qalam
{
    internal sealed class DocumentTranslationService
    {
        private readonly Config _config;
        private readonly TranslationService _translator;

        public DocumentTranslationService(Config config, TranslationService translator)
        {
            _config = config;
            _translator = translator;
        }

        public static bool Supports(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".docx" || extension == ".pdf";
        }

        public async Task<string> DetectSourceAsync(string input, CancellationToken token)
        {
            input = Path.GetFullPath(input);
            string extension = Path.GetExtension(input).ToLowerInvariant();
            if (extension == ".docx")
            {
                return await Task.Run(() => DocxDocumentTranslator.DetectSource(input), token).ConfigureAwait(false);
            }
            if (extension == ".pdf")
            {
                return await new PdfDocumentTranslator(_config, _translator)
                    .DetectSourceAsync(input, token).ConfigureAwait(false);
            }
            throw new DocumentTranslationException("Qalam can translate .docx and .pdf documents.");
        }

        public async Task<string> ResolveTargetAsync(string input, CancellationToken token)
        {
            if (!_config.AutoTarget) return _config.Target;
            string source = await DetectSourceAsync(input, token).ConfigureAwait(false);
            return string.Equals(source, "ar", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";
        }

        public async Task<DocumentTranslationReport> TranslateAsync(string input, string output, string target,
            string source, IProgress<DocumentProgress> progress, CancellationToken token,
            Func<IList<DocumentSegment>, bool> review = null)
        {
            input = Path.GetFullPath(input);
            output = Path.GetFullPath(output);
            if (!File.Exists(input)) throw new DocumentTranslationException("The input document no longer exists.");
            if (string.Equals(Path.GetFullPath(input), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
                throw new DocumentTranslationException("Choose a different output file. Qalam never overwrites the original document.");
            if (!Supports(input)) throw new DocumentTranslationException("Qalam can translate .docx and .pdf documents.");

            Stopwatch watch = Stopwatch.StartNew();
            DocumentTranslationReport report = new DocumentTranslationReport();
            report.Input = input;
            report.Output = output;
            report.Target = target;
            report.Source = source;
            report.Format = Path.GetExtension(input).TrimStart('.').ToLowerInvariant();

            try
            {
                if (report.Format == "docx")
                {
                    string temp = output + "." + Guid.NewGuid().ToString("N") + ".qalam.tmp";
                    try
                    {
                        DocxTranslationResult docx = await new DocxDocumentTranslator(_translator)
                            .TranslateAsync(input, temp, target, progress, token, review).ConfigureAwait(false);
                        if (File.Exists(output)) File.Replace(temp, output, null);
                        else File.Move(temp, output);
                        report.Translated = docx.Translated;
                        report.Units = docx.Total;
                    }
                    finally
                    {
                        try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                    }
                }
                else
                {
                    PdfTranslationResult result = await new PdfDocumentTranslator(_config, _translator)
                        .TranslateAsync(input, output, target, progress, token, review).ConfigureAwait(false);
                    report.Translated = result.Translated;
                    report.Units = result.Units;
                    report.Pages = result.Pages;
                    report.Skipped = result.Skipped;
                    if (result.Warnings != null) report.Warnings.AddRange(result.Warnings);
                }

                report.Skipped = Math.Max(report.Skipped, report.Units - report.Translated);
                watch.Stop();
                report.Elapsed = watch.Elapsed;

                DocumentProgress finished = new DocumentProgress();
                finished.Done = report.Units;
                finished.Total = report.Units;
                finished.Stage = "Translated";
                finished.Detail = report.Translated + " " + report.UnitLabel + " written";
                if (progress != null) progress.Report(finished);
                return report;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DocumentTranslationException)
            {
                throw;
            }
            catch (InvalidDataException ex)
            {
                throw new DocumentTranslationException(
                    "This file is not a readable Office Open XML document. If it is an older .doc, save it as .docx first.", ex);
            }
            catch (Exception ex)
            {
                throw new DocumentTranslationException("Document translation failed: " + ex.Message, ex);
            }
        }

        public static string LanguageName(string code)
        {
            return LanguageCatalog.NameOf(code);
        }
    }
}
