// Document translation for Qalam: DOCX through WordprocessingML, PDF through
// the bundled PyMuPDF geometry bridge. Original files are never overwritten.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using WinForms = System.Windows.Forms;

namespace Qalam
{
    internal sealed class DocumentProgress
    {
        public int Done;
        public int Total;
        public string Stage;
        public string Detail;

        public int Percent
        {
            get
            {
                if (Total <= 0) return 0;
                int value = (int)Math.Round(Done * 100.0 / Total);
                if (value < 0) return 0;
                if (value > 100) return 100;
                return value;
            }
        }
    }

    internal sealed class DocumentTranslationReport
    {
        public string Input;
        public string Output;
        public string Source;
        public string Target;
        public string Format;
        public int Pages;
        public int Units;
        public int Translated;
        public int Skipped;
        public List<string> Warnings = new List<string>();
        public TimeSpan Elapsed;

        public string UnitLabel
        {
            get { return Format == "pdf" ? "text blocks" : "paragraphs"; }
        }
    }

    internal sealed class DocumentSegment
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Source { get; set; }
        public string Translation { get; set; }
    }

    internal sealed class DocumentTranslationException : Exception
    {
        public DocumentTranslationException(string message) : base(message) { }
        public DocumentTranslationException(string message, Exception inner) : base(message, inner) { }
    }

    internal sealed class DocxTranslationResult
    {
        public int Total;
        public int Translated;
    }

    internal sealed class DocumentTextTranslator
    {
        private const int ChunkChars = 1600;
        private readonly TranslationService _translator;
        private readonly Dictionary<string, string> _cache = new Dictionary<string, string>(StringComparer.Ordinal);

        public DocumentTextTranslator(TranslationService translator)
        {
            _translator = translator;
        }

        public async Task<string> TranslateAsync(string text, string target, CancellationToken token)
        {
            string clean = Normalize(text);
            if (string.IsNullOrWhiteSpace(clean) || !HasLetters(clean)) return text;

            string key = target + "\u0001" + clean;
            if (_cache.ContainsKey(key)) return _cache[key];

            List<string> chunks = Split(clean, ChunkChars);
            StringBuilder builder = new StringBuilder();
            foreach (string chunk in chunks)
            {
                token.ThrowIfCancellationRequested();
                string translated = await TranslateChunkAsync(chunk, target, token).ConfigureAwait(false);
                if (builder.Length > 0) builder.Append(' ');
                builder.Append(translated.Trim());
                await Task.Delay(90, token).ConfigureAwait(false);
            }

            string result = Collapse(builder.ToString());
            _cache[key] = result;
            return result;
        }

        private async Task<string> TranslateChunkAsync(string text, string target, CancellationToken token)
        {
            Exception last = null;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    TranslationOutcome outcome = await _translator.TranslateAsync(text, target).ConfigureAwait(false);
                    if (outcome != null && !string.IsNullOrWhiteSpace(outcome.Text)) return outcome.Text;
                    last = new InvalidOperationException("The translation service returned no text.");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    last = ex;
                }

                if (attempt < 3)
                {
                    Logger.Write("document translate retry " + (attempt + 1) + ": " + (last == null ? "empty" : last.Message));
                    await Task.Delay(700 * (attempt + 1), token).ConfigureAwait(false);
                }
            }
            throw new DocumentTranslationException(
                "Translation failed after four attempts. " + (last == null ? "" : last.Message), last);
        }

        public static bool HasLetters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if (char.IsLetter(c)) return true;
            }
            return false;
        }

        public static string DetectScript(string text)
        {
            if (string.IsNullOrEmpty(text)) return "en";
            int arabic = 0;
            int latin = 0;
            foreach (char c in text)
            {
                if ((c >= '\u0600' && c <= '\u06FF') || (c >= '\u0750' && c <= '\u077F') ||
                    (c >= '\uFB50' && c <= '\uFDFF') || (c >= '\uFE70' && c <= '\uFEFF'))
                    arabic++;
                else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) latin++;
            }
            return arabic > latin ? "ar" : "en";
        }

        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string value = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\t', ' ').Replace('\u00AD', '\0');
            value = value.Replace("\0", "");
            StringBuilder builder = new StringBuilder(value.Length);
            bool space = false;
            foreach (char c in value)
            {
                if (char.IsWhiteSpace(c))
                {
                    space = true;
                    continue;
                }
                if (space && builder.Length > 0) builder.Append(' ');
                space = false;
                builder.Append(c);
            }
            return builder.ToString().Trim();
        }

        private static string Collapse(string text)
        {
            StringBuilder builder = new StringBuilder();
            bool space = false;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    space = true;
                    continue;
                }
                if (space && builder.Length > 0) builder.Append(' ');
                space = false;
                builder.Append(c);
            }
            return builder.ToString().Trim();
        }

        private static List<string> Split(string text, int max)
        {
            List<string> chunks = new List<string>();
            string remaining = text.Trim();
            while (remaining.Length > max)
            {
                int cut = FindCut(remaining, max);
                if (cut < max / 3) cut = max;
                if (char.IsHighSurrogate(remaining[cut - 1])) cut--;
                if (cut <= 0) cut = Math.Min(max, remaining.Length);
                chunks.Add(remaining.Substring(0, cut).Trim());
                remaining = remaining.Substring(cut).TrimStart();
            }
            if (remaining.Length > 0) chunks.Add(remaining);
            return chunks;
        }

        private static int FindCut(string text, int max)
        {
            char[] stops = new char[] { '.', '!', '?', ';', '\u3002', '\uFF01', '\uFF1F', '\u061F' };
            for (int i = max; i > max / 3; i--)
            {
                if (Array.IndexOf(stops, text[i - 1]) >= 0 &&
                    (i == text.Length || char.IsWhiteSpace(text[i])))
                    return i;
            }
            for (int i = max; i > max / 3; i--)
            {
                if (char.IsWhiteSpace(text[i - 1])) return i;
            }
            return max;
        }
    }

    internal sealed class DocxDocumentTranslator
    {
        private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        private static readonly XName[] ParagraphPropertyOrder = new XName[]
        {
            W + "pStyle", W + "keepNext", W + "keepLines", W + "pageBreakBefore", W + "framePr",
            W + "widowControl", W + "numPr", W + "suppressLineNumbers", W + "pBdr", W + "shd",
            W + "tabs", W + "suppressAutoHyphens", W + "kinsoku", W + "wordWrap", W + "overflowPunct",
            W + "topLinePunct", W + "autoSpaceDE", W + "autoSpaceDN", W + "bidi", W + "adjustRightInd",
            W + "snapToGrid", W + "spacing", W + "ind", W + "contextualSpacing", W + "mirrorIndents",
            W + "suppressOverlap", W + "jc", W + "textDirection", W + "textAlignment", W + "textboxTightWrap",
            W + "outlineLvl", W + "divId", W + "cnfStyle", W + "rPr", W + "sectPr", W + "pPrChange"
        };
        private static readonly XName[] RunPropertyOrder = new XName[]
        {
            W + "rStyle", W + "rFonts", W + "b", W + "bCs", W + "i", W + "iCs", W + "caps",
            W + "smallCaps", W + "strike", W + "dstrike", W + "outline", W + "shadow", W + "emboss",
            W + "imprint", W + "noProof", W + "snapToGrid", W + "vanish", W + "webHidden", W + "color",
            W + "spacing", W + "w", W + "kern", W + "position", W + "sz", W + "szCs", W + "highlight",
            W + "u", W + "effect", W + "bdr", W + "shd", W + "fitText", W + "vertAlign", W + "rtl",
            W + "cs", W + "em", W + "lang", W + "eastAsianLayout", W + "specVanish", W + "oMath"
        };

        private sealed class RunInfo
        {
            public XElement Run;
            public List<XElement> TextNodes;
            public string Text;
            public string FormatKey;
            public int Words;
        }

        private sealed class ParagraphInfo
        {
            public XElement Paragraph;
            public string Text;
            public List<RunInfo> Runs;
            public string Translation;
        }

        public static string DetectSource(string path)
        {
            using (ZipArchive archive = ZipFile.OpenRead(path))
            {
                ZipArchiveEntry entry = FindPart(archive, "word/document.xml");
                if (entry == null) throw new DocumentTranslationException("This file is not a readable .docx document.");
                XDocument document = LoadXml(entry);
                StringBuilder text = new StringBuilder();
                foreach (XElement node in document.Descendants(W + "t"))
                {
                    text.Append(node.Value);
                    text.Append(' ');
                    if (text.Length > 60000) break;
                }
                return DocumentTextTranslator.DetectScript(text.ToString());
            }
        }

        private readonly TranslationService _translator;

        public DocxDocumentTranslator(TranslationService translator)
        {
            _translator = translator;
        }

        public async Task<DocxTranslationResult> TranslateAsync(string input, string tempOutput, string target,
            IProgress<DocumentProgress> progress, CancellationToken token,
            Func<IList<DocumentSegment>, bool> review = null)
        {
            File.Copy(input, tempOutput, true);

            using (ZipArchive archive = ZipFile.Open(tempOutput, ZipArchiveMode.Update))
            {
                List<KeyValuePair<string, XDocument>> parts = new List<KeyValuePair<string, XDocument>>();
                foreach (ZipArchiveEntry entry in archive.Entries.ToList())
                {
                    if (!IsTextPart(entry.FullName)) continue;
                    parts.Add(new KeyValuePair<string, XDocument>(entry.FullName, LoadXml(entry)));
                }

                if (!parts.Any(p => string.Equals(p.Key, "word/document.xml", StringComparison.OrdinalIgnoreCase)))
                    throw new DocumentTranslationException("This .docx has no Word document part.");

                List<KeyValuePair<string, List<ParagraphInfo>>> prepared =
                    new List<KeyValuePair<string, List<ParagraphInfo>>>();
                int total = 0;
                foreach (KeyValuePair<string, XDocument> part in parts)
                {
                    List<ParagraphInfo> paragraphs = CollectParagraphs(part.Value);
                    prepared.Add(new KeyValuePair<string, List<ParagraphInfo>>(part.Key, paragraphs));
                    total += paragraphs.Count;
                }

                if (total == 0)
                    throw new DocumentTranslationException("No translatable paragraphs were found in this document.");

                DocumentTextTranslator translator = new DocumentTextTranslator(_translator ?? new TranslationService());
                int done = 0;
                int processed = 0;
                List<ParagraphInfo> translatedParagraphs = new List<ParagraphInfo>();
                foreach (KeyValuePair<string, List<ParagraphInfo>> part in prepared)
                {
                    foreach (ParagraphInfo paragraph in part.Value)
                    {
                        token.ThrowIfCancellationRequested();
                        try
                        {
                            string translated = await translator.TranslateAsync(paragraph.Text, target, token).ConfigureAwait(false);
                            if (!string.IsNullOrWhiteSpace(translated))
                            {
                                paragraph.Translation = translated;
                                translatedParagraphs.Add(paragraph);
                                done++;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Logger.Write("docx paragraph skipped: " + ex.Message);
                        }

                        processed++;
                        DocumentProgress update = new DocumentProgress();
                        update.Done = processed;
                        update.Total = total;
                        update.Stage = "Translating " + Path.GetFileName(input);
                        update.Detail = processed + " of " + total + " paragraphs";
                        if (progress != null) progress.Report(update);
                    }
                }

                if (done == 0)
                    throw new DocumentTranslationException("None of the paragraphs could be translated. Check your connection and try again.");

                // Held back until review: the document parts are only written
                // after the user has had a chance to edit each passage.
                if (review != null)
                {
                    List<DocumentSegment> segments = new List<DocumentSegment>();
                    for (int i = 0; i < translatedParagraphs.Count; i++)
                    {
                        DocumentSegment segment = new DocumentSegment();
                        segment.Id = i.ToString();
                        segment.Label = "Paragraph " + (i + 1);
                        segment.Source = translatedParagraphs[i].Text;
                        segment.Translation = translatedParagraphs[i].Translation;
                        segments.Add(segment);
                    }

                    if (!review(segments)) throw new OperationCanceledException(token);

                    done = 0;
                    for (int i = 0; i < translatedParagraphs.Count; i++)
                    {
                        string edited = segments[i].Translation == null ? "" : segments[i].Translation.Trim();
                        translatedParagraphs[i].Translation = edited;
                        if (edited.Length > 0) done++;
                    }
                    if (done == 0)
                        throw new DocumentTranslationException("Every translation was cleared in review. The original file was not changed.");
                }

                foreach (ParagraphInfo paragraph in translatedParagraphs)
                {
                    if (string.IsNullOrWhiteSpace(paragraph.Translation)) continue;
                    ApplyTranslation(paragraph, paragraph.Translation, target);
                }

                foreach (KeyValuePair<string, XDocument> part in parts)
                {
                    ZipArchiveEntry existing = FindPart(archive, part.Key);
                    if (existing == null) continue;
                    string name = existing.FullName;
                    existing.Delete();
                    ZipArchiveEntry replacement = archive.CreateEntry(name, CompressionLevel.Optimal);
                    using (Stream stream = replacement.Open())
                    using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings
                    {
                        Encoding = new UTF8Encoding(false),
                        Indent = false,
                        OmitXmlDeclaration = false,
                        CloseOutput = false
                    }))
                    {
                        part.Value.Save(writer);
                    }
                }

                DocxTranslationResult result = new DocxTranslationResult();
                result.Total = total;
                result.Translated = done;
                return result;
            }
        }

        private static List<ParagraphInfo> CollectParagraphs(XDocument document)
        {
            List<ParagraphInfo> paragraphs = new List<ParagraphInfo>();
            foreach (XElement paragraph in document.Descendants(W + "p"))
            {
                List<RunInfo> runs = new List<RunInfo>();
                foreach (XElement run in paragraph.Descendants(W + "r"))
                {
                    if (run.Ancestors(W + "p").FirstOrDefault() != paragraph) continue;
                    List<XElement> nodes = run.Elements(W + "t").ToList();
                    if (nodes.Count == 0) continue;
                    string text = string.Concat(nodes.Select(n => n.Value));
                    if (text.Length == 0) continue;
                    XElement properties = run.Element(W + "rPr");
                    RunInfo info = new RunInfo();
                    info.Run = run;
                    info.TextNodes = nodes;
                    info.Text = text;
                    info.FormatKey = properties == null ? "" : properties.ToString(SaveOptions.DisableFormatting);
                    info.Words = Regex.Matches(text, @"\S+").Count;
                    runs.Add(info);
                }

                if (runs.Count == 0) continue;
                string paragraphText = DocumentTextTranslator.Normalize(string.Concat(runs.Select(r => r.Text)));
                if (!DocumentTextTranslator.HasLetters(paragraphText)) continue;

                ParagraphInfo paragraphInfo = new ParagraphInfo();
                paragraphInfo.Paragraph = paragraph;
                paragraphInfo.Text = paragraphText;
                paragraphInfo.Runs = runs;
                paragraphs.Add(paragraphInfo);
            }
            return paragraphs;
        }

        private static void ApplyTranslation(ParagraphInfo paragraph, string translated, string target)
        {
            bool allSame = paragraph.Runs.All(r => string.Equals(r.FormatKey, paragraph.Runs[0].FormatKey, StringComparison.Ordinal));
            if (paragraph.Runs.Count == 1 || allSame)
            {
                SetRunText(paragraph.Runs[0], translated);
                for (int i = 1; i < paragraph.Runs.Count; i++) SetRunText(paragraph.Runs[i], "");
            }
            else
            {
                string[] words = translated.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int sourceWords = paragraph.Runs.Sum(r => r.Words);
                int assigned = 0;
                for (int i = 0; i < paragraph.Runs.Count; i++)
                {
                    RunInfo run = paragraph.Runs[i];
                    int count;
                    if (i == paragraph.Runs.Count - 1)
                    {
                        count = words.Length - assigned;
                    }
                    else if (sourceWords <= 0)
                    {
                        count = 0;
                    }
                    else
                    {
                        count = (int)Math.Round(words.Length * (double)run.Words / sourceWords);
                        if (count > words.Length - assigned) count = words.Length - assigned;
                        if (count < 0) count = 0;
                    }

                    StringBuilder part = new StringBuilder();
                    for (int w = 0; w < count; w++)
                    {
                        if (part.Length > 0) part.Append(' ');
                        part.Append(words[assigned + w]);
                    }
                    if (assigned + count < words.Length && part.Length > 0) part.Append(' ');
                    SetRunText(run, part.ToString());
                    assigned += count;
                }
            }

            ApplyDirection(paragraph, target);
        }

        private static void ApplyDirection(ParagraphInfo paragraph, string target)
        {
            bool rtl = IsRtl(target);
            XElement pPr = paragraph.Paragraph.Element(W + "pPr");
            if (rtl)
            {
                if (pPr == null)
                {
                    pPr = new XElement(W + "pPr");
                    paragraph.Paragraph.AddFirst(pPr);
                }
                if (pPr.Element(W + "bidi") == null)
                    InsertOrdered(pPr, new XElement(W + "bidi"), ParagraphPropertyOrder);
            }
            else if (pPr != null)
            {
                XElement bidi = pPr.Element(W + "bidi");
                if (bidi != null) bidi.Remove();
            }

            XElement alignment = pPr == null ? null : pPr.Element(W + "jc");
            if (alignment != null)
            {
                string value = (string)alignment.Attribute(W + "val");
                if (rtl && string.Equals(value, "left", StringComparison.OrdinalIgnoreCase))
                    alignment.SetAttributeValue(W + "val", "right");
                else if (!rtl && string.Equals(value, "right", StringComparison.OrdinalIgnoreCase))
                    alignment.SetAttributeValue(W + "val", "left");
            }

            foreach (RunInfo run in paragraph.Runs)
            {
                XElement rPr = run.Run.Element(W + "rPr");
                if (rtl)
                {
                    if (rPr == null)
                    {
                        rPr = new XElement(W + "rPr");
                        run.Run.AddFirst(rPr);
                    }
                    if (rPr.Element(W + "rtl") == null)
                        InsertOrdered(rPr, new XElement(W + "rtl"), RunPropertyOrder);
                    XElement size = rPr.Element(W + "sz");
                    if (size != null && rPr.Element(W + "szCs") == null)
                    {
                        string sizeValue = (string)size.Attribute(W + "val");
                        if (!string.IsNullOrEmpty(sizeValue))
                            InsertOrdered(rPr, new XElement(W + "szCs", new XAttribute(W + "val", sizeValue)), RunPropertyOrder);
                    }
                    XElement fonts = rPr.Element(W + "rFonts");
                    if (fonts != null && fonts.Attribute(W + "cs") == null)
                    {
                        string ascii = (string)fonts.Attribute(W + "ascii");
                        if (!string.IsNullOrEmpty(ascii)) fonts.SetAttributeValue(W + "cs", ascii);
                    }
                }
                else if (rPr != null)
                {
                    XElement direction = rPr.Element(W + "rtl");
                    if (direction != null) direction.Remove();
                }
            }
        }

        private static void SetRunText(RunInfo run, string text)
        {
            if (run.TextNodes.Count == 0) return;
            for (int i = 1; i < run.TextNodes.Count; i++) run.TextNodes[i].Remove();
            XElement node = run.TextNodes[0];
            node.Value = text;
            bool preserve = text.Length > 0 &&
                (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[text.Length - 1]));
            node.SetAttributeValue(XNamespace.Xml + "space", preserve ? "preserve" : null);
        }

        private static void InsertOrdered(XElement parent, XElement child, XName[] order)
        {
            int index = Array.IndexOf(order, child.Name);
            if (index < 0)
            {
                parent.Add(child);
                return;
            }

            XElement before = parent.Elements().FirstOrDefault(e =>
            {
                int candidate = Array.IndexOf(order, e.Name);
                return candidate > index;
            });
            if (before != null) before.AddBeforeSelf(child);
            else parent.Add(child);
        }

        private static bool IsRtl(string target)
        {
            return string.Equals(target, "ar", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(target, "fa", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(target, "ur", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(target, "he", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTextPart(string name)
        {
            if (name.StartsWith("word/header", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("word/footer", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) return true;
            return string.Equals(name, "word/document.xml", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "word/footnotes.xml", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "word/endnotes.xml", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "word/comments.xml", StringComparison.OrdinalIgnoreCase);
        }

        private static ZipArchiveEntry FindPart(ZipArchive archive, string name)
        {
            return archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, name, StringComparison.OrdinalIgnoreCase));
        }

        private static XDocument LoadXml(ZipArchiveEntry entry)
        {
            using (Stream stream = entry.Open())
            using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreWhitespace = false,
                CloseInput = false
            }))
            {
                return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            }
        }
    }
}
