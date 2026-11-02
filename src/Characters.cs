// Chinese character dictionary and stroke-order data.
//
// The character table is generated from Unihan + CJKRadicals by
// tools/build_characters.py and shipped in data/characters.tsv. Stroke outlines
// come from Hanzi Writer's character data (derived from Make Me a Hanzi) and are
// cached under %LOCALAPPDATA%\Qalam\strokes on first use.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using ShapePath = System.Windows.Shapes.Path;

namespace Qalam
{
    internal sealed class CharacterInfo
    {
        public char Char;
        public string Pinyin = "";
        public int RadicalNumber;
        public string Radical = "";
        public int ExtraStrokes;
        public int TotalStrokes;
        public string Traditional = "";
        public string Simplified = "";
        public string Definition = "";
        public string RadicalMeaning = "";
    }

    internal static class CharacterDictionary
    {
        private static Dictionary<char, CharacterInfo> _entries;
        private static bool _loaded;
        private static string _error = "";

        public static string Error { get { EnsureLoaded(); return _error; } }
        public static int Count { get { EnsureLoaded(); return _entries == null ? 0 : _entries.Count; } }
        public static string DataFile { get { return FindDataFile(); } }

        private static string FindDataFile()
        {
            List<string> candidates = new List<string>();
            candidates.Add(Path.Combine(Paths.DataDir, "data", "characters.tsv"));
            candidates.Add(Path.Combine(Paths.AppDir, "data", "characters.tsv"));
            DirectoryInfo parent = Directory.GetParent(Paths.AppDir);
            if (parent != null)
                candidates.Add(Path.Combine(parent.FullName, "data", "characters.tsv"));
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }
            return candidates[0];
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _entries = new Dictionary<char, CharacterInfo>();

