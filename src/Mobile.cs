// Qalam's local web/mobile surface.
//
// The desktop app hosts a small HTTP server on the LAN. The same responsive
// page works in a desktop browser and on a phone; the token in the URL is the
// only credential and is generated locally. No account or cloud backend exists.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace Qalam
{
    internal sealed class MobileServer : IDisposable
    {
        private static readonly object JsonGate = new object();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        private readonly TranslationService _translator;
        private readonly string _token;
        private TcpListener _listener;
        private Thread _thread;
        private volatile bool _running;
        private int _port;

        public MobileServer(TranslationService translator, int port, string token)
        {
            _translator = translator;
            _port = port;
            _token = string.IsNullOrEmpty(token) ? NewToken() : token;
        }

        public int Port { get { return _port; } }
        public string Token { get { return _token; } }

        public void Start(IPAddress bind)
        {
            if (_listener != null) return;
            _listener = new TcpListener(bind, _port);
            _listener.Start();
            _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _running = true;
            _thread = new Thread(AcceptLoop);
            _thread.IsBackground = true;
            _thread.Name = "Qalam mobile";
            _thread.Start();
            Logger.Write("mobile: listening port=" + _port);
        }

        public void Start()
        {
            Start(IPAddress.Any);
        }

        public string UrlFor(string host)
        {
            if (string.IsNullOrEmpty(host)) host = "localhost";
            return "http://" + host + ":" + _port + "/?token=" + _token;
        }

        public static string NewToken()
        {
            byte[] bytes = new byte[16];
            using (RNGCryptoServiceProvider random = new RNGCryptoServiceProvider())
                random.GetBytes(bytes);
            StringBuilder hex = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) hex.Append(value.ToString("x2"));
            return hex.ToString();
        }

        public static string LocalAddress()
        {
            try
            {
                IPAddress[] addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
                IPAddress fallback = null;
                foreach (IPAddress address in addresses)
                {
                    if (address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(address)) continue;
                    if (fallback == null) fallback = address;
                    byte[] bytes = address.GetAddressBytes();
                    if (bytes[0] == 10 ||
                        (bytes[0] == 192 && bytes[1] == 168) ||
                        (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31))
                        return address.ToString();
                }
                if (fallback != null) return fallback.ToString();
            }
            catch (Exception ex)
            {
                Logger.Write("mobile address: " + ex.Message);
            }
            return "localhost";
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client = null;
                try { client = _listener.AcceptTcpClient(); }
                catch (SocketException)
                {
                    if (!_running) break;
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!_running) break;
                    Logger.Write("mobile accept: " + ex.Message);
                    continue;
                }

                TcpClient captured = client;
                ThreadPool.QueueUserWorkItem(delegate(object state)
                {
                    try { Handle((TcpClient)state); }
                    catch (Exception ex) { Logger.Write("mobile request: " + ex.Message); }
                }, captured);
            }
        }

        private void Handle(TcpClient client)
        {
            using (client)
            {
                client.ReceiveTimeout = 15000;
                client.SendTimeout = 15000;
                NetworkStream stream = client.GetStream();
                MobileRequest request = ReadRequest(stream);
                if (request == null) return;
                try { Route(stream, request); }
                catch (Exception ex)
                {
                    Logger.Write("mobile route " + request.Path + ": " + ex.Message);
                    SendJson(stream, 500, Error("Request failed."));
                }
            }
        }

        private void Route(NetworkStream stream, MobileRequest request)
        {
            if (request.Method == "OPTIONS")
            {
                Send(stream, 204, "No Content", "text/plain; charset=utf-8", new byte[0], false);
                return;
            }

            string path = request.Path;
            if (path == "/" || path == "/index.html")
            {
                if (!Authorized(request))
                {
                    SendText(stream, 401, "Open the exact Qalam link, including its token.");
                    return;
                }
                ServeAsset(stream, "index.html", "text/html; charset=utf-8", true);
                return;
            }

            if (path == "/app.css") { ServeAsset(stream, "app.css", "text/css; charset=utf-8", false); return; }
            if (path == "/app.js") { ServeAsset(stream, "app.js", "application/javascript; charset=utf-8", false); return; }
            if (path == "/sw.js") { ServeAsset(stream, "sw.js", "application/javascript; charset=utf-8", false); return; }
            if (path == "/icon.svg") { ServeAsset(stream, "icon.svg", "image/svg+xml", false); return; }
            if (path == "/manifest.webmanifest" || path == "/manifest.json")
            {
                if (!Authorized(request)) { SendJson(stream, 401, Error("Token required.")); return; }
                ServeAsset(stream, "manifest.webmanifest", "application/manifest+json; charset=utf-8", true);
                return;
            }
            if (path == "/favicon.ico") { Send(stream, 204, "No Content", "image/x-icon", new byte[0], false); return; }

            if (path == "/download/windows")
            {
                if (!Authorized(request))
                {
                    SendText(stream, 401, "Include the Qalam token in the download link.");
                    return;
                }
                try
                {
                    string package = WindowsPackage.GetOrCreate();
                    SendFile(stream, package, "application/zip", "Qalam-Windows.zip");
                }
                catch (Exception ex)
                {
                    Logger.Write("windows package: " + ex.Message);
                    SendText(stream, 503, "The Windows package could not be created on this machine.");
                }
                return;
            }

            if (path.StartsWith("/api/", StringComparison.Ordinal))
            {
                if (!Authorized(request)) { SendJson(stream, 401, Error("Token required.")); return; }
                if (path == "/api/health") { SendJson(stream, 200, Health()); return; }
                if (path == "/api/languages") { SendJson(stream, 200, Languages()); return; }
                if (path == "/api/translate") { HandleTranslate(stream, request); return; }
                if (path == "/api/rewrite") { HandleRewrite(stream, request); return; }
                if (path == "/api/report") { HandleReport(stream, request); return; }
                if (path == "/api/corpus") { HandleCorpus(stream, request); return; }
                if (path == "/api/study") { HandleStudy(stream, request); return; }
                if (path == "/api/study/add") { HandleStudyAdd(stream, request); return; }
                if (path == "/api/study/grade") { HandleStudyGrade(stream, request); return; }
                if (path == "/api/study/import") { HandleStudyImport(stream); return; }
                SendJson(stream, 404, Error("Unknown API route."));
                return;
            }

            SendText(stream, 404, "Not found.");
        }

        private bool Authorized(MobileRequest request)
        {
            string supplied = null;
            request.Query.TryGetValue("token", out supplied);
            if (string.IsNullOrEmpty(supplied))
                request.Headers.TryGetValue("x-qalam-token", out supplied);
            if (string.IsNullOrEmpty(supplied)) return false;
            return string.Equals(supplied, _token, StringComparison.Ordinal);
        }

        private void HandleTranslate(NetworkStream stream, MobileRequest request)
        {
            Dictionary<string, object> body = Body(request);
            string text = Value(body, "text");
            string target = Value(body, "target");
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                SendJson(stream, 400, Error("Text is required."));
                return;
            }
            if (text.Length > 600)
            {
                SendJson(stream, 400, Error("Text is limited to 600 characters."));
                return;
            }
            if (string.IsNullOrEmpty(target) || string.Equals(target, "auto", StringComparison.OrdinalIgnoreCase))
            {
                string detected = Colloquial.DetectLanguage(text);
                target = string.Equals(detected, "ar", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";
            }
            if (!LanguageCatalog.Contains(target))
            {
                SendJson(stream, 400, Error("Unknown target language."));
                return;
            }

            TranslationOutcome outcome = _translator.TranslateAsync(text, target).GetAwaiter().GetResult();
            if (outcome == null || string.IsNullOrEmpty(outcome.Text))
            {
                SendJson(stream, 502, Error("No translation returned."));
                return;
            }
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["translation"] = outcome.Text;
            result["detected"] = outcome.Detected;
            result["target"] = target;
            result["engine"] = outcome.Engine;
            SendJson(stream, 200, result);
        }

        private void HandleRewrite(NetworkStream stream, MobileRequest request)
        {
            Dictionary<string, object> body = Body(request);
            string text = Value(body, "text");
            string language = Value(body, "language");
            string context = Value(body, "context");
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                SendJson(stream, 400, Error("Text is required."));
                return;
            }
            if (text.Length > 1200)
            {
                SendJson(stream, 400, Error("Text is limited to 1200 characters for wording suggestions."));
                return;
            }
            if (string.IsNullOrEmpty(language))
                language = ArabicText.ContainsArabic(text) ? "ar" : "en";

            TranslationOutcome outcome = _translator.SuggestWordingAsync(text, language, context)
                .GetAwaiter().GetResult();
            if (outcome == null || string.IsNullOrEmpty(outcome.Text))
            {
                string reason = _translator.LocalModelError;
                SendJson(stream, 503, Error("Wording suggestions need a local Ollama model." +
                    (string.IsNullOrEmpty(reason) ? "" : " " + reason)));
                return;
            }
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["suggestion"] = outcome.Text;
            result["language"] = language;
            result["engine"] = outcome.Engine;
            SendJson(stream, 200, result);
        }

        private void HandleReport(NetworkStream stream, MobileRequest request)
        {
            Dictionary<string, object> body = Body(request);
            string details = Value(body, "details");
            string steps = Value(body, "steps");
            string expected = Value(body, "expected");
            if (string.IsNullOrWhiteSpace(details))
            {
                SendJson(stream, 400, Error("Describe what happened first."));
                return;
            }
            if (details.Length > 4000 || (steps != null && steps.Length > 2000) ||
                (expected != null && expected.Length > 2000))
            {
                SendJson(stream, 400, Error("The bug report is too long."));
                return;
            }

            string report = BugReport.Build(Config.Load(), details, steps, expected);
            string path = BugReport.Save(report);
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["path"] = path;
            result["report"] = report;
            SendJson(stream, 200, result);
        }

        private void HandleCorpus(NetworkStream stream, MobileRequest request)
        {
            string query = Query(request, "query");
            string from = Query(request, "from");
            string to = Query(request, "to");
            string hint = Query(request, "hint");
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            {
                SendJson(stream, 400, Error("query, from, and to are required."));
                return;
            }
            List<ExamplePair> examples = ConcordanceStore.Shared.Search(query, from, to, 6, hint);
            List<object> rows = new List<object>();
            foreach (ExamplePair example in examples)
            {
                Dictionary<string, object> row = new Dictionary<string, object>();
                row["source"] = example.Source;
                row["target"] = example.Target;
                row["term"] = example.Term;
                row["origin"] = example.Origin;
                row["license"] = example.License;
                rows.Add(row);
            }
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["query"] = query;
            result["from"] = from;
            result["to"] = to;
            result["examples"] = rows;
            SendJson(stream, 200, result);
        }

        private void HandleStudy(NetworkStream stream, MobileRequest request)
        {
            string scope = Query(request, "scope");
            List<StudyCard> cards = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase)
                ? StudyStore.All() : StudyStore.Due();
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["stats"] = Stats(StudyStore.Stats());
            result["scope"] = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase) ? "all" : "due";
            List<object> rows = new List<object>();
            foreach (StudyCard card in cards) rows.Add(Card(card));
            result["cards"] = rows;
            SendJson(stream, 200, result);
        }

        private void HandleStudyAdd(NetworkStream stream, MobileRequest request)
        {
            Dictionary<string, object> body = Body(request);
            string source = Value(body, "source");
            string translation = Value(body, "translation");
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(translation))
            {
                SendJson(stream, 400, Error("source and translation are required."));
                return;
            }
            HistoryEntry entry = new HistoryEntry();
            entry.Source = source;
            entry.Translation = translation;
            entry.Detected = Value(body, "detected");
            entry.Target = Value(body, "target");
            entry.Time = DateTime.Now;
            int added = StudyStore.Add(entry);
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["added"] = added;
            result["stats"] = Stats(StudyStore.Stats());
            SendJson(stream, 200, result);
        }

        private void HandleStudyGrade(NetworkStream stream, MobileRequest request)
        {
            Dictionary<string, object> body = Body(request);
            string source = Value(body, "source");
            string target = Value(body, "target");
            string gradeText = Value(body, "grade");
            StudyGrade grade;
            if (!TryGrade(gradeText, out grade))
            {
                SendJson(stream, 400, Error("grade must be again, hard, good, or easy."));
                return;
            }

            StudyCard found = null;
            foreach (StudyCard card in StudyStore.All())
            {
                if (string.Equals(card.Source, source, StringComparison.Ordinal) &&
                    string.Equals(card.Target, target, StringComparison.Ordinal))
                {
                    found = card;
                    break;
                }
            }
            if (found == null)
            {
                SendJson(stream, 404, Error("Card not found."));
                return;
            }

            StudyCard graded = StudyStore.Grade(found, grade);
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["card"] = Card(graded);
            result["stats"] = Stats(StudyStore.Stats());
            SendJson(stream, 200, result);
        }

        private void HandleStudyImport(NetworkStream stream)
        {
            List<HistoryEntry> entries = new List<HistoryEntry>();
            entries.AddRange(FavoritesStore.Load());
            entries.AddRange(HistoryStore.Load());
            int added = StudyStore.Import(entries);
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["added"] = added;
            result["stats"] = Stats(StudyStore.Stats());
            SendJson(stream, 200, result);
        }

        private Dictionary<string, object> Health()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["ok"] = true;
            result["app"] = "Qalam";
            result["mobile"] = true;
            result["languages"] = LanguageCatalog.Count;
            result["corpusPairs"] = ConcordanceStore.Shared.PairCount;
            return result;
        }

        private Dictionary<string, object> Languages()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["count"] = LanguageCatalog.Count;
            result["languages"] = LanguageCatalog.Json();
            return result;
        }

        private static Dictionary<string, object> Stats(StudyStats stats)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["total"] = stats.Total;
            result["due"] = stats.Due;
            result["newCards"] = stats.NewCards;
            result["reviewedToday"] = stats.ReviewedToday;
            result["nextDue"] = stats.NextDue.HasValue ? stats.NextDue.Value.ToString("o", CultureInfo.InvariantCulture) : null;
            return result;
        }

        private static Dictionary<string, object> Card(StudyCard card)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["source"] = card.Source;
            result["translation"] = card.Translation;
            result["detected"] = card.Detected;
            result["target"] = card.Target;
            result["due"] = card.Due.ToString("o", CultureInfo.InvariantCulture);
            result["intervalDays"] = card.IntervalDays;
            result["repetitions"] = card.Repetitions;
            result["lapses"] = card.Lapses;
            result["ease"] = card.Ease;
            return result;
        }

        private static bool TryGrade(string text, out StudyGrade grade)
        {
            grade = StudyGrade.Good;
            if (string.Equals(text, "again", StringComparison.OrdinalIgnoreCase) || text == "1") { grade = StudyGrade.Again; return true; }
            if (string.Equals(text, "hard", StringComparison.OrdinalIgnoreCase) || text == "2") { grade = StudyGrade.Hard; return true; }
            if (string.Equals(text, "good", StringComparison.OrdinalIgnoreCase) || text == "3") { grade = StudyGrade.Good; return true; }
            if (string.Equals(text, "easy", StringComparison.OrdinalIgnoreCase) || text == "4") { grade = StudyGrade.Easy; return true; }
            return false;
        }

        private void ServeAsset(NetworkStream stream, string name, string contentType, bool replaceToken)
        {
            string path = Path.Combine(Paths.MobileDir(), name);
            if (!File.Exists(path))
            {
                SendText(stream, 500, "Mobile asset missing: " + name);
                return;
            }
            string text = File.ReadAllText(path, Encoding.UTF8);
            if (replaceToken) text = text.Replace("__TOKEN__", _token);
            Send(stream, 200, "OK", contentType, new UTF8Encoding(false).GetBytes(text), true);
        }

        private static Dictionary<string, object> Body(MobileRequest request)
        {
            if (string.IsNullOrEmpty(request.Body)) return new Dictionary<string, object>();
            object parsed = null;
            try { parsed = Json.DeserializeObject(request.Body); }
            catch { return new Dictionary<string, object>(); }
            Dictionary<string, object> map = parsed as Dictionary<string, object>;
            return map ?? new Dictionary<string, object>();
        }

        private static string Value(Dictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null) return null;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string Query(MobileRequest request, string key)
        {
            string value;
            request.Query.TryGetValue(key, out value);
            return value;
        }

        private static Dictionary<string, object> Error(string message)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["error"] = message;
            return result;
        }

        private static void SendJson(NetworkStream stream, int status, object value)
        {
            string json;
            lock (JsonGate) json = Json.Serialize(value);
            Send(stream, status, Reason(status), "application/json; charset=utf-8",
                 new UTF8Encoding(false).GetBytes(json), true);
        }

        private static void SendText(NetworkStream stream, int status, string text)
        {
            Send(stream, status, Reason(status), "text/plain; charset=utf-8",
                 new UTF8Encoding(false).GetBytes(text), true);
        }

        private static void SendFile(NetworkStream stream, string path, string contentType, string downloadName)
        {
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                StringBuilder head = new StringBuilder();
                head.Append("HTTP/1.1 200 OK\r\n");
                head.Append("Content-Type: ").Append(contentType).Append("\r\n");
                head.Append("Content-Length: ").Append(file.Length).Append("\r\n");
                head.Append("Content-Disposition: attachment; filename=\"").Append(downloadName).Append("\"\r\n");
                head.Append("Connection: close\r\n");
                head.Append("Cache-Control: no-store\r\n");
                head.Append("X-Content-Type-Options: nosniff\r\n");
                head.Append("Referrer-Policy: no-referrer\r\n");
                head.Append("\r\n");
                byte[] header = Encoding.ASCII.GetBytes(head.ToString());
                stream.Write(header, 0, header.Length);
                byte[] buffer = new byte[64 * 1024];
                int read;
                while ((read = file.Read(buffer, 0, buffer.Length)) > 0)
                    stream.Write(buffer, 0, read);
                stream.Flush();
            }
        }

        private static void Send(NetworkStream stream, int status, string reason, string contentType, byte[] body, bool noStore)
        {
            if (body == null) body = new byte[0];
            StringBuilder head = new StringBuilder();
            head.Append("HTTP/1.1 ").Append(status).Append(' ').Append(reason).Append("\r\n");
            head.Append("Content-Type: ").Append(contentType).Append("\r\n");
            head.Append("Content-Length: ").Append(body.Length).Append("\r\n");
            head.Append("Connection: close\r\n");
            head.Append(noStore ? "Cache-Control: no-store\r\n" : "Cache-Control: public, max-age=3600\r\n");
            head.Append("X-Content-Type-Options: nosniff\r\n");
            head.Append("Referrer-Policy: no-referrer\r\n");
            head.Append("\r\n");
            byte[] header = Encoding.ASCII.GetBytes(head.ToString());
            stream.Write(header, 0, header.Length);
            if (body.Length > 0) stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        private static string Reason(int status)
        {
            if (status == 200) return "OK";
            if (status == 204) return "No Content";
            if (status == 400) return "Bad Request";
            if (status == 401) return "Unauthorized";
            if (status == 404) return "Not Found";
            if (status == 413) return "Payload Too Large";
            if (status == 502) return "Bad Gateway";
            if (status == 503) return "Service Unavailable";
            return "Internal Server Error";
        }

        private static MobileRequest ReadRequest(NetworkStream stream)
        {
            byte[] buffer = new byte[65536];
            int count = 0;
            int headerEnd = -1;
            while (count < buffer.Length)
            {
                int read = stream.Read(buffer, count, buffer.Length - count);
                if (read <= 0) return null;
                count += read;
                headerEnd = FindHeaderEnd(buffer, count);
                if (headerEnd >= 0) break;
            }
            if (headerEnd < 0) return null;

            string headers = Encoding.UTF8.GetString(buffer, 0, headerEnd);
            string[] lines = headers.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length == 0) return null;
            string[] first = lines[0].Split(' ');
            if (first.Length < 2) return null;

            MobileRequest request = new MobileRequest();
            request.Method = first[0].ToUpperInvariant();
            string target = first[1];
            int question = target.IndexOf('?');
            request.Path = question >= 0 ? target.Substring(0, question) : target;
            request.Query = ParseQuery(question >= 0 ? target.Substring(question + 1) : "");
            request.Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon <= 0) continue;
                request.Headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
            }

            int contentLength = 0;
            string lengthText;
            if (request.Headers.TryGetValue("Content-Length", out lengthText))
                int.TryParse(lengthText, out contentLength);
            if (contentLength < 0 || contentLength > 1048576) return null;

            if (contentLength > 0)
            {
                byte[] body = new byte[contentLength];
                int already = Math.Min(contentLength, count - headerEnd);
                if (already > 0) Buffer.BlockCopy(buffer, headerEnd, body, 0, already);
                int offset = already;
                while (offset < contentLength)
                {
                    int read = stream.Read(body, offset, contentLength - offset);
                    if (read <= 0) return null;
                    offset += read;
                }
                request.Body = Encoding.UTF8.GetString(body);
            }
            else request.Body = "";
            return request;
        }

        private static int FindHeaderEnd(byte[] buffer, int count)
        {
            for (int i = 0; i + 3 < count; i++)
                if (buffer[i] == '\r' && buffer[i + 1] == '\n' && buffer[i + 2] == '\r' && buffer[i + 3] == '\n')
                    return i + 4;
            for (int i = 0; i + 1 < count; i++)
                if (buffer[i] == '\n' && buffer[i + 1] == '\n')
                    return i + 2;
            return -1;
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query)) return values;
            foreach (string part in query.Split('&'))
            {
                if (part.Length == 0) continue;
                int equals = part.IndexOf('=');
                string key = equals >= 0 ? part.Substring(0, equals) : part;
                string value = equals >= 0 ? part.Substring(equals + 1) : "";
                try
                {
                    values[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value.Replace("+", " "));
                }
                catch { }
            }
            return values;
        }

        public void Dispose()
        {
            _running = false;
            try { if (_listener != null) _listener.Stop(); }
            catch { }
            _listener = null;
            _thread = null;
            Logger.Write("mobile: stopped");
        }
    }

    internal sealed class MobileRequest
    {
        public string Method;
        public string Path;
        public Dictionary<string, string> Query;
        public Dictionary<string, string> Headers;
        public string Body;
    }

    internal static class WindowsPackage
    {
        private static readonly object Gate = new object();
        private static string _zip;

        public static string GetOrCreate()
        {
            lock (Gate)
            {
                if (!string.IsNullOrEmpty(_zip) && File.Exists(_zip)) return _zip;

                string stage = Path.Combine(Path.GetTempPath(), "qalam-package-" + Guid.NewGuid().ToString("N"));
                try
                {
                    string dist = Path.Combine(stage, "dist");
                    Directory.CreateDirectory(dist);
                    CopyFile(RequiredFile("Qalam.exe"), Path.Combine(dist, "Qalam.exe"));
                    CopyDirectory(Paths.MobileDir(), Path.Combine(stage, "mobile"), false);

                    string data = FindDirectory("data");
                    if (data != null) CopyDirectory(data, Path.Combine(stage, "data"), false);
                    CopyDirectory(Paths.TashkeelDir(), Path.Combine(stage, "tashkeel"), false);
                    CopyDirectory(Paths.DocumentDir(), Path.Combine(stage, "documents"), false);

                    // The 50k seed and topical packs are small enough to ship. The
                    // 500k depth packs stay on the machine that already has them.
                    string corpus = FindDirectory("concordance");
                    if (corpus != null) CopyDirectory(corpus, Path.Combine(stage, "concordance"), true);

                    string install = FindFile("install.ps1");
                    if (install == null) throw new InvalidOperationException("install.ps1 was not found beside the app.");
                    CopyFile(install, Path.Combine(stage, "install.ps1"));
                    string uninstall = FindFile("uninstall.ps1");
                    if (uninstall != null) CopyFile(uninstall, Path.Combine(stage, "uninstall.ps1"));
                    string readme = FindFile("README.md");
                    if (readme != null) CopyFile(readme, Path.Combine(stage, "README.md"));

                    File.WriteAllText(Path.Combine(stage, "Install Qalam.cmd"),
                        "@echo off\r\n" +
                        "setlocal\r\n" +
                        "cd /d \"%~dp0\"\r\n" +
                        "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%~dp0install.ps1\" -SkipBuild\r\n" +
                        "if errorlevel 1 pause\r\n",
                        new UTF8Encoding(false));
                    File.WriteAllText(Path.Combine(stage, "PACKAGE.txt"),
                        "Qalam for Windows\r\n" +
                        "=================\r\n\r\n" +
                        "1. Extract this ZIP to a folder.\r\n" +
                        "2. Double-click Install Qalam.cmd.\r\n" +
                        "3. Windows 10/11 and Python 3 are required for the offline Tashkeel worker.\r\n\r\n" +
                        "This package includes the desktop app, browser assets, the character dictionary,\r\n" +
                        "and the seed, topical, and 100k core language packs. The large depth packs are\r\n" +
                        "not included; they stay on the machine that generated this download.\r\n",
                        new UTF8Encoding(false));

                    string zip = Path.Combine(Path.GetTempPath(), "Qalam-Windows-" + Guid.NewGuid().ToString("N") + ".zip");
                    ZipFile.CreateFromDirectory(stage, zip, CompressionLevel.Optimal, false);
                    _zip = zip;
                    Logger.Write("windows package created: " + zip);
                    return _zip;
                }
                finally
                {
                    try { Directory.Delete(stage, true); }
                    catch { }
                }
            }
        }

        private static string RequiredFile(string name)
        {
            string path = FindFile(name);
            if (path == null) throw new InvalidOperationException(name + " was not found.");
            return path;
        }

        private static string FindFile(string name)
        {
            string[] candidates = new string[]
            {
                Path.Combine(Paths.AppDir, name),
                Path.Combine(Paths.DataDir, name)
            };
            for (int i = 0; i < candidates.Length; i++)
                if (File.Exists(candidates[i])) return candidates[i];

            DirectoryInfo parent = Directory.GetParent(Paths.AppDir);
            if (parent != null)
            {
                string path = Path.Combine(parent.FullName, name);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        private static string FindDirectory(string name)
        {
            string[] candidates = new string[]
            {
                Path.Combine(Paths.AppDir, name),
                Path.Combine(Paths.DataDir, name)
            };
            for (int i = 0; i < candidates.Length; i++)
                if (Directory.Exists(candidates[i])) return candidates[i];

            DirectoryInfo parent = Directory.GetParent(Paths.AppDir);
            if (parent != null)
            {
                string path = Path.Combine(parent.FullName, name);
                if (Directory.Exists(path)) return path;
            }
            return null;
        }

        private static void CopyFile(string source, string destination)
        {
            if (string.IsNullOrEmpty(source)) throw new InvalidOperationException("Package source file is missing.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
        }

        private static void CopyDirectory(string source, string destination, bool skipDepth)
        {
            if (string.IsNullOrEmpty(source) || !Directory.Exists(source)) return;
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith(".pyc", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase)) continue;
                if (skipDepth && !name.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) &&
                    !name.EndsWith(".qidx", StringComparison.OrdinalIgnoreCase)) continue;
                if (skipDepth && (name.IndexOf("-depth", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("-deep", StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                if (skipDepth && new FileInfo(file).Length > 5L * 1024 * 1024) continue;
                File.Copy(file, Path.Combine(destination, name), true);
            }
            foreach (string directory in Directory.GetDirectories(source))
            {
                string name = Path.GetFileName(directory);
                if (name.Equals(".venv", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals("__pycache__", StringComparison.OrdinalIgnoreCase)) continue;
                CopyDirectory(directory, Path.Combine(destination, name), skipDepth);
            }
        }
    }

    internal static class MobileServe
    {
        public static int Run(string[] args)
        {
            int port = 8787;
            string portText = Arg(args, "--port");
            if (!string.IsNullOrEmpty(portText)) int.TryParse(portText, out port);
            string token = Arg(args, "--token");
            if (string.IsNullOrEmpty(token)) token = MobileServer.NewToken();
            string output = Arg(args, "--out");
            if (string.IsNullOrEmpty(output)) output = Path.Combine(Path.GetTempPath(), "qalam-mobile-url.txt");

            MobileServer server = null;
            CloudflareTunnel tunnel = null;
            try
            {
                server = new MobileServer(new TranslationService(), port, token);
                server.Start(IPAddress.Any);
                string local = server.UrlFor("127.0.0.1");
                string lan = server.UrlFor(MobileServer.LocalAddress());
                string publicUrl = null;
                if (Has(args, "--public"))
                {
                    tunnel = new CloudflareTunnel();
                    tunnel.Start(server.Port);
                    publicUrl = tunnel.WaitForUrl(TimeSpan.FromSeconds(35));
                    publicUrl = publicUrl + "/?token=" + token;
                }

                string directory = Path.GetDirectoryName(output);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                StringBuilder urls = new StringBuilder();
                urls.AppendLine(local);
                urls.AppendLine(lan);
                if (!string.IsNullOrEmpty(publicUrl)) urls.AppendLine(publicUrl);
                File.WriteAllText(output, urls.ToString(), new UTF8Encoding(false));
                Console.WriteLine(local);
                Console.WriteLine(lan);
                if (!string.IsNullOrEmpty(publicUrl)) Console.WriteLine(publicUrl);
                while (true) Thread.Sleep(1000);
            }
            catch (Exception ex)
            {
                Logger.Write("mobile serve: " + ex.Message);
                Console.WriteLine("mobile serve: " + ex.Message);
                return 1;
            }
            finally
            {
                if (tunnel != null) tunnel.Dispose();
                if (server != null) server.Dispose();
            }
        }

        private static bool Has(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }

    internal sealed class CloudflareTunnel : IDisposable
    {
        private readonly ManualResetEventSlim _ready = new ManualResetEventSlim(false);
        private Process _process;
        private string _publicUrl;

        public void Start(int port)
        {
            string executable = FindCloudflared();
            if (executable == null)
                throw new InvalidOperationException("cloudflared was not found. Install it or add it to PATH.");

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = executable;
            info.Arguments = "tunnel --url http://127.0.0.1:" + port + " --no-autoupdate";
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            _process = Process.Start(info);
            if (_process == null) throw new InvalidOperationException("cloudflared could not start.");

            ThreadPool.QueueUserWorkItem(delegate { ReadLines(_process.StandardError); });
            ThreadPool.QueueUserWorkItem(delegate { ReadLines(_process.StandardOutput); });
        }

        public string WaitForUrl(TimeSpan timeout)
        {
            if (!_ready.Wait(timeout))
                throw new TimeoutException("Cloudflare did not return a public URL in time.");
            return _publicUrl;
        }

        private void ReadLines(StreamReader reader)
        {
            try
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Match match = Regex.Match(line, "https://[a-z0-9-]+\\.trycloudflare\\.com", RegexOptions.IgnoreCase);
                    if (!match.Success) continue;
                    _publicUrl = match.Value;
                    _ready.Set();
                }
            }
            catch { }
        }

        private static string FindCloudflared()
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(path))
            {
                foreach (string directory in path.Split(Path.PathSeparator))
                {
                    try
                    {
                        string candidate = Path.Combine(directory.Trim(), "cloudflared.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                    catch { }
                }
            }
            string[] known = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "cloudflared", "cloudflared.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "cloudflared", "cloudflared.exe")
            };
            foreach (string candidate in known)
                if (File.Exists(candidate)) return candidate;
            return null;
        }

        public void Dispose()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(3000);
                }
            }
            catch { }
            try { if (_process != null) _process.Dispose(); }
            catch { }
            _process = null;
            _ready.Dispose();
        }
    }

    internal static class MobileSelfTest
    {
        public static int Run(string[] args)
        {
            string output = Arg(args, "--out");
            if (string.IsNullOrEmpty(output))
                output = Path.Combine(Path.GetTempPath(), "qalam-mobile-selftest");
            bool outputIsFile = output.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
            string report = outputIsFile ? output : Path.Combine(output, "mobiletest.log");
            string outputDirectory = outputIsFile ? Path.GetDirectoryName(output) : output;
            if (!string.IsNullOrEmpty(outputDirectory)) Directory.CreateDirectory(outputDirectory);

            string originalSync = Paths.SyncFolder;
            string temp = Path.Combine(Path.GetTempPath(), "qalam-mobile-test-" + Guid.NewGuid().ToString("N"));
            Paths.SyncFolder = temp;
            StringBuilder log = new StringBuilder();
            int code = 0;
            MobileServer server = null;
            HttpClient client = null;
            try
            {
                Directory.CreateDirectory(temp);
                StudyStore.Clear();
                TranslationService translator = new TranslationService();
                server = new MobileServer(translator, 0, MobileServer.NewToken());
                server.Start(IPAddress.Loopback);
                string root = "http://127.0.0.1:" + server.Port;
                client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("X-Qalam-Token", server.Token);

                string health = Get(client, root + "/api/health");
                Check(log, "health", health.IndexOf("\"ok\":true", StringComparison.Ordinal) >= 0);
                string languages = Get(client, root + "/api/languages");
                Check(log, "languages", languages.IndexOf("\"count\":" + LanguageCatalog.Count, StringComparison.Ordinal) >= 0);
                string html = Get(client, root + "/?token=" + server.Token);
                Check(log, "html", html.IndexOf("Qalam", StringComparison.Ordinal) >= 0 && html.IndexOf("app.js", StringComparison.Ordinal) >= 0);
                string css = Get(client, root + "/app.css");
                Check(log, "theme_controls", html.IndexOf("themeSelect", StringComparison.Ordinal) >= 0 &&
                      html.IndexOf(">Sepia<", StringComparison.Ordinal) >= 0 &&
                      css.IndexOf("[data-theme=\"sepia\"]", StringComparison.Ordinal) >= 0 &&
                      css.IndexOf("[data-theme=\"dark\"]", StringComparison.Ordinal) >= 0);
                Check(log, "wording_controls", html.IndexOf("wordingBtn", StringComparison.Ordinal) >= 0 &&
                      html.IndexOf("wordingCard", StringComparison.Ordinal) >= 0 &&
                      html.IndexOf("Use this", StringComparison.Ordinal) >= 0);
                Check(log, "report_controls", html.IndexOf("reportBugBtn", StringComparison.Ordinal) >= 0 &&
                      html.IndexOf("reportModal", StringComparison.Ordinal) >= 0 &&
                      html.IndexOf("reportDetails", StringComparison.Ordinal) >= 0);

                using (HttpClient anonymous = new HttpClient())
                {
                    HttpResponseMessage unauthorized = anonymous.GetAsync(root + "/api/health").GetAwaiter().GetResult();
                    Check(log, "auth_required", (int)unauthorized.StatusCode == 401);
                }

                string translation = Post(client, root + "/api/translate",
                    "{\"text\":\"No mames, g\\u00fcey.\",\"target\":\"en\"}");
                Check(log, "translate", translation.IndexOf("No way", StringComparison.OrdinalIgnoreCase) >= 0);
                log.AppendLine("translation=" + translation);

                string added = Post(client, root + "/api/study/add",
                    "{\"source\":\"No mames, g\\u00fcey.\",\"translation\":\"No way, dude.\",\"detected\":\"es\",\"target\":\"en\"}");
                Check(log, "study_add", added.IndexOf("\"added\":1", StringComparison.Ordinal) >= 0);
                string study = Get(client, root + "/api/study?scope=all");
                Check(log, "study_list", study.IndexOf("\"total\":1", StringComparison.Ordinal) >= 0);
                string graded = Post(client, root + "/api/study/grade",
                    "{\"source\":\"No mames, g\\u00fcey.\",\"target\":\"en\",\"grade\":\"good\"}");
                Check(log, "study_grade", graded.IndexOf("\"intervalDays\":1", StringComparison.Ordinal) >= 0);
                log.AppendLine("grade=" + graded);

                string manifest = Get(client, root + "/manifest.webmanifest?token=" + server.Token);
                Check(log, "manifest", manifest.IndexOf("\"name\"", StringComparison.Ordinal) >= 0 &&
                      manifest.IndexOf("Qalam", StringComparison.Ordinal) >= 0);
                using (HttpResponseMessage packageResponse = client.GetAsync(root + "/download/windows?token=" + server.Token).GetAwaiter().GetResult())
                {
                    byte[] package = packageResponse.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    Check(log, "windows_download", packageResponse.IsSuccessStatusCode &&
                          package.Length > 1024 && package[0] == (byte)'P' && package[1] == (byte)'K');
                    log.AppendLine("package_bytes=" + package.Length);
                }
            }
            catch (Exception ex)
            {
                code = 1;
                log.AppendLine("FAIL: " + ex);
            }
            finally
            {
                if (client != null) client.Dispose();
                if (server != null) server.Dispose();
                Paths.SyncFolder = originalSync;
                try { StudyStore.Clear(); }
                catch { }
                try { Directory.Delete(temp, true); }
                catch { }
            }

            if (log.ToString().IndexOf("FAIL ", StringComparison.Ordinal) >= 0) code = 1;
            File.WriteAllText(report, log.ToString(), new UTF8Encoding(false));
            Console.WriteLine(log.ToString());
            Console.WriteLine("mobile test report: " + report);
            return code;
        }

        private static string Get(HttpClient client, string url)
        {
            using (HttpResponseMessage response = client.GetAsync(url).GetAwaiter().GetResult())
            {
                string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                    throw new Exception("GET " + url + " -> " + (int)response.StatusCode + " " + body);
                return body;
            }
        }

        private static string Post(HttpClient client, string url, string json)
        {
            using (StringContent content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (HttpResponseMessage response = client.PostAsync(url, content).GetAwaiter().GetResult())
            {
                string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                    throw new Exception("POST " + url + " -> " + (int)response.StatusCode + " " + body);
                return body;
            }
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
    }
}
