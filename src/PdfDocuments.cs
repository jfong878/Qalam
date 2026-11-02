// PDF document translation through the PyMuPDF geometry bridge. Qalam
// translates text blocks; the bridge redacts the originals and places the
// translations back in the same rectangles.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Qalam
{
    internal sealed class PdfBlockInfo
    {
        public string Id { get; set; }
        public int Page { get; set; }
        public float[] Bbox { get; set; }
        public string Text { get; set; }
        public double Size { get; set; }
        public string Color { get; set; }
        public string Font { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Serif { get; set; }
        public bool Monospace { get; set; }
        public string Align { get; set; }
        public double LineHeight { get; set; }
    }

    internal sealed class PdfExtract
    {
        public int Pages { get; set; }
        public List<PdfBlockInfo> Blocks { get; set; }
        public List<string> Warnings { get; set; }

        public PdfExtract()
        {
            Blocks = new List<PdfBlockInfo>();
            Warnings = new List<string>();
        }
    }

    internal sealed class PdfDetect
    {
        public string Source { get; set; }
        public string Sample { get; set; }
    }

    internal sealed class PdfTranslationItem
    {
        public string Id { get; set; }
        public string Translation { get; set; }
    }

    internal sealed class PdfTranslationPayload
    {
        public string Target { get; set; }
        public List<PdfTranslationItem> Blocks { get; set; }

        public PdfTranslationPayload()
        {
            Blocks = new List<PdfTranslationItem>();
        }
    }

    internal sealed class PdfApplyResult
    {
        public int Inserted { get; set; }
        public List<string> Failed { get; set; }
    }

    internal sealed class PdfTranslationResult
    {
        public int Pages;
        public int Units;
        public int Translated;
        public int Skipped;
        public List<string> Warnings = new List<string>();
    }

    internal sealed class PdfDocumentTranslator
    {
        private static readonly object PythonGate = new object();
        private static string _pythonPath;

        private readonly Config _config;
        private readonly TranslationService _translator;

        public PdfDocumentTranslator(Config config, TranslationService translator)
        {
            _config = config;
            _translator = translator;
        }

        public async Task<string> DetectSourceAsync(string input, CancellationToken token)
        {
            string tempDir = NewTempDir();
            try
            {
                string output = Path.Combine(tempDir, "detect.json");
                await RunAsync("detect", new string[] { input, output }, token).ConfigureAwait(false);
                PdfDetect detect = new JavaScriptSerializer().Deserialize<PdfDetect>(File.ReadAllText(output, Encoding.UTF8));
                return detect != null && string.Equals(detect.Source, "ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";
            }
            finally
            {
                DeleteDirectory(tempDir);
            }
        }

        public async Task<PdfTranslationResult> TranslateAsync(string input, string output, string target,
            IProgress<DocumentProgress> progress, CancellationToken token)
        {
            if (!File.Exists(Paths.PdfScriptPath))
                throw new DocumentTranslationException("The PDF helper is missing. Reinstall Qalam or restore documents/pdf_translate.py.");

            string tempDir = NewTempDir();
            try
            {
                string blocksPath = Path.Combine(tempDir, "blocks.json");
                await RunAsync("extract", new string[] { input, blocksPath }, token).ConfigureAwait(false);
                PdfExtract extract = new JavaScriptSerializer().Deserialize<PdfExtract>(
                    File.ReadAllText(blocksPath, Encoding.UTF8));
                if (extract == null || extract.Blocks == null || extract.Blocks.Count == 0)
                    throw new DocumentTranslationException(
                        "This PDF has no selectable text. Scanned PDFs need OCR, which Qalam does not bundle.");

                PdfTranslationResult result = new PdfTranslationResult();
                result.Pages = extract.Pages;
                result.Units = extract.Blocks.Count;
                if (extract.Warnings != null) result.Warnings.AddRange(extract.Warnings);

                DocumentTextTranslator documentTranslator = new DocumentTextTranslator(_translator);
                List<PdfTranslationItem> translatedBlocks = new List<PdfTranslationItem>();
                int processed = 0;
                foreach (PdfBlockInfo block in extract.Blocks)
                {
                    token.ThrowIfCancellationRequested();
                    string translated = null;
                    if (!string.IsNullOrWhiteSpace(block.Text) && DocumentTextTranslator.HasLetters(block.Text))
                    {
                        try
                        {
                            translated = await documentTranslator.TranslateAsync(block.Text, target, token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            result.Warnings.Add("page " + (block.Page + 1) + ": block left in the original language (" + ex.Message + ")");
                            Logger.Write("pdf block " + block.Id + " skipped: " + ex.Message);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(translated))
                    {
                        PdfTranslationItem item = new PdfTranslationItem();
                        item.Id = block.Id;
                        item.Translation = translated;
                        translatedBlocks.Add(item);
                        result.Translated++;
                    }
                    else
                    {
                        result.Skipped++;
                    }

                    processed++;
                    DocumentProgress update = new DocumentProgress();
                    update.Done = processed;
                    update.Total = extract.Blocks.Count;
                    update.Stage = "Translating " + Path.GetFileName(input);
                    update.Detail = processed + " of " + extract.Blocks.Count + " text blocks";
                    if (progress != null) progress.Report(update);
                }

                if (translatedBlocks.Count == 0)
                    throw new DocumentTranslationException("No PDF text blocks could be translated. The original file was not changed.");

                PdfTranslationPayload payload = new PdfTranslationPayload();
                payload.Target = target;
                payload.Blocks = translatedBlocks;
                string translationsPath = Path.Combine(tempDir, "translations.json");
                File.WriteAllText(translationsPath, new JavaScriptSerializer().Serialize(payload), Utf8.NoBom);

                DocumentProgress applying = new DocumentProgress();
                applying.Done = extract.Blocks.Count;
                applying.Total = extract.Blocks.Count;
                applying.Stage = "Writing translated PDF";
                applying.Detail = "Restoring text into the original page layout";
                if (progress != null) progress.Report(applying);

                string applyOutput = await RunAsync("apply", new string[] { input, translationsPath, output }, token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(applyOutput))
                {
                    try
                    {
                        PdfApplyResult applied = new JavaScriptSerializer().Deserialize<PdfApplyResult>(applyOutput);
                        if (applied != null)
                        {
                            // The bridge reports how many translations actually
                            // landed. A passage that had to stay in its source
                            // language must not be counted as translated.
                            result.Translated = Math.Min(result.Translated, applied.Inserted);
                            result.Skipped = Math.Max(result.Skipped, result.Units - result.Translated);
                            if (applied.Failed != null)
                            {
                                foreach (string failure in applied.Failed)
                                    result.Warnings.Add("layout: " + failure);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Write("pdf apply result: " + ex.Message);
                    }
                }
                return result;
            }
            finally
            {
                DeleteDirectory(tempDir);
            }
        }

        private async Task<string> RunAsync(string command, string[] args, CancellationToken token)
        {
            string python = FindPython(_config);
            StringBuilder arguments = new StringBuilder();
            arguments.Append(Quote(Paths.PdfScriptPath));
            arguments.Append(' ');
            arguments.Append(command);
            foreach (string arg in args)
            {
                arguments.Append(' ');
                arguments.Append(Quote(arg));
            }

            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = python;
            start.Arguments = arguments.ToString();
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;
            start.WorkingDirectory = Path.GetDirectoryName(Paths.PdfScriptPath);

            using (Process process = new Process())
            {
                process.StartInfo = start;
                try
                {
                    if (!process.Start())
                        throw new DocumentTranslationException("Could not start the PDF helper.");
                }
                catch (Exception ex)
                {
                    throw new DocumentTranslationException("Could not start Python for PDF translation. " + ex.Message, ex);
                }

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                while (!process.WaitForExit(150))
                {
                    if (token.IsCancellationRequested)
                    {
                        try { process.Kill(); } catch { }
                        throw new OperationCanceledException(token);
                    }
                }
                process.WaitForExit();
                string stdout = await outputTask.ConfigureAwait(false);
                string stderr = await errorTask.ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    string message = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                    if (string.IsNullOrWhiteSpace(message)) message = "PDF helper exited with code " + process.ExitCode + ".";
                    throw new DocumentTranslationException(message.Trim());
                }
                return stdout.Trim();
            }
        }

        public static string FindPython(Config config)
        {
            lock (PythonGate)
            {
                if (!string.IsNullOrEmpty(_pythonPath)) return _pythonPath;

                List<string> candidates = new List<string>();
                if (config != null && !string.IsNullOrEmpty(config.PdfPythonPath)) candidates.Add(config.PdfPythonPath);
                if (config != null) candidates.Add(Paths.ResolvePython(config.PythonPath));
                candidates.Add("python.exe");
                candidates.Add("python3.exe");

                foreach (string candidate in candidates)
                {
                    if (string.IsNullOrWhiteSpace(candidate)) continue;
                    if (candidate.IndexOf(Path.DirectorySeparatorChar) >= 0 && !File.Exists(candidate)) continue;
                    if (ProbePython(candidate))
                    {
                        _pythonPath = candidate;
                        Logger.Write("pdf python: " + candidate);
                        return candidate;
                    }
                }
            }

            throw new DocumentTranslationException(
                "PDF translation needs Python 3 with PyMuPDF. Install it with: python -m pip install pymupdf");
        }

        private static bool ProbePython(string executable)
        {
            try
            {
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = executable;
                start.Arguments = "-c \"import pymupdf; print(pymupdf.__version__)\"";
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.StandardOutputEncoding = Encoding.UTF8;
                start.StandardErrorEncoding = Encoding.UTF8;
                using (Process process = Process.Start(start))
                {
                    if (process == null) return false;
                    Task<string> output = process.StandardOutput.ReadToEndAsync();
                    Task<string> error = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(5000))
                    {
                        try { process.Kill(); } catch { }
                        return false;
                    }
                    process.WaitForExit();
                    string version = output.Result;
                    return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(version);
                }
            }
            catch
            {
                return false;
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static string NewTempDir()
        {
            string path = Path.Combine(Path.GetTempPath(), "qalam-doc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch { }
        }
    }
}