            string file = FindDataFile();
            if (!File.Exists(file))
            {
                _error = "Character data not found. Run the installer to copy data/characters.tsv.";
                Logger.Write("characters: " + _error + " (" + file + ")");
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(file, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length == 0 || line[0] == '#') continue;
                        string[] parts = line.Split('\t');
                        if (parts.Length < 9 || parts[0].Length == 0) continue;

                        CharacterInfo info = new CharacterInfo();
                        info.Char = parts[0][0];
                        info.Pinyin = parts[1];
                        int number;
                        if (int.TryParse(parts[2], out number)) info.RadicalNumber = number;
                        info.Radical = parts[3];
                        int extra;
                        if (int.TryParse(parts[4], out extra)) info.ExtraStrokes = extra;
                        int total;
                        if (int.TryParse(parts[5], out total)) info.TotalStrokes = total;
                        info.Traditional = parts[6];
                        info.Simplified = parts[7];
                        info.Definition = parts[8];
                        _entries[info.Char] = info;
                    }
                }
                Logger.Write("characters loaded: " + _entries.Count + " from " + file);
            }
            catch (Exception ex)
            {
                _error = "Could not read character data: " + ex.Message;
                Logger.Write("characters: " + ex.Message);
            }
        }

        public static CharacterInfo Lookup(char c)
        {
            EnsureLoaded();
            CharacterInfo info;
            if (_entries == null || !_entries.TryGetValue(c, out info)) return null;

            if (info.RadicalMeaning.Length == 0 && info.Radical.Length > 0)
            {
                CharacterInfo radical;
                if (_entries.TryGetValue(info.Radical[0], out radical))
                    info.RadicalMeaning = radical.Definition;
            }
            return info;
        }

        public static CharacterInfo Lookup(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            foreach (char c in text)
            {
                if (ChineseText.IsHan(c)) return Lookup(c);
            }
            return null;
        }

        public static List<CharacterInfo> ForText(string text, int max)
        {
            List<CharacterInfo> list = new List<CharacterInfo>();
            if (string.IsNullOrEmpty(text)) return list;

            List<char> seen = new List<char>();
            foreach (char c in text)
            {
                if (!ChineseText.IsHan(c) || seen.Contains(c)) continue;
                seen.Add(c);

                CharacterInfo info = Lookup(c);
                if (info == null)
                {
                    info = new CharacterInfo();
                    info.Char = c;
                    info.Definition = "";
                }
                list.Add(info);
                if (max > 0 && list.Count >= max) break;
            }
            return list;
        }

        public static string ShortMeaning(string definition, int limit)
        {
            if (string.IsNullOrEmpty(definition)) return "";
            string text = definition;
            int stop = text.IndexOf(';');
            if (stop > 0) text = text.Substring(0, stop);
            stop = text.IndexOf(',');
            if (stop > 0) text = text.Substring(0, stop);
            text = text.Trim();
            if (text.Length > limit) text = text.Substring(0, limit).TrimEnd() + "\u2026";
            return text;
        }
    }

    internal sealed class StrokeData
    {
        public List<string> Strokes = new List<string>();
        public List<double[]> Starts = new List<double[]>();
    }

    internal static class StrokeService
    {
        private const string Endpoint = "https://cdn.jsdelivr.net/npm/hanzi-writer-data@2.0.1/";
        private static readonly HttpClient Http = CreateClient();

        public static string CacheDir { get { return Path.Combine(Paths.DataDir, "strokes"); } }

        private static HttpClient CreateClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            return client;
        }

        public static async System.Threading.Tasks.Task<StrokeData> LoadAsync(char c)
        {
            string path = Path.Combine(CacheDir, ((int)c).ToString("X4") + ".json");
            string json = null;
            if (File.Exists(path))
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            else
            {
                Directory.CreateDirectory(CacheDir);
                string url = Endpoint + Uri.EscapeDataString(c.ToString()) + ".json";
                json = await Http.GetStringAsync(url).ConfigureAwait(false);
                File.WriteAllText(path, json, new UTF8Encoding(false));
            }
            return Parse(json);
        }

        public static StrokeData Parse(string json)
        {
            StrokeData data = new StrokeData();
            Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            if (root == null) throw new InvalidOperationException("Stroke data is not a JSON object.");

            object[] strokes = root.ContainsKey("strokes") ? root["strokes"] as object[] : null;
            if (strokes != null)
            {
                foreach (object stroke in strokes)
                {
                    string path = Convert.ToString(stroke);
                    if (!string.IsNullOrEmpty(path)) data.Strokes.Add(path);
                }
            }

            object[] medians = root.ContainsKey("medians") ? root["medians"] as object[] : null;
            if (medians != null)
            {
                foreach (object median in medians)
                {
                    object[] points = median as object[];
                    double[] start = new double[] { 0, 0 };
                    if (points != null && points.Length > 0)
                    {
                        object[] first = points[0] as object[];
                        if (first != null && first.Length >= 2)
                        {
                            start[0] = Convert.ToDouble(first[0]);
                            start[1] = Convert.ToDouble(first[1]);
                        }
                    }
                    data.Starts.Add(start);
                }
            }
            return data;
        }

        public static List<ShapePath> BuildPaths(StrokeData data, int visible, double size)
        {
            List<ShapePath> paths = new List<ShapePath>();
            if (data == null) return paths;

            double scale = (size - 24.0) / 1024.0;
            Matrix transform = new Matrix(scale, 0, 0, -scale, 12, 12 + 1024 * scale);

            for (int i = 0; i < data.Strokes.Count; i++)
            {
                ShapePath path = new ShapePath();
                path.Data = Geometry.Parse(data.Strokes[i]);
                if (i < visible) path.Fill = Ui.Bright;
                else if (i == visible) path.Fill = Ui.Gold;
                else path.Fill = Ui.StrokeHint;
                path.RenderTransform = new MatrixTransform(transform);
                paths.Add(path);

                if (i == visible)
                {
                    double[] start = i < data.Starts.Count ? data.Starts[i] : new double[] { 512, 512 };
                    Point point = transform.Transform(new Point(start[0], start[1]));
                    ShapePath marker = new ShapePath();
                    marker.Data = new EllipseGeometry(new Point(point.X, point.Y), 3.5, 3.5);
                    marker.Fill = Ui.Gold;
                    paths.Add(marker);
                }
            }
            return paths;
        }
    }
}
