// Qalam - an independent replacement for Reverso Context's Ctrl + C + C popup.
// Builds with the in-box .NET Framework 4.8 compiler (C# 5); no SDK required.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace Qalam
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Logger.Init();
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
            catch { }
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Logger.Write("fatal: " + e.ExceptionObject);
            };

            if (args.Any(a => string.Equals(a, "--docselftest", StringComparison.OrdinalIgnoreCase)))
                return DocumentSelfTest.Run(args);

            if (args.Any(a => string.Equals(a, "--compare", StringComparison.OrdinalIgnoreCase)))
                return TranslationCompare.Run(args);

            if (args.Any(a => string.Equals(a, "--grammartest", StringComparison.OrdinalIgnoreCase)))
                return GrammarSelfTest.Run(args);

            if (args.Any(a => string.Equals(a, "--buildconcordance", StringComparison.OrdinalIgnoreCase)))
                return ConcordanceBuilder.Run(args);

            if (args.Any(a => string.Equals(a, "--findconcordance", StringComparison.OrdinalIgnoreCase)))
                return ConcordanceTool.RunQuery(args);

            if (args.Any(a => string.Equals(a, "--concordancetest", StringComparison.OrdinalIgnoreCase)))
                return ConcordanceSelfTest.Run(args);

            if (args.Any(a => string.Equals(a, "--registertest", StringComparison.OrdinalIgnoreCase)))
                return RegisterSelfTest.Run(args);

            if (args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)))
                return SelfTest.Run(args);

            if (args.Any(a => string.Equals(a, "--charactertest", StringComparison.OrdinalIgnoreCase)))
            {
                string output = Path.Combine(Paths.AppDir, "charactertest.txt");
                for (int i = 0; i + 1 < args.Length; i++)
                {
                    if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                        output = Path.GetFullPath(args[i + 1]);
                }
                return CharacterSelfTest.Run(output);
            }

            if (args.Any(a => string.Equals(a, "--studytest", StringComparison.OrdinalIgnoreCase)))
            {
                string output = Path.Combine(Paths.AppDir, "studytest.txt");
                for (int i = 0; i + 1 < args.Length; i++)
                {
                    if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                        output = Path.GetFullPath(args[i + 1]);
                }
                return StudySelfTest.Run(output);
            }

            if (args.Any(a => string.Equals(a, "--mobiletest", StringComparison.OrdinalIgnoreCase)))
                return MobileSelfTest.Run(args);

            if (args.Any(a => string.Equals(a, "--mobileserve", StringComparison.OrdinalIgnoreCase)))
                return MobileServe.Run(args);

            bool background = args.Any(a =>
                string.Equals(a, "--background", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "--startup", StringComparison.OrdinalIgnoreCase));

            using (SingleInstance instance = new SingleInstance())
            {
                if (!instance.IsFirst)
                {
                    // A pinned taskbar click starts this second process. Wake
                    // the already-running instance so it can show its window.
                    if (!background) SingleInstance.SignalExisting();
                    return 0;
                }

                var application = new System.Windows.Application();
                application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                application.Exit += delegate { UpdateService.InstallPendingOnExit(); };
                application.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
                {
                    Logger.Write("ui: " + e.Exception);
                    e.Handled = true;
                };

                var controller = new AppController(background);
                instance.Activated += delegate
                {
                    application.Dispatcher.BeginInvoke(new Action(controller.ShowMainWindow));
                };
                instance.Start();
                application.Run();
                controller.Dispose();
            }
            return 0;
        }
    }

    internal static class Utf8
    {
        public static readonly Encoding NoBom = new UTF8Encoding(false);
    }

    internal static class Logger
    {
        private static readonly object Gate = new object();
        private static string _path;

        public static string LogPath { get { return _path; } }

        public static void Init()
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(Paths.DataDir, "logs"));
                _path = Path.Combine(Paths.DataDir, "logs", "qalam.log");
                if (File.Exists(_path) && new FileInfo(_path).Length > 2 * 1024 * 1024)
                {
                    string old = _path + ".1";
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(_path, old);
                }
            }
            catch { }
        }

        public static void Write(string message)
        {
            try
            {
                if (_path == null) Init();
                lock (Gate)
                {
                    File.AppendAllText(_path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine, Utf8.NoBom);
                }
            }
            catch { }
        }
    }

    internal static class Paths
    {
        // Optional user-chosen folder (for example a OneDrive folder) used for
        // history and favorites. There is no Qalam account backend.
        public static string SyncFolder = "";

        public static string StoreDir
        {
            get
            {
                try
                {
                    if (!string.IsNullOrEmpty(SyncFolder) && Directory.Exists(SyncFolder)) return SyncFolder;
                }
                catch { }
                return DataDir;
            }
        }

        public static string DataDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Qalam"); }
        }

        public static string ConfigFile { get { return Path.Combine(DataDir, "config.json"); } }

        public static string AppDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar); }
        }

        public static string TashkeelDir()
        {
            string beside = Path.Combine(AppDir, "tashkeel");
            if (File.Exists(Path.Combine(beside, "tashkeel_server.py"))) return beside;

            DirectoryInfo parent = Directory.GetParent(AppDir);
            if (parent != null)
            {
                string dev = Path.Combine(parent.FullName, "tashkeel");
                if (File.Exists(Path.Combine(dev, "tashkeel_server.py"))) return dev;
            }
            return beside;
        }

        public static string ScriptPath { get { return Path.Combine(TashkeelDir(), "tashkeel_server.py"); } }

        public static string MobileDir()
        {
            string beside = Path.Combine(AppDir, "mobile");
            if (File.Exists(Path.Combine(beside, "index.html"))) return beside;

            DirectoryInfo parent = Directory.GetParent(AppDir);
            if (parent != null)
            {
                string dev = Path.Combine(parent.FullName, "mobile");
                if (File.Exists(Path.Combine(dev, "index.html"))) return dev;
            }
            return beside;
        }

        public static string DocumentDir()
        {
            string beside = Path.Combine(AppDir, "documents");
            if (File.Exists(Path.Combine(beside, "pdf_translate.py"))) return beside;

            DirectoryInfo parent = Directory.GetParent(AppDir);
            if (parent != null)
            {
                string dev = Path.Combine(parent.FullName, "documents");
                if (File.Exists(Path.Combine(dev, "pdf_translate.py"))) return dev;
            }
            return beside;
        }

        public static string PdfScriptPath { get { return Path.Combine(DocumentDir(), "pdf_translate.py"); } }

        public static string ResolvePython(string configured)
        {
            if (!string.IsNullOrEmpty(configured) && File.Exists(configured)) return configured;
            string venv = Path.Combine(TashkeelDir(), ".venv", "Scripts", "python.exe");
            if (File.Exists(venv)) return venv;
            return "python.exe";
        }
    }

    public sealed class Config
    {
        private const string DefaultUpdateUrl = "https://github.com/jfong878/Qalam/releases/latest/download/latest.json";

        public bool AutoTarget { get; set; }
        public string Target { get; set; }
        public int DoubleCopyWindowMs { get; set; }
        public int MaxChars { get; set; }
        public string PythonPath { get; set; }
        public string PdfPythonPath { get; set; }
        public string SyncFolder { get; set; }
        public bool MobileEnabled { get; set; }
        public int MobilePort { get; set; }
        public string MobileToken { get; set; }
        // HTTPS address of latest.json. The default is the public GitHub
        // release feed; an explicit empty value disables automatic checks.
        public string UpdateUrl { get; set; }
        public bool AutoUpdate { get; set; }
        public bool LocalModelEnabled { get; set; }
        public string LocalModelEndpoint { get; set; }
        public string LocalModelName { get; set; }
        // auto = phrasebook/local slang + online fallback; online = no model;
        // offline = local Ollama only. Kept as a string so older config files
        // deserialize without a custom converter.
        public string TranslationMode { get; set; }
        // fusha keeps Mishkal's case endings; any lahja code makes the worker use
        // the dialect form and drop ʾiʿrāb for tashkeel and for speech.
        public string SpokenLahja { get; set; }
        public string Theme { get; set; }
        public string[] ExcludedProcesses { get; set; }

        public Config()
        {
            AutoTarget = true;
            Target = "ar";
            DoubleCopyWindowMs = 600;
            MaxChars = 600;
            PythonPath = "";
            PdfPythonPath = "";
            SyncFolder = "";
            MobileEnabled = false;
            MobilePort = 8787;
            MobileToken = "";
            UpdateUrl = DefaultUpdateUrl;
            AutoUpdate = true;
            LocalModelEnabled = true;
            LocalModelEndpoint = "http://127.0.0.1:11434";
            LocalModelName = "";
            TranslationMode = "auto";
            SpokenLahja = Pronunciation.Fusha;
            Pronunciation.Current = SpokenLahja;
            Theme = ThemeManager.SystemTheme;
            ExcludedProcesses = new string[] { "keepass", "keepassxc", "1password", "bitwarden", "lastpass", "dashlane", "enpass" };
        }

        public static Config Load()
        {
            try
            {
                if (File.Exists(Paths.ConfigFile))
                {
                    string json = File.ReadAllText(Paths.ConfigFile, Encoding.UTF8);
                    Config cfg = new JavaScriptSerializer().Deserialize<Config>(json);
                    if (cfg != null)
                    {
                        if (cfg.MaxChars < 50) cfg.MaxChars = 50;
                        if (cfg.DoubleCopyWindowMs < 150) cfg.DoubleCopyWindowMs = 150;
                        if (string.IsNullOrEmpty(cfg.Target)) cfg.Target = "ar";
                        if (cfg.ExcludedProcesses == null) cfg.ExcludedProcesses = new string[0];
                        if (cfg.PdfPythonPath == null) cfg.PdfPythonPath = "";
                        if (cfg.SyncFolder == null) cfg.SyncFolder = "";
                        if (cfg.MobilePort < 1024 || cfg.MobilePort > 65535) cfg.MobilePort = 8787;
                        if (cfg.MobileToken == null) cfg.MobileToken = "";
                        bool addUpdateUrl = json.IndexOf("\"UpdateUrl\"", StringComparison.OrdinalIgnoreCase) < 0;
                        if (addUpdateUrl) cfg.UpdateUrl = DefaultUpdateUrl;
                        else if (cfg.UpdateUrl == null) cfg.UpdateUrl = DefaultUpdateUrl;
                        bool addAutoUpdate = json.IndexOf("\"AutoUpdate\"", StringComparison.OrdinalIgnoreCase) < 0;
                        if (addAutoUpdate) cfg.AutoUpdate = true;
                        // Existing config files predate the on-device model. Keep
                        // the new path on unless the user explicitly turned it off.
                        if (json.IndexOf("\"LocalModelEnabled\"", StringComparison.OrdinalIgnoreCase) < 0)
                            cfg.LocalModelEnabled = true;
                        if (string.IsNullOrEmpty(cfg.LocalModelEndpoint))
                            cfg.LocalModelEndpoint = "http://127.0.0.1:11434";
                        if (cfg.LocalModelName == null) cfg.LocalModelName = "";
                        bool addSpokenLahja = json.IndexOf("\"SpokenLahja\"", StringComparison.OrdinalIgnoreCase) < 0;
                        bool addTranslationMode = json.IndexOf("\"TranslationMode\"", StringComparison.OrdinalIgnoreCase) < 0;
                        bool addTheme = json.IndexOf("\"Theme\"", StringComparison.OrdinalIgnoreCase) < 0;
                        cfg.TranslationMode = TranslationService.NormalizeMode(cfg.TranslationMode);
                        cfg.SpokenLahja = Pronunciation.Normalize(cfg.SpokenLahja);
                        Pronunciation.Current = cfg.SpokenLahja;
                        cfg.Theme = ThemeManager.Normalize(cfg.Theme);
                        Paths.SyncFolder = cfg.SyncFolder;
                        if (addSpokenLahja || addTranslationMode || addTheme || addUpdateUrl || addAutoUpdate) cfg.Save();
                        return cfg;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write("config load: " + ex.Message);
            }
            Config fresh = new Config();
            fresh.Save();
            return fresh;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Paths.DataDir);
                File.WriteAllText(Paths.ConfigFile, new JavaScriptSerializer().Serialize(this), Utf8.NoBom);
            }
            catch (Exception ex)
            {
                Logger.Write("config save: " + ex.Message);
            }
        }
    }

    internal sealed class HistoryEntry
    {
        public string Source { get; set; }
        public string Translation { get; set; }
        public string Detected { get; set; }
        public string Target { get; set; }
        public DateTime Time { get; set; }
    }

    internal static class HistoryStore
    {
        private const int MaxEntries = 30;
        private static readonly object Gate = new object();

        public static string FilePath { get { return Path.Combine(Paths.StoreDir, "history.json"); } }

        public static List<HistoryEntry> Load()
        {
            lock (Gate) { return LoadUnlocked(); }
        }

        public static void Add(HistoryEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Source) || string.IsNullOrEmpty(entry.Translation)) return;
            lock (Gate)
            {
                try
                {
                    List<HistoryEntry> entries = LoadUnlocked();
                    entries.RemoveAll(e => string.Equals(e.Source, entry.Source, StringComparison.Ordinal) &&
                                           string.Equals(e.Target, entry.Target, StringComparison.Ordinal));
                    if (entry.Time == default(DateTime)) entry.Time = DateTime.Now;
                    entries.Insert(0, entry);
                    if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
                    Directory.CreateDirectory(Paths.StoreDir);
                    File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(entries), Utf8.NoBom);
                }
                catch (Exception ex)
                {
                    Logger.Write("history save: " + ex.Message);
                }
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                try { if (File.Exists(FilePath)) File.Delete(FilePath); }
                catch (Exception ex) { Logger.Write("history clear: " + ex.Message); }
            }
        }

        private static List<HistoryEntry> LoadUnlocked()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<HistoryEntry>();
                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                List<HistoryEntry> entries = new JavaScriptSerializer().Deserialize<List<HistoryEntry>>(json);
                return entries ?? new List<HistoryEntry>();
            }
            catch (Exception ex)
            {
                Logger.Write("history load: " + ex.Message);
                return new List<HistoryEntry>();
            }
        }
    }

    internal static class FavoritesStore
    {
        private const int MaxEntries = 200;
        private static readonly object Gate = new object();

        public static string FilePath { get { return Path.Combine(Paths.StoreDir, "favorites.json"); } }

        public static List<HistoryEntry> Load()
        {
            lock (Gate) { return LoadUnlocked(); }
        }

        public static void Save(HistoryEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Source) || string.IsNullOrEmpty(entry.Translation)) return;
            lock (Gate)
            {
                try
                {
                    List<HistoryEntry> entries = LoadUnlocked();
                    entries.RemoveAll(e => string.Equals(e.Source, entry.Source, StringComparison.Ordinal) &&
                                           string.Equals(e.Target, entry.Target, StringComparison.Ordinal));
                    if (entry.Time == default(DateTime)) entry.Time = DateTime.Now;
                    entries.Insert(0, entry);
                    if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
                    Directory.CreateDirectory(Paths.StoreDir);
                    File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(entries), Utf8.NoBom);
                }
                catch (Exception ex)
                {
                    Logger.Write("favorites save: " + ex.Message);
                }
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                try { if (File.Exists(FilePath)) File.Delete(FilePath); }
                catch (Exception ex) { Logger.Write("favorites clear: " + ex.Message); }
            }
        }

        private static List<HistoryEntry> LoadUnlocked()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<HistoryEntry>();
                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                List<HistoryEntry> entries = new JavaScriptSerializer().Deserialize<List<HistoryEntry>>(json);
                return entries ?? new List<HistoryEntry>();
            }
            catch (Exception ex)
            {
                Logger.Write("favorites load: " + ex.Message);
                return new List<HistoryEntry>();
            }
        }
    }

    internal static class ArabicText
    {
        public static bool IsMark(char c)
        {
            return (c >= 0x0610 && c <= 0x061A) || (c >= 0x064B && c <= 0x065F) ||
                   c == 0x0670 || (c >= 0x06D6 && c <= 0x06ED);
        }

        public static string StripDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            StringBuilder builder = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (IsMark(c) || c == 0x0640) continue;
                builder.Append(c);
            }
            return builder.ToString();
        }

        public static bool ContainsArabic(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                if ((c >= 0x0600 && c <= 0x06FF) || (c >= 0x0750 && c <= 0x077F) ||
                    (c >= 0x08A0 && c <= 0x08FF) || (c >= 0xFB50 && c <= 0xFDFF) ||
                    (c >= 0xFE70 && c <= 0xFEFF))
                    return true;
            }
            return false;
        }
    }

    internal static class ChineseText
    {
        public static bool IsHan(char c)
        {
            return (c >= '\u3400' && c <= '\u4DBF') || (c >= '\u4E00' && c <= '\u9FFF') ||
                   (c >= '\uF900' && c <= '\uFAFF');
        }

        public static bool ContainsHan(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                if (IsHan(c)) return true;
            }
            return false;
        }

        public static bool IsChineseLanguage(string code)
        {
            return !string.IsNullOrEmpty(code) &&
                   code.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class SelectionReader
    {
        private static int _busy;

        public static async Task<string> TryReadAsync(int timeoutMs)
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return null;

            Task<string> read = Task.Run(new Func<string>(Read));
#pragma warning disable 4014
            read.ContinueWith(t =>
            {
                Interlocked.Exchange(ref _busy, 0);
                GC.KeepAlive(t.Exception);
            }, TaskContinuationOptions.ExecuteSynchronously);
#pragma warning restore 4014

            Task finished = await Task.WhenAny(read, Task.Delay(timeoutMs)).ConfigureAwait(true);
            if (finished != read)
            {
                Logger.Write("uia timeout");
                return null;
            }

            try
            {
                return await read.ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Logger.Write("uia: " + ex.Message);
                return null;
            }
        }

        private static string Read()
        {
            AutomationElement focused = AutomationElement.FocusedElement;
            if (focused == null) return null;

            object isPassword = focused.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty);
            if (isPassword is bool && (bool)isPassword) return null;

            object patternObject;
            if (!focused.TryGetCurrentPattern(TextPattern.Pattern, out patternObject)) return null;
            TextPattern pattern = patternObject as TextPattern;
            if (pattern == null) return null;

            TextPatternRange[] ranges = pattern.GetSelection();
            if (ranges == null || ranges.Length == 0) return null;

            StringBuilder builder = new StringBuilder();
            foreach (TextPatternRange range in ranges)
            {
                if (range == null) continue;
                try
                {
                    if (range.CompareEndpoints(TextPatternRangeEndpoint.Start, range, TextPatternRangeEndpoint.End) == 0)
                        continue;
                    string part = range.GetText(-1);
                    if (!string.IsNullOrEmpty(part)) builder.Append(part);
                }
                catch (Exception)
                {
                    // One provider range failing should not discard the others.
                }
            }

            string text = builder.ToString().Trim();
            return text.Length == 0 ? null : text;
        }
    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }

    internal sealed class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int VK_CONTROL = 0x11;
        private const int VK_C = 0x43;

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private readonly HookProc _proc;
        private readonly int _windowMs;
        private IntPtr _handle;
        private long _lastCopyMs;
        private uint _sequenceAtFirstCopy;

        public event Action<uint> DoubleCopy;

        public bool IsInstalled { get { return _handle != IntPtr.Zero; } }

        public KeyboardHook(int windowMs)
        {
            _windowMs = windowMs;
            _proc = Callback;
        }

        public void Install()
        {
            _handle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
            if (_handle == IntPtr.Zero)
                Logger.Write("keyboard hook failed: " + Marshal.GetLastWin32Error());
        }

        private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && (wParam.ToInt64() == WM_KEYDOWN || wParam.ToInt64() == WM_SYSKEYDOWN))
                {
                    KBDLLHOOKSTRUCT info = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                    bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                    if (info.vkCode == VK_C && ctrl)
                    {
                        long now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
                        long delta = now - _lastCopyMs;
                        if (delta >= 30 && delta <= _windowMs)
                        {
                            _lastCopyMs = 0;
                            Action<uint> handler = DoubleCopy;
                            if (handler != null) handler(_sequenceAtFirstCopy);
                        }
                        else
                        {
                            _lastCopyMs = now;
                            _sequenceAtFirstCopy = NativeMethods.GetClipboardSequenceNumber();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write("keyboard hook: " + ex.Message);
            }
            return CallNextHookEx(_handle, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }

    internal sealed class TranslationOutcome
    {
        public string Text;
        public string Detected;
        public string Engine;
        public List<string> Synonyms = new List<string>();
    }

    internal sealed class TranslationService
    {
        private const string SingleEndpoint = "https://translate.googleapis.com/translate_a/single";
        private const string DictionaryEndpoint = "https://clients5.google.com/translate_a/t";
        private const string MemoryEndpoint = "https://api.mymemory.translated.net/get";

        private readonly HttpClient _http;
        private readonly LocalModelClient _local;
        private string _mode = "auto";

        public TranslationService()
        {
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromSeconds(10);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/json,text/plain,*/*");
            _local = new LocalModelClient();
        }

        public void ConfigureProvider(bool enabled, string endpoint, string model, string mode)
        {
            _local.Enabled = enabled;
            _local.Endpoint = endpoint;
            _local.Model = model;
            _mode = NormalizeMode(mode);
        }

        public string Mode { get { return _mode; } }
        public string LocalModelName { get { return _local.ResolvedModel; } }
        public string LocalModelError { get { return _local.LastError; } }

        public async Task<TranslationOutcome> SuggestWordingAsync(string text, string language, string context)
        {
            return await _local.RewriteAsync(text, language, context).ConfigureAwait(false);
        }

        public static string NormalizeMode(string mode)
        {
            if (string.Equals(mode, "online", StringComparison.OrdinalIgnoreCase)) return "online";
            if (string.Equals(mode, "offline", StringComparison.OrdinalIgnoreCase)) return "offline";
            return "auto";
        }

        public async Task<TranslationOutcome> TranslateAsync(string text, string target)
        {
            string detected = Colloquial.DetectLanguage(text);

            // A fully covered colloquial phrase has a curated rendering. This is
            // deterministic, works offline, and keeps the intended register.
            // Short phrases such as "good luck" have no stopword signal, so infer
            // the phrasebook direction from the requested target and the script
            // before giving up on the curated path.
            string phraseSource = detected;
            if (phraseSource == null)
            {
                if (ArabicText.ContainsArabic(text)) phraseSource = "ar";
                else if (string.Equals(target, "ar", StringComparison.OrdinalIgnoreCase)) phraseSource = "en";
                else if (string.Equals(target, "en", StringComparison.OrdinalIgnoreCase)) phraseSource = "es";
            }
            string phrased = Colloquial.ComposeTranslation(text, phraseSource, target);
            // Stopword detection can read an English line such as "no problem"
            // as Spanish because of the shared word "no". The Arabic phrasebook
            // is the only curated target-"ar" path, so try English explicitly
            // when the text is Latin-script.
            if (phrased == null && string.Equals(target, "ar", StringComparison.OrdinalIgnoreCase) &&
                !ArabicText.ContainsArabic(text))
            {
                phrased = Colloquial.ComposeTranslation(text, "en", target);
                if (phrased != null) phraseSource = "en";
            }
            if (phrased != null)
            {
                TranslationOutcome outcome = new TranslationOutcome();
                outcome.Text = phrased;
                outcome.Detected = phraseSource;
                outcome.Engine = "phrasebook";
                Logger.Write("translate phrasebook " + (phraseSource == null ? "?" : phraseSource) + "->" + target);
                return outcome;
            }

            // The offline provider is a complete alternative path: it must not
            // silently reach the network when the local model is unavailable.
            if (_mode == "offline")
            {
                string offlineSource = detected;
                if (string.IsNullOrEmpty(offlineSource))
                    offlineSource = ArabicText.ContainsArabic(text) ? "ar" : "en";
                try
                {
                    TranslationOutcome offline = await _local
                        .TranslateGeneralAsync(text, offlineSource, target).ConfigureAwait(false);
                    if (offline != null)
                    {
                        Logger.Write("translate offline " + (detected == null ? "?" : detected) + "->" + target);
                        return offline;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Write("translate offline: " + ex.Message);
                }
                string reason = _local.LastError;
                throw new InvalidOperationException(
                    "Offline translation is unavailable." +
                    (string.IsNullOrEmpty(reason) ? " Start Ollama and install a local model, or switch to Auto." : " " + reason),
                    null);
            }

            bool colloquial = Colloquial.LooksColloquial(text, detected);
            if (colloquial && _mode != "online")
            {
                // Public endpoints flatten slang. Prefer a local model when one is
                // available, then fall through to the online services if it is not.
                try
                {
                    TranslationOutcome local = await _local.TranslateAsync(text, detected, target).ConfigureAwait(false);
                    if (local != null)
                    {
                        Logger.Write("translate local " + (detected == null ? "?" : detected) + "->" + target);
                        return local;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Write("translate local: " + ex.Message);
                }
            }

            Task<TranslationOutcome> single = TranslateSingleOnceAsync(text, target);
            Task<TranslationOutcome> dictionary = TranslateDictionaryAsync(text, target);
            Exception last = null;

            // Run both Google shapes at once; whichever answers first wins. A slow
            // or throttled endpoint therefore cannot hold the popup hostage.
            Task<TranslationOutcome> first = await Task.WhenAny(single, dictionary).ConfigureAwait(false);
            Task<TranslationOutcome> second = first == single ? dictionary : single;
#pragma warning disable 4014
            second.ContinueWith(t => { GC.KeepAlive(t.Exception); }, TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore 4014

            try
            {
                return await first.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                last = ex;
                Logger.Write("translate first: " + ex.Message);
            }

            try
            {
                return await second.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                last = ex;
                Logger.Write("translate second: " + ex.Message);
            }

            string source = detected;
            if (string.IsNullOrEmpty(source))
                source = ArabicText.ContainsArabic(text) ? "ar" : "en";
            if (text.Length <= 450 && !string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return await TranslateMemoryAsync(text, source, target).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    last = ex;
                    Logger.Write("translate memory: " + ex.Message);
                }
            }

            // Last resort for normal prose when every online endpoint is down.
            // Online mode deliberately skips this so a comparison run measures
            // the online path rather than a hidden local fallback.
            if (!colloquial && _mode == "auto")
            {
                try
                {
                    TranslationOutcome local = await _local
                        .TranslateGeneralAsync(text, source, target).ConfigureAwait(false);
                    if (local != null) return local;
                }
                catch (Exception ex)
                {
                    Logger.Write("translate local fallback: " + ex.Message);
                }
            }

            throw new InvalidOperationException("All translation endpoints failed. " + (last != null ? last.Message : ""), last);
        }

        private async Task<TranslationOutcome> TranslateSingleOnceAsync(string text, string target)
        {
            string json;
            if (text.Length > 400)
            {
                Dictionary<string, string> values = new Dictionary<string, string>();
                values["client"] = "gtx";
                values["sl"] = "auto";
                values["tl"] = target;
                values["dt"] = "t";
                values["q"] = text;
                using (FormUrlEncodedContent body = new FormUrlEncodedContent(values))
                using (HttpResponseMessage response = await _http.PostAsync(SingleEndpoint, body).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
            else
            {
                string url = SingleEndpoint + "?client=gtx&sl=auto&tl=" + Uri.EscapeDataString(target) +
                             "&dt=t&dt=bd&q=" + Uri.EscapeDataString(text);
                json = await _http.GetStringAsync(url).ConfigureAwait(false);
            }
            return ParseSingle(json);
        }

        private async Task<TranslationOutcome> TranslateDictionaryAsync(string text, string target)
        {
            string json;
            if (text.Length > 400)
            {
                Dictionary<string, string> values = new Dictionary<string, string>();
                values["client"] = "dict-chrome-ex";
                values["ie"] = "UTF-8";
                values["oe"] = "UTF-8";
                values["sl"] = "auto";
                values["tl"] = target;
                values["q"] = text;
                using (FormUrlEncodedContent body = new FormUrlEncodedContent(values))
                using (HttpResponseMessage response = await _http.PostAsync(DictionaryEndpoint, body).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
            else
            {
                string url = DictionaryEndpoint + "?client=dict-chrome-ex&ie=UTF-8&oe=UTF-8&sl=auto&tl=" +
                             Uri.EscapeDataString(target) + "&q=" + Uri.EscapeDataString(text);
                json = await _http.GetStringAsync(url).ConfigureAwait(false);
            }
            return ParseDictionary(json);
        }

        private async Task<TranslationOutcome> TranslateMemoryAsync(string text, string source, string target)
        {
            string url = MemoryEndpoint + "?q=" + Uri.EscapeDataString(text) + "&langpair=" +
                         Uri.EscapeDataString(source + "|" + target);
            string json = await _http.GetStringAsync(url).ConfigureAwait(false);
            TranslationOutcome outcome = ParseMemory(json);
            if (outcome.Detected == null) outcome.Detected = source;
            return outcome;
        }

        public async Task<List<string>> GetSynonymsAsync(string text, string target)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 60) return new List<string>();

            // Google throttles the first host aggressively after a translation burst.
            // The same dictionary payload is served by all three, so walk them in turn.
            string[] endpoints = new string[]
            {
                "https://translate.googleapis.com/translate_a/single",
                "https://translate.google.com/translate_a/single",
                "https://clients5.google.com/translate_a/single"
            };

            string lastError = null;
            foreach (string endpoint in endpoints)
            {
                string client = endpoint.IndexOf("clients5", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "dict-chrome-ex" : "gtx";
                string url = endpoint + "?client=" + client + "&sl=auto&tl=" + Uri.EscapeDataString(target) +
                             "&dt=bd&q=" + Uri.EscapeDataString(text);
                string json = null;
                try
                {
                    json = await _http.GetStringAsync(url).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    Logger.Write("synonyms " + new Uri(endpoint).Host + ": " + ex.Message);
                }

                if (json != null)
                {
                    List<string> parsed = ParseSynonyms(json);
                    if (parsed.Count > 0) return parsed;
                }

                await Task.Delay(180).ConfigureAwait(false);
            }

            if (lastError != null) Logger.Write("synonyms unavailable: " + lastError);
            return new List<string>();
        }

        private static List<string> ParseSynonyms(string json)
        {
            object[] root = new JavaScriptSerializer().DeserializeObject(json) as object[];
            return ParseDictionaryEntries(root);
        }

        private static List<string> ParseDictionaryEntries(object[] root)
        {
            List<string> words = new List<string>();
            if (root == null || root.Length < 2) return words;

            object[] entries = root[1] as object[];
            if (entries == null) return words;

            foreach (object entryObject in entries)
            {
                object[] entry = entryObject as object[];
                if (entry == null || entry.Length < 2) continue;
                object[] translations = entry[1] as object[];
                if (translations == null) continue;

                foreach (object wordObject in translations)
                {
                    string word = wordObject as string;
                    if (string.IsNullOrWhiteSpace(word)) continue;
                    word = WebUtility.HtmlDecode(word).Trim();
                    if (word.Length == 0 || words.Contains(word)) continue;
                    words.Add(word);
                    if (words.Count >= 8) return words;
                }
            }
            return words;
        }

        private static TranslationOutcome ParseSingle(string json)
        {
            object[] root = new JavaScriptSerializer().DeserializeObject(json) as object[];
            if (root == null || root.Length == 0)
                throw new InvalidOperationException("Unexpected translation response.");

            StringBuilder builder = new StringBuilder();
            object[] segments = root[0] as object[];
            if (segments != null)
            {
                foreach (object segmentObject in segments)
                {
                    object[] segment = segmentObject as object[];
                    if (segment != null && segment.Length > 0 && segment[0] is string)
                        builder.Append((string)segment[0]);
                }
            }

            string translated = builder.ToString();
            if (translated.Length == 0)
                throw new InvalidOperationException("The translation service returned no text.");

            TranslationOutcome outcome = new TranslationOutcome();
            outcome.Text = translated;
            outcome.Detected = root.Length > 2 ? root[2] as string : null;
            outcome.Synonyms = ParseDictionaryEntries(root);
            return outcome;
        }

        private static TranslationOutcome ParseDictionary(string json)
        {
            object[] root = new JavaScriptSerializer().DeserializeObject(json) as object[];
            if (root == null || root.Length == 0)
                throw new InvalidOperationException("Unexpected dictionary response.");

            StringBuilder builder = new StringBuilder();
            string detected = null;
            if (root[0] is string)
            {
                builder.Append((string)root[0]);
                if (root.Length > 1) detected = root[1] as string;
            }
            else
            {
                foreach (object item in root)
                {
                    object[] segment = item as object[];
                    if (segment == null || segment.Length == 0 || !(segment[0] is string)) continue;
                    builder.Append((string)segment[0]);
                    if (detected == null && segment.Length > 1) detected = segment[1] as string;
                }
            }

            string translated = builder.ToString();
            if (translated.Length == 0)
                throw new InvalidOperationException("The dictionary endpoint returned no text.");

            TranslationOutcome outcome = new TranslationOutcome();
            outcome.Text = translated;
            outcome.Detected = detected;
            return outcome;
        }

        private static TranslationOutcome ParseMemory(string json)
        {
            Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            if (root == null) throw new InvalidOperationException("Unexpected memory response.");

            object dataObject;
            Dictionary<string, object> data = root.TryGetValue("responseData", out dataObject) ? dataObject as Dictionary<string, object> : null;
            if (data == null) throw new InvalidOperationException("Unexpected memory response.");

            string translated = data.ContainsKey("translatedText") ? Convert.ToString(data["translatedText"]) : null;
            if (string.IsNullOrWhiteSpace(translated) || translated.IndexOf("PLEASE SELECT", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("The memory endpoint returned no text.");

            TranslationOutcome outcome = new TranslationOutcome();
            outcome.Text = translated;
            outcome.Detected = null;
            return outcome;
        }
    }

    internal sealed class ExamplePair
    {
        public string Source;
        public string Target;
        public string Term;
        public bool FromPhrasebook;
        public bool FromConcordance;
        public string Origin;
        public string License;
        // 0..1 spoken-register estimate for a corpus line (Register.cs).
        public double Register;
    }

    internal sealed class ContextService
    {
        private readonly HttpClient _http;
        private readonly ConcordanceStore _concordance;

        public ContextService()
        {
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromSeconds(7);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            _concordance = ConcordanceStore.Shared;
        }

        public List<ExamplePair> GetLocalExamples(string text, string sourceLang, string targetLang, string hint, int max)
        {
            List<ExamplePair> examples = new List<ExamplePair>();
            string source = Normalize(sourceLang);
            string target = Normalize(targetLang);
            if (source == null || target == null || source == target) return examples;

            int words = WordCount(text);
            if (words == 0 || words > 24 || text.Length > 240) return examples;

            // Curated usage comes first for known slang. The local concordance
            // follows with real aligned sentences from open corpora, so this
            // path works in offline mode and does not depend on Tatoeba/Google.
            examples.AddRange(Colloquial.ExamplesFor(text, source, target, Math.Min(3, Math.Max(1, max))));
            // At most three corpus lines: one slot stays available for a curated
            // line, and a weak fourth alignment should not crowd out a better
            // phrasebook or network result.
            examples.AddRange(_concordance.Search(text, source, target,
                                                  Math.Min(3, Math.Max(1, max)), hint));

            // If the corpus had nothing for this phrase, finish with the rest of
            // the curated examples before any network source is considered.
            if (examples.Count < max)
            {
                foreach (ExamplePair extra in Colloquial.ExamplesFor(text, source, target, max))
                {
                    bool exists = false;
                    foreach (ExamplePair existing in examples)
                    {
                        if (string.Equals(existing.Source, extra.Source, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists) examples.Add(extra);
                    if (examples.Count >= max) break;
                }
            }
            return Dedupe(examples).Take(Math.Max(1, max)).ToList();
        }

        public async Task<List<ExamplePair>> GetExamplesAsync(string text, string sourceLang, string targetLang, string hint = null)
        {
            string source = Normalize(sourceLang);
            string target = Normalize(targetLang);
            if (source == null || target == null || source == target) return new List<ExamplePair>();

            // Phrases are worth looking up now, not just dictionary words. A real
            // sentence still has its own context, so cap the work at a short
            // paragraph and search its component expressions instead.
            int words = WordCount(text);
            if (words == 0 || words > 24 || text.Length > 240) return new List<ExamplePair>();

            List<ExamplePair> examples = GetLocalExamples(text, source, target, hint, 4);
            // Three real local lines already answer the context question. Do not
            // pad a good local result with subtitle-derived network memory just
            // to reach four; the fourth slot is for a genuine gap.
            if (examples.Count >= 3) return examples;

            List<string> terms = BuildTerms(text, source);
            if (terms.Count > 0)
            {
                List<Task<List<ExamplePair>>> tasks = new List<Task<List<ExamplePair>>>();
                foreach (string term in terms)
                {
                    // A single word is a dictionary entry in MyMemory, not a usage
                    // example. Tatoeba still provides sentences for it.
                    if (WordCount(term) >= 2)
                        tasks.Add(SafeMemoryAsync(term, text, source, target));
                    tasks.Add(SafeTatoebaAsync(term, source, target));
                }

                List<ExamplePair>[] found = await Task.WhenAll(tasks).ConfigureAwait(false);
                foreach (List<ExamplePair> group in found)
                    if (group != null) examples.AddRange(group);
            }

            return Dedupe(examples).Take(4).ToList();
        }

        private static List<string> BuildTerms(string text, string source)
        {
            List<string> terms = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            char[] trim = new char[]
            {
                '.', ',', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']',
                '\u060c', '\u061f', '\u061b', '\u00ab', '\u00bb'
            };

            string clean = Clean(ArabicText.StripDiacritics(text));
            if (clean != null && clean.Length <= 80) AddTerm(terms, seen, clean.Trim(trim));

            foreach (ColloquialMatch match in Colloquial.FindMatches(text, source))
            {
                AddTerm(terms, seen, source == "en" ? match.Entry.English : match.Entry.Spanish);
                if (terms.Count >= 4) break;
            }

            return terms.Take(4).ToList();
        }

        private static void AddTerm(List<string> terms, HashSet<string> seen, string term)
        {
            if (string.IsNullOrWhiteSpace(term)) return;
            term = term.Trim();
            if (term.Length > 80) term = term.Substring(0, 80);
            if (!seen.Add(term)) return;
            terms.Add(term);
        }

        private async Task<List<ExamplePair>> SafeMemoryAsync(string term, string fullText, string source, string target)
        {
            try
            {
                return await MemoryAsync(term, fullText, source, target).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Write("examples memory: " + ex.Message);
                return new List<ExamplePair>();
            }
        }

        private async Task<List<ExamplePair>> SafeTatoebaAsync(string term, string source, string target)
        {
            try
            {
                return await TatoebaAsync(term, source, target).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Write("examples tatoeba: " + ex.Message);
                return new List<ExamplePair>();
            }
        }

        private static int WordCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return text.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private static bool HasSubtitleNoise(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.IndexOf("-->", StringComparison.Ordinal) >= 0 ||
                   value.IndexOf("00:", StringComparison.Ordinal) >= 0;
        }

        private static bool ContainsPhrase(string sentence, string query)
        {
            string cleanSentence = ArabicText.StripDiacritics(Clean(sentence));
            string cleanQuery = ArabicText.StripDiacritics(Clean(query));
            if (cleanSentence == null || cleanQuery == null) return false;

            char[] trim = new char[]
            {
                '.', ',', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']',
                '\u060c', '\u061f', '\u061b', '\u00ab', '\u00bb'
            };
            cleanQuery = cleanQuery.Trim(trim);
            if (cleanQuery.Length == 0) return false;
            return cleanSentence.IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async Task<List<ExamplePair>> MemoryAsync(string term, string fullText, string source, string target)
        {
            string memorySource = source == "zh" ? "zh-CN" : source;
            string memoryTarget = target == "zh" ? "zh-CN" : target;
            string url = "https://api.mymemory.translated.net/get?q=" + Uri.EscapeDataString(term) +
                         "&langpair=" + Uri.EscapeDataString(memorySource + "|" + memoryTarget);
            string json = await _http.GetStringAsync(url).ConfigureAwait(false);

            Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            if (root == null) return new List<ExamplePair>();

            object matchesObject;
            object[] matches = root.TryGetValue("matches", out matchesObject) ? matchesObject as object[] : null;
            List<ExamplePair> results = new List<ExamplePair>();
            if (matches == null) return results;

            foreach (object matchObject in matches)
            {
                Dictionary<string, object> match = matchObject as Dictionary<string, object>;
                if (match == null) continue;

                string segment = Clean(match.ContainsKey("segment") ? Convert.ToString(match["segment"]) : null);
                string translation = Clean(match.ContainsKey("translation") ? Convert.ToString(match["translation"]) : null);
                if (segment == null || translation == null) continue;
                if (HasSubtitleNoise(segment) || HasSubtitleNoise(translation)) continue;
                if (WordCount(segment) < 2 || WordCount(translation) < 1) continue;
                if (segment.Length > 200 || translation.Length > 200) continue;
                if (!ContainsPhrase(segment, term)) continue;
                if (string.Equals(segment, fullText.Trim(), StringComparison.OrdinalIgnoreCase)) continue;

                // MyMemory's own MT rows are the literal translations that caused
                // the problem in the first place. Only human memory is context.
                string reference = match.ContainsKey("reference") ? Convert.ToString(match["reference"]) : null;
                string createdBy = match.ContainsKey("created-by") ? Convert.ToString(match["created-by"]) : null;
                if ((reference != null && reference.IndexOf("Machine Translation", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    string.Equals(createdBy, "MT!", StringComparison.OrdinalIgnoreCase)) continue;

                results.Add(NewPair(segment, translation, term));
                if (results.Count >= 5) break;
            }
            return results;
        }

        private async Task<List<ExamplePair>> TatoebaAsync(string text, string source, string target)
        {
            string from = TatoebaCode(source);
            string to = TatoebaCode(target);
            if (from == null || to == null) return new List<ExamplePair>();

            string query = BuildQuery(text);
            if (query.Length == 0) return new List<ExamplePair>();

            string url = "https://tatoeba.org/en/api_v0/search?from=" + from + "&to=" + to +
                         "&query=" + Uri.EscapeDataString(query);
            string json = await _http.GetStringAsync(url).ConfigureAwait(false);

            Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            if (root == null) return new List<ExamplePair>();

            object resultsObject;
            object[] results = root.TryGetValue("results", out resultsObject) ? resultsObject as object[] : null;
            List<ExamplePair> pairs = new List<ExamplePair>();
            if (results == null) return pairs;

            foreach (object resultObject in results)
            {
                Dictionary<string, object> result = resultObject as Dictionary<string, object>;
                if (result == null) continue;

                string sentence = Clean(result.ContainsKey("text") ? Convert.ToString(result["text"]) : null);
                if (sentence == null) continue;
                if (HasSubtitleNoise(sentence)) continue;
                if (WordCount(sentence) < 2 || sentence.Length > 200) continue;
                if (!ContainsPhrase(sentence, query)) continue;

                AddTatoebaTranslations(result, sentence, target, text, pairs);
                if (pairs.Count >= 5) break;
            }
            return pairs;
        }

        private static void AddTatoebaTranslations(Dictionary<string, object> result, string sentence, string target, string term, List<ExamplePair> pairs)
        {
            object translationsObject;
            if (!result.TryGetValue("translations", out translationsObject)) return;

            object[] groups = translationsObject as object[];
            if (groups == null) return;
            string targetThree = TatoebaCode(target);

            foreach (object groupObject in groups)
            {
                object[] group = groupObject as object[];
                if (group == null) continue;

                foreach (object itemObject in group)
                {
                    Dictionary<string, object> item = itemObject as Dictionary<string, object>;
                    if (item == null) continue;

                    string langTag = item.ContainsKey("lang_tag") ? Convert.ToString(item["lang_tag"]) : null;
                    string lang = item.ContainsKey("lang") ? Convert.ToString(item["lang"]) : null;
                    bool matches = string.Equals(langTag, target, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(lang, targetThree, StringComparison.OrdinalIgnoreCase) ||
                                   (target == "zh" && (string.Equals(langTag, "zh", StringComparison.OrdinalIgnoreCase) ||
                                                       string.Equals(langTag, "cmn", StringComparison.OrdinalIgnoreCase)));
                    if (!matches) continue;

                    string translation = Clean(item.ContainsKey("text") ? Convert.ToString(item["text"]) : null);
                    if (translation == null || translation.Length > 200) continue;
                    if (HasSubtitleNoise(translation)) continue;
                    pairs.Add(NewPair(sentence, translation, term));
                }
            }
        }

        private static List<ExamplePair> Dedupe(List<ExamplePair> pairs)
        {
            List<ExamplePair> unique = new List<ExamplePair>();
            HashSet<string> seen = new HashSet<string>();
            foreach (ExamplePair pair in pairs)
            {
                if (pair == null || string.IsNullOrEmpty(pair.Source) || string.IsNullOrEmpty(pair.Target)) continue;
                // One line per source sentence: Tatoeba often carries several gendered
                // or regional translations of the same sentence, which reads as a
                // duplicate in a three-line popup.
                string key = ArabicText.StripDiacritics(pair.Source).Trim().ToLowerInvariant();
                if (!seen.Add(key)) continue;
                unique.Add(pair);
            }
            return unique;
        }

        private static ExamplePair NewPair(string source, string target)
        {
            return NewPair(source, target, null);
        }

        private static ExamplePair NewPair(string source, string target, string term)
        {
            ExamplePair pair = new ExamplePair();
            pair.Source = source;
            pair.Target = target;
            pair.Term = term;
            return pair;
        }

        private static string BuildQuery(string text)
        {
            string clean = Clean(ArabicText.StripDiacritics(text));
            if (clean == null) return "";
            string query = clean;
            if (query.Length > 80) query = query.Substring(0, 80);
            return query;
        }

        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            string text = WebUtility.HtmlDecode(value).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
            while (text.IndexOf("  ", StringComparison.Ordinal) >= 0) text = text.Replace("  ", " ");
            return text.Length == 0 ? null : text;
        }

        private static string Normalize(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            string value = code.ToLowerInvariant();
            int dash = value.IndexOf('-');
            if (dash > 0) value = value.Substring(0, dash);
            return value;
        }

        private static string TatoebaCode(string code)
        {
            switch (code)
            {
                case "ar": return "ara";
                case "en": return "eng";
                case "fr": return "fra";
                case "es": return "spa";
                case "de": return "deu";
                case "ru": return "rus";
                case "zh": return "cmn";
                case "tr": return "tur";
                case "ur": return "urd";
                case "fa": return "pes";
                case "hi": return "hin";
                case "pt": return "por";
                case "it": return "ita";
                case "ja": return "jpn";
                case "ko": return "kor";
                case "id": return "ind";
                case "nl": return "nld";
                case "pl": return "pol";
                case "uk": return "ukr";
                case "el": return "ell";
                case "he": return "heb";
                case "vi": return "vie";
                case "th": return "tha";
                case "ro": return "ron";
                default: return null;
            }
        }
    }

    internal static class SpeechService
    {
        private static readonly HttpClient Http = CreateClient();
        private static MediaPlayer _player;
        private static string _lastFile;

        // Set once at startup so speech can ask the offline worker for the
        // dialect reading without the popup holding a config or a client.
        public static Func<string, string, string> Vocalizer;

        public static async Task SpeakAsync(string text, string language)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(language)) return;
            text = PrepareSpoken(text, language);
            if (text.Length > 220) text = text.Substring(0, 220);

            string url = "https://translate.google.com/translate_tts?ie=UTF-8&client=tw-ob&tl=" +
                         Uri.EscapeDataString(language) + "&q=" + Uri.EscapeDataString(text);
            byte[] audio = await Http.GetByteArrayAsync(url).ConfigureAwait(true);
            if (audio == null || audio.Length < 1000) throw new InvalidOperationException("Empty audio response.");

            string file = Path.Combine(Path.GetTempPath(), "qalam-tts-" + Guid.NewGuid().ToString("N") + ".mp3");
            File.WriteAllBytes(file, audio);

            if (_player == null)
            {
                _player = new MediaPlayer();
                _player.MediaFailed += delegate(object sender, ExceptionEventArgs e)
                {
                    Logger.Write("tts playback: " + e.ErrorException.Message);
                };
            }

            _player.Close();
            string previous = _lastFile;
            _lastFile = file;
            _player.Open(new Uri(file));
            _player.Play();

            if (previous != null)
            {
                try { File.Delete(previous); }
                catch { }
            }
        }

        // Fuṣḥā speaks the text as written. A lahja register first asks the
        // offline worker for the dialect vocalization, which is what makes the
        // reader drop case endings and use the spoken vowels.
        private static string PrepareSpoken(string text, string language)
        {
            if (Vocalizer == null) return text;
            if (!Pronunciation.IsArabic(language)) return text;
            if (!Pronunciation.NeedsDialectVocalization(Pronunciation.Current)) return text;
            foreach (char c in text)
                if (ArabicText.IsMark(c)) return text;
            try { return Vocalizer(text, Pronunciation.Current); }
            catch (Exception ex) { Logger.Write("tts lahja: " + ex.Message); return text; }
        }

        private static HttpClient CreateClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            return client;
        }
    }

    internal sealed class TashkeelClient : IDisposable
    {
        private readonly object _gate = new object();
        private readonly string _python;
        private readonly string _script;
        private Process _process;
        private StreamWriter _stdin;
        private StreamReader _stdout;
        private int _nextId;

        public string PythonPath { get { return _python; } }

        public TashkeelClient(Config config)
        {
            _python = Paths.ResolvePython(config.PythonPath);
            _script = Paths.ScriptPath;
        }

        public void Ping()
        {
            lock (_gate)
            {
                Dictionary<string, object> request = new Dictionary<string, object>();
                request["id"] = ++_nextId;
                request["cmd"] = "ping";
                Send(request);
                Receive(20000);
            }
        }

        public string Diacritize(string text)
        {
            return TextCommand("tashkeel", text, "Tashkeel", null);
        }

        // A lahja register asks the worker for the spoken form: dialect vowels
        // and no case endings, instead of a Fuṣḥā reading.
        public string Diacritize(string text, string lahja)
        {
            return TextCommand("tashkeel", text, "Tashkeel", Pronunciation.Normalize(lahja));
        }

        public string Pinyin(string text)
        {
            return TextCommand("pinyin", text, "Pinyin", null);
        }

        private string TextCommand(string command, string text, string name, string lahja)
        {
            lock (_gate)
            {
                Dictionary<string, object> request = new Dictionary<string, object>();
                request["id"] = ++_nextId;
                request["cmd"] = command;
                request["text"] = text;
                if (!string.IsNullOrEmpty(lahja)) request["lahja"] = lahja;
                Send(request);

                Dictionary<string, object> response = Receive(20000);
                bool ok = response.ContainsKey("ok") && Convert.ToBoolean(response["ok"]);
                if (!ok)
                {
                    string error = response.ContainsKey("error") ? Convert.ToString(response["error"]) : "unknown error";
                    throw new InvalidOperationException(name + " failed: " + error);
                }
                return CleanOutput(response.ContainsKey("text") ? Convert.ToString(response["text"]) : "");
            }
        }

        private static string CleanOutput(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsControl(c)) continue;
                builder.Append(c);
            }
            return builder.ToString().Trim();
        }

        private void Send(Dictionary<string, object> request)
        {
            EnsureStarted();
            _stdin.WriteLine(new JavaScriptSerializer().Serialize(request));
            _stdin.Flush();
        }

        private Dictionary<string, object> Receive(int timeoutMs)
        {
            Task<string> read = _stdout.ReadLineAsync();
            if (!read.Wait(timeoutMs))
            {
                Kill();
                throw new TimeoutException("Tashkeel worker did not respond.");
            }
            string line = read.Result;
            if (line == null)
            {
                Kill();
                throw new InvalidOperationException("Tashkeel worker stopped.");
            }
            Dictionary<string, object> response = new JavaScriptSerializer().DeserializeObject(line) as Dictionary<string, object>;
            if (response == null)
                throw new InvalidOperationException("Bad tashkeel response.");
            return response;
        }

        private void EnsureStarted()
        {
            if (_process != null && !_process.HasExited) return;
            Kill();

            if (!File.Exists(_script))
                throw new FileNotFoundException("Tashkeel worker script not found: " + _script);

            ProcessStartInfo info = new ProcessStartInfo(_python, "-u \"" + _script + "\"");
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardInput = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.WorkingDirectory = Path.GetDirectoryName(_script);
            info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            info.EnvironmentVariables["PYTHONUTF8"] = "1";

            Process process = new Process();
            process.StartInfo = info;
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data)) Logger.Write("tashkeel stderr: " + e.Data);
            };
            process.Start();
            process.BeginErrorReadLine();

            _process = process;
            _stdin = new StreamWriter(process.StandardInput.BaseStream, Utf8.NoBom);
            _stdin.NewLine = "\n";
            _stdin.AutoFlush = false;
            _stdout = new StreamReader(process.StandardOutput.BaseStream, Utf8.NoBom);
            Logger.Write("tashkeel worker started: " + _python);
        }

        private void Kill()
        {
            try { if (_stdin != null) _stdin.Dispose(); }
            catch { }
            try { if (_stdout != null) _stdout.Dispose(); }
            catch { }
            try { if (_process != null && !_process.HasExited) _process.Kill(); }
            catch { }
            try { if (_process != null) _process.Dispose(); }
            catch { }
            _stdin = null;
            _stdout = null;
            _process = null;
        }

        public void Dispose()
        {
            lock (_gate) { Kill(); }
        }
    }

    internal static class Ui
    {
        // These brushes are deliberately mutable: a theme change updates the
        // palette in place, so existing controls follow immediately without
        // rebuilding the window tree.
        public static readonly SolidColorBrush Window = ThemeBrush(0xFF, 0x0E, 0x12, 0x18);
        public static readonly SolidColorBrush Mark = ThemeBrush(0xFF, 0x18, 0x1E, 0x28);
        public static readonly SolidColorBrush Input = ThemeBrush(0xFF, 0x15, 0x1A, 0x22);
        public static readonly SolidColorBrush InputBorder = ThemeBrush(0xFF, 0x2A, 0x34, 0x42);
        public static readonly SolidColorBrush Menu = ThemeBrush(0xFF, 0x1A, 0x20, 0x2A);
        public static readonly SolidColorBrush Deep = ThemeBrush(0xFF, 0x0B, 0x0F, 0x16);
        public static readonly SolidColorBrush Popup = ThemeBrush(0xF8, 0x0E, 0x12, 0x1A);
        public static readonly SolidColorBrush PopupLine = ThemeBrush(0x26, 0xFF, 0xFF, 0xFF);
        public static readonly SolidColorBrush Accent = ThemeBrush(0xFF, 0xE8, 0xC8, 0x78);
        public static readonly SolidColorBrush Gold = ThemeBrush(0xFF, 0xD9, 0xB9, 0x6A);
        public static readonly SolidColorBrush Bright = ThemeBrush(0xFF, 0xF2, 0xF4, 0xF8);
        public static readonly SolidColorBrush Muted = ThemeBrush(0xFF, 0x92, 0x9C, 0xAE);
        public static readonly SolidColorBrush Faint = ThemeBrush(0xFF, 0x63, 0x6C, 0x7C);
        public static readonly SolidColorBrush Line = ThemeBrush(0x1A, 0xFF, 0xFF, 0xFF);
        public static readonly SolidColorBrush Panel = ThemeBrush(0x12, 0xFF, 0xFF, 0xFF);
        public static readonly SolidColorBrush TashText = ThemeBrush(0xFF, 0xF0, 0xE2, 0xC2);
        public static readonly SolidColorBrush ErrorBrush = ThemeBrush(0xFF, 0xFF, 0x8A, 0x80);
        public static readonly SolidColorBrush WarnBrush = ThemeBrush(0xFF, 0xE0, 0xB0, 0x60);
        public static readonly SolidColorBrush GoldWash = ThemeBrush(0x18, 0xD9, 0xB9, 0x6A);
        public static readonly SolidColorBrush Selection = ThemeBrush(0x55, 0xD9, 0xB9, 0x6A);
        public static readonly SolidColorBrush HoverWash = ThemeBrush(0x1F, 0xFF, 0xFF, 0xFF);
        public static readonly SolidColorBrush Outline = ThemeBrush(0x33, 0xD9, 0xB9, 0x6A);
        public static readonly SolidColorBrush OutlineHover = ThemeBrush(0x1A, 0xD9, 0xB9, 0x6A);
        public static readonly SolidColorBrush Primary = ThemeBrush(0xFF, 0xE8, 0xC8, 0x78);
        public static readonly SolidColorBrush PrimaryHover = ThemeBrush(0xFF, 0xF2, 0xD9, 0x92);
        public static readonly SolidColorBrush PrimaryText = ThemeBrush(0xFF, 0x14, 0x18, 0x20);
        public static readonly SolidColorBrush SelectedRow = ThemeBrush(0x1F, 0xD9, 0xB9, 0x6A);
        public static readonly SolidColorBrush StrokeHint = ThemeBrush(0x1B, 0xF2, 0xF4, 0xF8);
        public static readonly SolidColorBrush Canvas = ThemeBrush(0xFF, 0x0E, 0x12, 0x1A);

        internal static SolidColorBrush MakeBrush(byte a, byte r, byte g, byte b)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }

        private static SolidColorBrush ThemeBrush(byte a, byte r, byte g, byte b)
        {
            return new SolidColorBrush(Color.FromArgb(a, r, g, b));
        }

        internal static void ApplyPalette(ThemePalette p)
        {
            Set(Window, p.Window);
            Set(Mark, p.Mark);
            Set(Input, p.Input);
            Set(InputBorder, p.InputBorder);
            Set(Menu, p.Menu);
            Set(Deep, p.Deep);
            Set(Popup, p.Popup);
            Set(PopupLine, p.PopupLine);
            Set(Accent, p.Accent);
            Set(Gold, p.Gold);
            Set(Bright, p.Bright);
            Set(Muted, p.Muted);
            Set(Faint, p.Faint);
            Set(Line, p.Line);
            Set(Panel, p.Panel);
            Set(TashText, p.TashText);
            Set(ErrorBrush, p.Error);
            Set(WarnBrush, p.Warn);
            Set(GoldWash, p.GoldWash);
            Set(Selection, p.Selection);
            Set(HoverWash, p.HoverWash);
            Set(Outline, p.Outline);
            Set(OutlineHover, p.OutlineHover);
            Set(Primary, p.Primary);
            Set(PrimaryHover, p.PrimaryHover);
            Set(PrimaryText, p.PrimaryText);
            Set(SelectedRow, p.SelectedRow);
            Set(StrokeHint, p.StrokeHint);
            Set(Canvas, p.Canvas);
        }

        private static void Set(SolidColorBrush brush, Color color)
        {
            if (brush.Color != color) brush.Color = color;
        }

        public static TextBlock Label(string text, double size, SolidColorBrush brush, FontWeight weight)
        {
            TextBlock block = new TextBlock();
            block.Text = text;
            block.FontSize = size;
            block.Foreground = brush;
            block.FontWeight = weight;
            block.FontFamily = new FontFamily("Segoe UI");
            TextOptions.SetTextFormattingMode(block, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(block, TextRenderingMode.ClearType);
            return block;
        }

        public static Button PrimaryButton(string label, RoutedEventHandler onClick)
        {
            Button button = new Button();
            button.Content = label;
            button.FontSize = 12.5;
            button.FontWeight = FontWeights.SemiBold;
            button.Foreground = PrimaryText;
            button.Cursor = Cursors.Hand;
            button.Focusable = false;
            button.Padding = new Thickness(0);
            button.Template = SolidTemplate(
                Primary,
                PrimaryHover,
                new Thickness(14, 8, 14, 8));
            button.Click += onClick;
            return button;
        }

        public static Button SmallOutlineButton(string label, RoutedEventHandler onClick)
        {
            Button button = new Button();
            button.Content = label;
            button.FontSize = 10.5;
            button.FontWeight = FontWeights.SemiBold;
            button.Foreground = Gold;
            button.Cursor = Cursors.Hand;
            button.Focusable = false;
            button.Padding = new Thickness(0);
            button.Template = OutlineTemplate(new Thickness(9, 4, 9, 4));
            button.Click += onClick;
            return button;
        }

        public static Button IconButton(string glyph, RoutedEventHandler onClick)
        {
            Button button = new Button();
            button.Content = glyph;
            button.FontSize = 13;
            button.Foreground = Faint;
            button.Cursor = Cursors.Hand;
            button.Focusable = false;
            button.Width = 22;
            button.Height = 22;
            button.Padding = new Thickness(0);

            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border), "Bd");
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            template.VisualTree = border;

            Trigger hover = new Trigger();
            hover.Property = Button.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(Border.BackgroundProperty, HoverWash, "Bd"));
            template.Triggers.Add(hover);
            button.Template = template;
            button.Click += onClick;
            return button;
        }

        private static ControlTemplate SolidTemplate(SolidColorBrush background, SolidColorBrush hoverBackground, Thickness padding)
        {
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border), "Bd");
            border.SetValue(Border.BackgroundProperty, background);
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(Border.PaddingProperty, padding);
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            template.VisualTree = border;

            Trigger hoverTrigger = new Trigger();
            hoverTrigger.Property = Button.IsMouseOverProperty;
            hoverTrigger.Value = true;
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBackground, "Bd"));
            template.Triggers.Add(hoverTrigger);

            Trigger disabled = new Trigger();
            disabled.Property = Button.IsEnabledProperty;
            disabled.Value = false;
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45, "Bd"));
            template.Triggers.Add(disabled);
            return template;
        }

        private static ControlTemplate OutlineTemplate(Thickness padding)
        {
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border), "Bd");
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            border.SetValue(Border.BorderBrushProperty, Outline);
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            border.SetValue(Border.PaddingProperty, padding);
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            template.VisualTree = border;

            Trigger hover = new Trigger();
            hover.Property = Button.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(Border.BackgroundProperty, OutlineHover, "Bd"));
            template.Triggers.Add(hover);
            return template;
        }
    }

    public sealed class TranslationResult
    {
        public string SourceText;
        public string Translation;
        public string Detected;
        public string Target;
        public string Error;
        public string Engine;
        public bool Truncated;
        public bool TashkeelOnly;
        public bool AutoTarget;
        public int MaxChars;
    }

    internal sealed class PopupWindow : Window
    {
        private const double WindowWidth = 528;

        public event Action<string> TargetChanged;
        public event Action<string> CharactersRequested;

        private readonly Border _root;
        private readonly TextBlock _source;
        private readonly TextBlock _translation;
        private readonly TextBlock _langPair;
        private readonly Button _targetButton;
        private readonly ContextMenu _targetMenu;
        private readonly TextBlock _status;
        private readonly Border _tashkeelPanel;
        private readonly TextBlock _tashkeelLabel;
        private readonly TextBlock _tashkeel;
        private readonly Button _copyButton;
        private readonly Button _listenButton;
        private readonly Button _charactersButton;
        private readonly Border _synonymsPanel;
        private readonly TextBlock _synonyms;
        private readonly Border _grammarPanel;
        private readonly TextBlock _grammarTitle;
        private readonly TextBlock _grammarBody;
        private readonly Border _examplesPanel;
        private readonly StackPanel _examples;
        private readonly TextBlock _examplesCredit;
        private readonly ScrollViewer _bodyScroll;

        private string _translationText = "";
        private string _tashkeelText = "";
        private string _tashkeelSpeech = "";
        private string _tashkeelSpeechLang = "ar";
        private string _tashkeelKind = "TASHKEEL";
        private string _speechLang = "";
        private string _cachedTashkeel = "";
        private string _cachedTashkeelRole = "";
        private string _cachedTashkeelKind = "TASHKEEL";
        private string _cachedTashkeelSpeech = "";
        private string _cachedTashkeelSpeechLang = "ar";
        private List<string> _cachedSynonyms = new List<string>();
        private List<ExamplePair> _cachedExamples = new List<ExamplePair>();
        private GrammarInfo _cachedGrammar;
        private DateTime _shownAt = DateTime.MinValue;
        private bool _suppressDeactivate;
        private bool _wasActive;
        private string _targetCode = "";

        public bool Headless;

        public PopupWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.Height;
            Width = WindowWidth;
            MaxHeight = 720;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -20000;
            Top = -20000;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            StackPanel stack = new StackPanel();

            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel brand = new StackPanel();
            brand.Orientation = Orientation.Horizontal;
            brand.VerticalAlignment = VerticalAlignment.Center;
            System.Windows.Shapes.Ellipse dot = new System.Windows.Shapes.Ellipse();
            dot.Width = 7;
            dot.Height = 7;
            dot.Fill = Ui.Accent;
            dot.Margin = new Thickness(0, 0, 8, 0);
            dot.VerticalAlignment = VerticalAlignment.Center;
            brand.Children.Add(dot);
            brand.Children.Add(Ui.Label("QALAM", 10.5, Ui.Muted, FontWeights.SemiBold));
            Grid.SetColumn(brand, 0);
            header.Children.Add(brand);

            _langPair = Ui.Label("", 10.5, Ui.Gold, FontWeights.SemiBold);
            _langPair.HorizontalAlignment = HorizontalAlignment.Right;
            _langPair.VerticalAlignment = VerticalAlignment.Center;
            _langPair.Margin = new Thickness(0, 0, 10, 0);
            Grid.SetColumn(_langPair, 1);
            header.Children.Add(_langPair);

            _targetButton = Ui.SmallOutlineButton("EN", delegate { OpenTargetMenu(); });
            _targetButton.FontSize = 10.5;
            _targetButton.Padding = new Thickness(0);
            _targetButton.MinWidth = 78;
            _targetButton.Margin = new Thickness(0, 0, 6, 0);
            _targetButton.VerticalAlignment = VerticalAlignment.Center;
            _targetButton.ToolTip = "Change the target language";
            Grid.SetColumn(_targetButton, 2);
            header.Children.Add(_targetButton);

            _targetMenu = BuildTargetMenu();

            Button close = Ui.IconButton("\u2715", delegate { Hide(); });
            Grid.SetColumn(close, 3);
            header.Children.Add(close);
            stack.Children.Add(header);

            StackPanel body = new StackPanel();

            _source = Ui.Label("", 12.5, Ui.Muted, FontWeights.Normal);
            _source.TextWrapping = TextWrapping.Wrap;
            _source.MaxHeight = 54;
            _source.Margin = new Thickness(0, 14, 0, 0);
            _source.ClipToBounds = true;
            body.Children.Add(_source);

            Border divider = new Border();
            divider.Height = 1;
            divider.Background = Ui.Line;
            divider.Margin = new Thickness(0, 14, 0, 0);
            body.Children.Add(divider);

            _translation = Ui.Label("", 21, Ui.Bright, FontWeights.Normal);
            _translation.TextWrapping = TextWrapping.Wrap;
            _translation.LineHeight = 30;
            _translation.Margin = new Thickness(0, 12, 0, 0);
            body.Children.Add(_translation);

            _status = Ui.Label("", 11.5, Ui.Muted, FontWeights.Normal);
            _status.TextWrapping = TextWrapping.Wrap;
            _status.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(_status);

            _tashkeelPanel = new Border();
            _tashkeelPanel.Background = Ui.Panel;
            _tashkeelPanel.CornerRadius = new CornerRadius(10);
            _tashkeelPanel.Padding = new Thickness(12, 9, 12, 12);
            _tashkeelPanel.Margin = new Thickness(0, 14, 0, 0);
            _tashkeelPanel.Visibility = Visibility.Collapsed;

            StackPanel tashkeelStack = new StackPanel();
            Grid tashkeelHeader = new Grid();
            tashkeelHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tashkeelHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tashkeelHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tashkeelHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _tashkeelLabel = Ui.Label("TASHKEEL", 9.5, Ui.Gold, FontWeights.SemiBold);
            _tashkeelLabel.VerticalAlignment = VerticalAlignment.Center;
            tashkeelHeader.Children.Add(_tashkeelLabel);

            _charactersButton = Ui.SmallOutlineButton("Characters", delegate
            {
                if (CharactersRequested != null) CharactersRequested(_tashkeelSpeech);
            });
            _charactersButton.Margin = new Thickness(0, 0, 6, 0);
            _charactersButton.Visibility = Visibility.Collapsed;
            Grid.SetColumn(_charactersButton, 1);
            tashkeelHeader.Children.Add(_charactersButton);

            Button listenTashkeel = Ui.SmallOutlineButton("Listen", delegate { Speak(_tashkeelSpeech, _tashkeelSpeechLang, _tashkeelKind); });
            listenTashkeel.Margin = new Thickness(0, 0, 6, 0);
            Grid.SetColumn(listenTashkeel, 2);
            tashkeelHeader.Children.Add(listenTashkeel);

            Button copyTashkeel = Ui.SmallOutlineButton("Copy", delegate { Copy(_tashkeelText, _tashkeelKind); });
            Grid.SetColumn(copyTashkeel, 3);
            tashkeelHeader.Children.Add(copyTashkeel);
            tashkeelStack.Children.Add(tashkeelHeader);

            _tashkeel = Ui.Label("", 19, Ui.TashText, FontWeights.Normal);
            _tashkeel.TextWrapping = TextWrapping.Wrap;
            _tashkeel.LineHeight = 33;
            _tashkeel.Margin = new Thickness(0, 6, 0, 0);
            _tashkeel.FlowDirection = FlowDirection.RightToLeft;
            _tashkeel.TextAlignment = TextAlignment.Right;

            tashkeelStack.Children.Add(_tashkeel);

            _tashkeelPanel.Child = tashkeelStack;
            body.Children.Add(_tashkeelPanel);

            _grammarPanel = new Border();
            _grammarPanel.Background = Ui.Panel;
            _grammarPanel.CornerRadius = new CornerRadius(10);
            _grammarPanel.Padding = new Thickness(12, 9, 12, 11);
            _grammarPanel.Margin = new Thickness(0, 12, 0, 0);
            _grammarPanel.Visibility = Visibility.Collapsed;

            StackPanel grammarStack = new StackPanel();
            _grammarTitle = Ui.Label("GRAMMAR", 9.5, Ui.Gold, FontWeights.SemiBold);
            grammarStack.Children.Add(_grammarTitle);
            _grammarBody = Ui.Label("", 11.5, Ui.Bright, FontWeights.Normal);
            _grammarBody.TextWrapping = TextWrapping.Wrap;
            _grammarBody.LineHeight = 19;
            _grammarBody.Margin = new Thickness(0, 5, 0, 0);
            grammarStack.Children.Add(_grammarBody);
            TextBlock grammarCredit = Ui.Label("Wiktionary via Kaikki  \u00b7  CC BY-SA", 8.5, Ui.Faint, FontWeights.Normal);
            grammarCredit.Margin = new Thickness(0, 6, 0, 0);
            grammarStack.Children.Add(grammarCredit);
            _grammarPanel.Child = grammarStack;
            body.Children.Add(_grammarPanel);

            _synonymsPanel = new Border();
            _synonymsPanel.Background = Ui.Panel;
            _synonymsPanel.CornerRadius = new CornerRadius(10);
            _synonymsPanel.Padding = new Thickness(12, 9, 12, 11);
            _synonymsPanel.Margin = new Thickness(0, 12, 0, 0);
            _synonymsPanel.Visibility = Visibility.Collapsed;

            StackPanel synonymsStack = new StackPanel();
            synonymsStack.Children.Add(Ui.Label("ALSO", 9.5, Ui.Gold, FontWeights.SemiBold));
            _synonyms = Ui.Label("", 13, Ui.Bright, FontWeights.Normal);
            _synonyms.TextWrapping = TextWrapping.Wrap;
            _synonyms.LineHeight = 22;
            _synonyms.Margin = new Thickness(0, 5, 0, 0);
            synonymsStack.Children.Add(_synonyms);
            _synonymsPanel.Child = synonymsStack;
            body.Children.Add(_synonymsPanel);

            _examplesPanel = new Border();
            _examplesPanel.Background = Ui.Panel;
            _examplesPanel.CornerRadius = new CornerRadius(10);
            _examplesPanel.Padding = new Thickness(12, 9, 12, 11);
            _examplesPanel.Margin = new Thickness(0, 12, 0, 0);
            _examplesPanel.Visibility = Visibility.Collapsed;

            StackPanel examplesStack = new StackPanel();
            examplesStack.Children.Add(Ui.Label("IN CONTEXT", 9.5, Ui.Gold, FontWeights.SemiBold));
            _examples = new StackPanel();
            _examples.Margin = new Thickness(0, 2, 0, 0);
            examplesStack.Children.Add(_examples);
            _examplesCredit = Ui.Label("Examples: Tatoeba (CC BY)  \u00b7  MyMemory", 8.5, Ui.Faint, FontWeights.Normal);
            _examplesCredit.Margin = new Thickness(0, 7, 0, 0);
            examplesStack.Children.Add(_examplesCredit);
            _examplesPanel.Child = examplesStack;
            body.Children.Add(_examplesPanel);

            _bodyScroll = new ScrollViewer();
            _bodyScroll.MaxHeight = 560;
            _bodyScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _bodyScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _bodyScroll.Focusable = false;
            _bodyScroll.Content = body;
            stack.Children.Add(_bodyScroll);

            Grid footer = new Grid();
            footer.Margin = new Thickness(0, 16, 0, 0);
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock hint = Ui.Label("Highlight, then Ctrl + C + C anywhere  \u00b7  Esc to dismiss", 10.5, Ui.Faint, FontWeights.Normal);
            hint.VerticalAlignment = VerticalAlignment.Center;
            footer.Children.Add(hint);

            StackPanel footerButtons = new StackPanel();
            footerButtons.Orientation = Orientation.Horizontal;
            footerButtons.VerticalAlignment = VerticalAlignment.Center;

            _listenButton = Ui.SmallOutlineButton("Listen", delegate { Speak(_translationText, _speechLang, "Translation"); });
            _listenButton.Margin = new Thickness(0, 0, 8, 0);
            _listenButton.IsEnabled = false;
            footerButtons.Children.Add(_listenButton);

            _copyButton = Ui.PrimaryButton("Copy translation", delegate { Copy(_translationText, "Translation"); });
            footerButtons.Children.Add(_copyButton);

            Grid.SetColumn(footerButtons, 1);
            footer.Children.Add(footerButtons);
            stack.Children.Add(footer);

            _root = new Border();
            _root.Background = Ui.Popup;
            _root.CornerRadius = new CornerRadius(16);
            _root.BorderBrush = Ui.PopupLine;
            _root.BorderThickness = new Thickness(1);
            _root.Margin = new Thickness(26);
            _root.Padding = new Thickness(22, 18, 22, 18);
            _root.Child = stack;

            DropShadowEffect shadow = new DropShadowEffect();
            shadow.BlurRadius = 34;
            shadow.ShadowDepth = 8;
            shadow.Opacity = 0.6;
            shadow.Color = Colors.Black;
            _root.Effect = shadow;

            _root.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    try { DragMove(); }
                    catch { }
                }
            };
            Content = _root;

            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    Hide();
                    e.Handled = true;
                }
            };
            Activated += delegate
            {
                _wasActive = true;
                _shownAt = DateTime.UtcNow;
            };
            Deactivated += delegate
            {
                // The first show can race with the window that had focus. Only
                // dismiss after this popup has actually held activation once.
                if (!_suppressDeactivate && _wasActive &&
                    DateTime.UtcNow - _shownAt > TimeSpan.FromMilliseconds(600))
                    Hide();
            };
            SizeChanged += delegate
            {
                if (IsVisible) Dispatcher.BeginInvoke(new Action(PlaceNearCursor), DispatcherPriority.Loaded);
            };
        }

        private ContextMenu BuildTargetMenu()
        {
            ContextMenu menu = new ContextMenu();
            menu.Background = Ui.Menu;
            menu.Foreground = Ui.Bright;
            menu.BorderBrush = Ui.InputBorder;

            MenuItem auto = new MenuItem();
            auto.Header = "Auto (Arabic \u21c4 English)";
            auto.Tag = "";
            auto.Click += delegate { SelectTarget(""); };
            menu.Items.Add(auto);
            menu.Items.Add(new Separator());

            for (int i = 0; i < LanguageCatalog.Count; i++)
            {
                string code = LanguageCatalog.Code(i);
                string name = LanguageCatalog.Name(i);
                MenuItem item = new MenuItem();
                item.Header = name + "  (" + code + ")";
                item.Tag = code;
                item.Click += delegate { SelectTarget(code); };
                menu.Items.Add(item);
            }


            menu.Opened += delegate
            {
                // A ContextMenu takes activation away from the popup. Keep the
                // deactivate handler from hiding the menu's owner.
                _suppressDeactivate = true;
                foreach (object entry in menu.Items)
                {
                    MenuItem item = entry as MenuItem;
                    if (item == null || item.Tag == null) continue;
                    item.IsChecked = string.Equals((string)item.Tag, _targetCode,
                        StringComparison.OrdinalIgnoreCase);
                }
            };
            menu.Closed += delegate
            {
                _suppressDeactivate = false;
                _shownAt = DateTime.UtcNow;
            };
            return menu;
        }

        private void OpenTargetMenu()
        {
            _suppressDeactivate = true;
            _targetMenu.PlacementTarget = _targetButton;
            _targetMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            _targetMenu.IsOpen = true;
        }

        internal int TargetMenuItems
        {
            get { return _targetMenu == null ? 0 : _targetMenu.Items.Count; }
        }

        internal void SelectTarget(string code)
        {
            _targetCode = code == null ? "" : code;
            UpdateTargetButton();
            if (TargetChanged != null) TargetChanged(_targetCode);
        }

        private void UpdateTargetButton()
        {
            _targetButton.Content = (string.IsNullOrEmpty(_targetCode) ? "AUTO" : _targetCode.ToUpperInvariant()) + "  \u25be";
        }

        public void ShowLoading(string source)
        {
            _source.Visibility = Visibility.Visible;
            _source.Text = Normalize(source);
            bool arabic = ArabicText.ContainsArabic(source);
            _source.FlowDirection = arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            _source.TextAlignment = arabic ? TextAlignment.Right : TextAlignment.Left;

            _langPair.Text = "\u2026";
            _translation.Text = "Translating\u2026";
            _translation.Foreground = Ui.Muted;
            _translation.FontSize = 18;
            _translation.FlowDirection = FlowDirection.LeftToRight;
            _translation.TextAlignment = TextAlignment.Left;
            _translationText = "";
            _speechLang = "";
            _copyButton.IsEnabled = false;
            _listenButton.IsEnabled = false;
            _status.Text = "";
            UpdateTargetButton();
            _tashkeelPanel.Visibility = Visibility.Collapsed;
            ResetTranscription();
            _synonymsPanel.Visibility = Visibility.Collapsed;
            _grammarPanel.Visibility = Visibility.Collapsed;
            _examplesPanel.Visibility = Visibility.Collapsed;
            _cachedTashkeel = "";
            _cachedTashkeelRole = "";
            _cachedSynonyms.Clear();
            _cachedExamples.Clear();
            _cachedGrammar = null;
            Present();
        }

        public void ShowResult(TranslationResult result)
        {
            _source.Visibility = Visibility.Visible;
            _source.Text = Normalize(result.SourceText);
            bool sourceArabic = ArabicText.ContainsArabic(result.SourceText);
            _source.FlowDirection = sourceArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            _source.TextAlignment = sourceArabic ? TextAlignment.Right : TextAlignment.Left;

            string detected = string.IsNullOrEmpty(result.Detected) ? "auto" : result.Detected.ToUpperInvariant();
            string target = string.IsNullOrEmpty(result.Target) ? "" : result.Target.ToUpperInvariant();
            _targetCode = result.AutoTarget ? "" : (result.Target == null ? "" : result.Target);
            UpdateTargetButton();
            _langPair.Text = result.TashkeelOnly ? detected + "  \u00b7  TASHKEEL" : detected + "   \u2192   " + target;

            _translation.FontSize = 21;
            _copyButton.IsEnabled = true;
            _speechLang = result.TashkeelOnly ? (result.Detected == null ? "ar" : result.Detected) : (result.Target == null ? "" : result.Target);
            _listenButton.IsEnabled = result.Error == null && !string.IsNullOrEmpty(result.Translation);

            if (result.TashkeelOnly)
            {
                _source.Visibility = Visibility.Collapsed;
                _translation.Text = result.SourceText;
                _translation.Foreground = Ui.Bright;
                _translation.FlowDirection = FlowDirection.RightToLeft;
                _translation.TextAlignment = TextAlignment.Right;
                _translationText = result.SourceText;
                _status.Text = "Diacritizing selected Arabic text\u2026";
                _status.Foreground = Ui.Muted;
            }
            else
            {
                bool arabic = ArabicText.ContainsArabic(result.Translation);
                _translation.Text = string.IsNullOrEmpty(result.Translation) ? "\u2014" : result.Translation;
                _translation.Foreground = Ui.Bright;
                _translation.FlowDirection = arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                _translation.TextAlignment = arabic ? TextAlignment.Right : TextAlignment.Left;
                _translationText = result.Translation;

                if (result.Error != null)
                {
                    _status.Text = result.Error;
                    _status.Foreground = Ui.ErrorBrush;
                }
                else if (result.Truncated)
                {
                    _status.Text = "Selection trimmed to " + result.MaxChars + " characters.";
                    _status.Foreground = Ui.WarnBrush;
                }
                else if (!string.IsNullOrEmpty(result.Engine))
                {
                    if (result.Engine == "phrasebook") _status.Text = "Colloquial meaning";
                    else if (result.Engine == "offline") _status.Text = "Offline translation on this device";
                    else _status.Text = "Slang register preserved on this device";
                    _status.Foreground = Ui.Muted;
                }
                else
                {
                    _status.Text = "";
                }
            }

            _tashkeelPanel.Visibility = Visibility.Collapsed;
            ResetTranscription();
            _synonymsPanel.Visibility = Visibility.Collapsed;
            _grammarPanel.Visibility = Visibility.Collapsed;
            _examplesPanel.Visibility = Visibility.Collapsed;
            _cachedSynonyms.Clear();
            _cachedExamples.Clear();
            _cachedGrammar = null;
            Present();
        }

        public void ShowTashkeel(string text, string role)
        {
            // The register also picks the speech tag, so Listen reads the same
            // lahja the tashkeel was produced in.
            ShowTranscription("TASHKEEL", text, role, true, text,
                Pronunciation.TtsLanguage(Pronunciation.Current));
        }

        public void ShowPinyin(string pinyin, string source, string role)
        {
            ShowTranscription("PINYIN", pinyin, role, false, source, "zh-CN");
        }

        private void ShowTranscription(string kind, string text, string role, bool rightToLeft,
                                       string speech, string speechLang)
        {
            _tashkeelKind = kind;
            _tashkeelSpeech = speech;
            _tashkeelSpeechLang = speechLang;
            _cachedTashkeel = text;
            _cachedTashkeelRole = role;
            _cachedTashkeelKind = kind;
            _cachedTashkeelSpeech = speech;
            _cachedTashkeelSpeechLang = speechLang;
            _tashkeelLabel.Text = kind + "  \u00b7  " + role.ToUpperInvariant() + "  \u00b7  OFFLINE";
            _charactersButton.Visibility = kind == "PINYIN" ? Visibility.Visible : Visibility.Collapsed;
            _tashkeel.Text = text;
            _tashkeel.FontSize = rightToLeft ? 19 : 16;
            _tashkeel.LineHeight = rightToLeft ? 33 : 27;
            _tashkeel.FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            _tashkeel.TextAlignment = rightToLeft ? TextAlignment.Right : TextAlignment.Left;
            _tashkeelText = text;
            _tashkeelPanel.Visibility = Visibility.Visible;
            if (_status.Text.StartsWith("Diacritizing", StringComparison.Ordinal) ||
                _status.Text.StartsWith("Transcribing", StringComparison.Ordinal))
                _status.Text = "";
            Present();
        }

        private void ResetTranscription()
        {
            _tashkeelText = "";
            _tashkeelSpeech = "";
            _tashkeelSpeechLang = "ar";
            _tashkeelKind = "TASHKEEL";
            if (_charactersButton != null) _charactersButton.Visibility = Visibility.Collapsed;
            _cachedTashkeel = "";
            _cachedTashkeelRole = "";
            _cachedTashkeelKind = "TASHKEEL";
            _cachedTashkeelSpeech = "";
            _cachedTashkeelSpeechLang = "ar";
        }

        public void ShowTranscriptionUnavailable(string kind)
        {
            _tashkeelPanel.Visibility = Visibility.Collapsed;
            _status.Text = kind + " unavailable \u2014 the offline worker is missing or failed to start.";
            _status.Foreground = Ui.WarnBrush;
            Present();
        }

        public void ShowGrammar(GrammarInfo info)
        {
            _cachedGrammar = info;
            if (info == null || !info.HasContent)
            {
                _grammarPanel.Visibility = Visibility.Collapsed;
                return;
            }

            string title = "GRAMMAR  \u00b7  " + info.Language.ToUpperInvariant();
            if (!string.IsNullOrEmpty(info.PartOfSpeech)) title += "  \u00b7  " + info.PartOfSpeech.ToUpperInvariant();
            if (info.FromCache) title += "  \u00b7  CACHED";
            _grammarTitle.Text = title;

            // Keep the card left-to-right: it mixes English labels with Arabic
            // forms, and a right-to-left base direction reverses the label order
            // and moves the definition bullets to the end of the line.
            _grammarBody.Text = GrammarText.Summary(info);
            _grammarBody.FlowDirection = FlowDirection.LeftToRight;
            _grammarBody.TextAlignment = TextAlignment.Left;
            _grammarPanel.Visibility = Visibility.Visible;
            Present();
        }

        public void ShowSynonyms(List<string> words)
        {
            _cachedSynonyms = words == null ? new List<string>() : words;
            if (_cachedSynonyms.Count == 0)
            {
                _synonymsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            _synonyms.Text = string.Join("   \u00b7   ", _cachedSynonyms.ToArray());
            bool arabic = ArabicText.ContainsArabic(_synonyms.Text);
            _synonyms.FlowDirection = arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            _synonyms.TextAlignment = arabic ? TextAlignment.Right : TextAlignment.Left;
            _synonymsPanel.Visibility = Visibility.Visible;
            Present();
        }

        public void ShowExamples(List<ExamplePair> examples)
        {
            _cachedExamples = examples == null ? new List<ExamplePair>() : examples;
            _examples.Children.Clear();
            if (_cachedExamples.Count == 0)
            {
                _examplesPanel.Visibility = Visibility.Collapsed;
                return;
            }

            bool hasPhrasebook = false;
            bool hasConcordance = false;
            bool hasOnline = false;
            List<string> credits = new List<string>();
            List<string> corpora = new List<string>();
            for (int i = 0; i < _cachedExamples.Count; i++)
            {
                ExamplePair pair = _cachedExamples[i];
                if (pair.FromPhrasebook) hasPhrasebook = true;
                else if (pair.FromConcordance)
                {
                    hasConcordance = true;
                    string corpus = string.IsNullOrEmpty(pair.Origin) ? "Open corpus" : pair.Origin;
                    if (!string.IsNullOrEmpty(pair.License)) corpus += " (" + pair.License + ")";
                    if (!corpora.Contains(corpus)) corpora.Add(corpus);
                }
                else hasOnline = true;
                StackPanel item = new StackPanel();
                item.Margin = new Thickness(0, i == 0 ? 8 : 10, 0, 0);

                TextBlock source = Ui.Label(pair.Source, 11.5, Ui.Muted, FontWeights.Normal);
                source.TextWrapping = TextWrapping.Wrap;
                if (ArabicText.ContainsArabic(pair.Source))
                {
                    source.FlowDirection = FlowDirection.RightToLeft;
                    source.TextAlignment = TextAlignment.Right;
                }

                TextBlock target = Ui.Label(pair.Target, 14, Ui.Bright, FontWeights.Normal);
                target.TextWrapping = TextWrapping.Wrap;
                target.Margin = new Thickness(0, 2, 0, 0);
                if (ArabicText.ContainsArabic(pair.Target))
                {
                    target.FlowDirection = FlowDirection.RightToLeft;
                    target.TextAlignment = TextAlignment.Right;
                }

                item.Children.Add(source);
                item.Children.Add(target);
                _examples.Children.Add(item);
            }

            if (hasPhrasebook) credits.Add("Qalam phrasebook");
            if (hasConcordance)
            {
                if (corpora.Count == 0) credits.Add("Open corpus");
                else credits.AddRange(corpora);
            }
            if (hasOnline) credits.Add("Tatoeba (CC BY)  \u00b7  MyMemory");
            _examplesCredit.Text = "Examples: " + ConcordanceText.Join("  \u00b7  ", credits);
            _examplesCredit.TextWrapping = TextWrapping.Wrap;
            _examplesPanel.Visibility = Visibility.Visible;
            Present();
        }

        public void ShowAgain(TranslationResult result)
        {
            List<string> synonyms = new List<string>(_cachedSynonyms);
            List<ExamplePair> examples = new List<ExamplePair>(_cachedExamples);
            GrammarInfo grammar = _cachedGrammar;
            string tashkeel = _cachedTashkeel;
            string role = _cachedTashkeelRole;
            string kind = _cachedTashkeelKind;
            string speech = _cachedTashkeelSpeech;
            string speechLang = _cachedTashkeelSpeechLang;

            ShowResult(result);
            ShowGrammar(grammar);
            ShowSynonyms(synonyms);
            ShowExamples(examples);
            if (!string.IsNullOrEmpty(tashkeel))
            {
                if (kind == "PINYIN") ShowPinyin(tashkeel, speech, role);
                else ShowTashkeel(tashkeel, role);
            }
        }

        public void PrepareForRender(TranslationResult result, string tashkeel, string role)
        {
            Headless = true;
            _bodyScroll.MaxHeight = 1400;
            ShowResult(result);
            if (!string.IsNullOrEmpty(tashkeel)) ShowTashkeel(tashkeel, role);
        }

        public void PrepareExtras(List<string> synonyms, List<ExamplePair> examples)
        {
            ShowSynonyms(synonyms);
            ShowExamples(examples);
        }

        private async void Speak(string text, string language, string what)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(language)) return;
            _status.Text = "Loading " + what.ToLowerInvariant() + " audio\u2026";
            _status.Foreground = Ui.Muted;
            try
            {
                await SpeechService.SpeakAsync(text, language);
                _status.Text = "";
            }
            catch (Exception ex)
            {
                Logger.Write("tts: " + ex.Message);
                _status.Text = "Audio unavailable.";
                _status.Foreground = Ui.WarnBrush;
            }
        }

        private void Copy(string text, string what)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                Clipboard.SetText(text);
                _status.Text = what + " copied to clipboard";
                _status.Foreground = Ui.Muted;
            }
            catch (Exception ex)
            {
                _status.Text = "Could not copy: " + ex.Message;
                _status.Foreground = Ui.ErrorBrush;
            }
        }

        private void Present()
        {
            if (Headless) return;
            _suppressDeactivate = true;
            if (!IsVisible)
            {
                Opacity = 0;
                _wasActive = false;
                Show();
            }
            _shownAt = DateTime.UtcNow;
            Dispatcher.BeginInvoke(new Action(delegate
            {
                UpdateLayout();
                PlaceNearCursor();
                Opacity = 1;
                Activate();
                _suppressDeactivate = false;

                // Examples and tashkeel arrive after the first placement and make
                // the popup taller. Re-place once layout has settled so it does
                // not grow past the bottom of the screen.
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    UpdateLayout();
                    PlaceNearCursor();
                }), DispatcherPriority.ContextIdle);
            }), DispatcherPriority.Loaded);
        }

        private void PlaceNearCursor()
        {
            try
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                if (handle == IntPtr.Zero) return;

                // GetWindowRect is in physical pixels, which is what SetWindowPos
                // and Screen.WorkingArea use. ActualWidth/Height are device-
                // independent units; using them directly clips the popup on a
                // scaled display (for example 125%).
                int width = 0;
                int height = 0;
                NativeMethods.RECT rect;
                if (NativeMethods.GetWindowRect(handle, out rect))
                {
                    width = rect.Right - rect.Left;
                    height = rect.Bottom - rect.Top;
                }
                if (width <= 0 || height <= 0)
                {
                    PresentationSource source = PresentationSource.FromVisual(this);
                    double scaleX = source == null || source.CompositionTarget == null
                        ? 1.0 : source.CompositionTarget.TransformToDevice.M11;
                    double scaleY = source == null || source.CompositionTarget == null
                        ? 1.0 : source.CompositionTarget.TransformToDevice.M22;
                    width = (int)Math.Ceiling(ActualWidth * scaleX);
                    height = (int)Math.Ceiling(ActualHeight * scaleY);
                }
                if (width <= 0 || height <= 0) return;

                System.Drawing.Point cursor = WinForms.Cursor.Position;
                WinForms.Screen screen = WinForms.Screen.FromPoint(cursor);
                System.Drawing.Rectangle area = screen.WorkingArea;

                int x = cursor.X + 18;
                int y = cursor.Y + 22;
                if (x + width > area.Right - 10) x = area.Right - width - 10;
                if (y + height > area.Bottom - 10) y = cursor.Y - height - 18;
                if (x < area.Left + 10) x = area.Left + 10;
                if (y < area.Top + 10) y = area.Top + 10;

                NativeMethods.SetWindowPos(handle, NativeMethods.HWND_TOPMOST, x, y, 0, 0,
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
            }
            catch (Exception ex)
            {
                Logger.Write("place: " + ex.Message);
            }
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            string text = value.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
            while (text.IndexOf("  ", StringComparison.Ordinal) >= 0)
                text = text.Replace("  ", " ");
            return text.Trim();
        }
    }

    internal sealed class AppController : IDisposable
    {
        private readonly Config _config;
        private readonly TranslationService _translator;
        private readonly DocumentTranslationService _documents;
        private readonly ContextService _context;
        private readonly GrammarService _grammar;
        private readonly TashkeelClient _tashkeel;
        private readonly PopupWindow _popup;
        private MobileServer _mobile;
        private CloudflareTunnel _tunnel;
        private string _mobileUrl;
        private readonly KeyboardHook _hook;
        private readonly System.Drawing.Icon _trayIcon;
        private WinForms.NotifyIcon _tray;
        private MainWindow _main;
        private TranslationResult _last;
        private long _lastTriggerTicks;
        private uint _pendingSequence;
        private bool _documentRunning;
        private bool _disposed;
        private DispatcherTimer _updateTimer;

        public AppController(bool background)
        {
            _config = Config.Load();
            ThemeManager.Apply(_config.Theme);
            ThemeManager.SystemThemeChanged += OnSystemThemeChanged;
            ThemeManager.WatchSystem();
            _translator = new TranslationService();
            _translator.ConfigureProvider(_config.LocalModelEnabled, _config.LocalModelEndpoint,
                _config.LocalModelName, _config.TranslationMode);
            _documents = new DocumentTranslationService(_config, _translator);
            _context = new ContextService();
            _grammar = new GrammarService();
            _tashkeel = new TashkeelClient(_config);
            SpeechService.Vocalizer = _tashkeel.Diacritize;
            _popup = new PopupWindow();
            _popup.TargetChanged += OnPopupTargetChanged;
            _popup.CharactersRequested += delegate(string text)
            {
                CharacterWindow.ShowFor(text, delegate
                {
                    if (_last != null) _popup.ShowAgain(_last);
                    else ShowMainWindow();
                });
            };
            _trayIcon = CreateTrayIcon();
            CreateTray();
            if (_config.MobileEnabled) StartMobile(true);
            StartUpdateChecks();

            _hook = new KeyboardHook(_config.DoubleCopyWindowMs);
            _hook.DoubleCopy += OnDoubleCopy;
            _hook.Install();
            Logger.Write("Qalam started; hook=" + _hook.IsInstalled + "; python=" + _tashkeel.PythonPath);

            Task.Run(delegate
            {
                try
                {
                    _tashkeel.Ping();
                    _tashkeel.Diacritize("\u0628\u0633\u0645 \u0627\u0644\u0644\u0647 \u0627\u0644\u0631\u062d\u0645\u0646 \u0627\u0644\u0631\u062d\u064a\u0645");
                    Logger.Write("tashkeel worker ready");
                }
                catch (Exception ex)
                {
                    Logger.Write("tashkeel warmup: " + ex.Message);
                }
            });

            if (!background) ShowMainWindow();
        }

        private void StartUpdateChecks()
        {
            if (!_config.AutoUpdate || string.IsNullOrWhiteSpace(_config.UpdateUrl)) return;

            // Check once after startup, then keep checking while the tray app
            // remains open. This is what makes a release reach users who leave
            // Qalam running instead of restarting it every day.
            Task.Delay(TimeSpan.FromSeconds(12)).ContinueWith(delegate
            {
                if (!_disposed) UpdateService.CheckAndDownload(_config, false);
            });

            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromHours(6);
            _updateTimer.Tick += delegate
            {
                if (!_disposed) UpdateService.CheckAndDownload(_config, false);
            };
            _updateTimer.Start();
            Logger.Write("automatic update checks enabled (startup + every 6 hours)");
        }

        private void OnSystemThemeChanged(object sender, EventArgs e)
        {
            if (!string.Equals(_config.Theme, ThemeManager.SystemTheme, StringComparison.OrdinalIgnoreCase)) return;
            System.Windows.Application app = System.Windows.Application.Current;
            if (app == null) return;
            app.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (string.Equals(_config.Theme, ThemeManager.SystemTheme, StringComparison.OrdinalIgnoreCase))
                    ThemeManager.Apply(_config.Theme);
            }));
        }

        private void OpenBugReport()
        {
            System.Windows.Application app = System.Windows.Application.Current;
            if (app == null) return;
            app.Dispatcher.BeginInvoke(new Action(delegate
            {
                BugReportWindow.ShowReport(_config);
            }));
        }

        public void ShowMainWindow()
        {
            if (_disposed) return;
            if (_main == null)
            {
                _main = new MainWindow(_config, _translator, _tashkeel, _grammar,
                    delegate(TranslationResult result) { _last = result; },
                    TranslateDocument);
            }
            _main.ShowFromTray();
        }

        private void OnDoubleCopy(uint sequence)
        {
            _pendingSequence = sequence;
            System.Windows.Application app = System.Windows.Application.Current;
            if (app == null) return;
            app.Dispatcher.BeginInvoke(new Action(async () => { await HandleDoubleCopyAsync(); }));
        }

        private async void OnPopupTargetChanged(string code)
        {
            try
            {
                if (string.IsNullOrEmpty(code))
                {
                    _config.AutoTarget = true;
                }
                else
                {
                    _config.AutoTarget = false;
                    _config.Target = code;
                }
                _config.Save();
                if (_main != null) _main.RefreshTarget();
                Logger.Write("popup target=" + (string.IsNullOrEmpty(code) ? "auto" : code));
                if (_last != null) await AnalyzeAndShowAsync(_last.SourceText, true);
            }
            catch (Exception ex)
            {
                Logger.Write("popup target: " + ex.Message);
            }
        }

        private async Task HandleDoubleCopyAsync()
        {
            try
            {
                long now = DateTime.UtcNow.Ticks;
                if (now - _lastTriggerTicks < TimeSpan.TicksPerMillisecond * 800) return;
                _lastTriggerTicks = now;

                string processName = ForegroundProcess();
                if (IsExcluded(processName))
                {
                    Logger.Write("skip excluded: " + processName);
                    return;
                }

                string selected = await SelectionReader.TryReadAsync(400).ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    Logger.Write("uia selection chars=" + selected.Length);
                    Logger.Write("trigger accepted: " + processName + " (uia)");
                    _popup.ShowLoading(selected);
                    await AnalyzeAndShowAsync(selected, false);
                    return;
                }

                uint sequence = _pendingSequence;
                long start = DateTime.UtcNow.Ticks;
                while (DateTime.UtcNow.Ticks - start < TimeSpan.TicksPerMillisecond * 500)
                {
                    await Task.Delay(35);
                    if (NativeMethods.GetClipboardSequenceNumber() != sequence) break;
                }
                uint finalSequence = NativeMethods.GetClipboardSequenceNumber();
                if (finalSequence == sequence)
                {
                    Logger.Write("skip: clipboard unchanged (" + processName + ")");
                    return;
                }

                string text = ReadClipboard();
                if (string.IsNullOrWhiteSpace(text))
                {
                    Logger.Write("skip: empty clipboard");
                    return;
                }

                Logger.Write("trigger accepted: " + processName + " (clipboard)");
                _popup.ShowLoading(text);
                await AnalyzeAndShowAsync(text, false);
            }
            catch (Exception ex)
            {
                Logger.Write("trigger: " + ex);
            }
        }

        private async Task ShowGrammarAsync(Task<GrammarInfo> task, string text)
        {
            GrammarInfo grammar = null;
            if (task != null)
            {
                try { grammar = await task; }
                catch (Exception ex)
                {
                    Logger.Write("grammar: " + ex.Message);
                    return;
                }
            }
            else if (IsSingleWord(text))
            {
                grammar = _grammar.LookupCached(text);
            }

            // A language-keyed miss can still exist under another cached
            // language for the same spelling (for example an English/Spanish
            // homograph). Fall back to the word-only lookup.
            if (grammar == null && IsSingleWord(text))
                grammar = _grammar.LookupCached(text);

            if (grammar == null)
            {
                Logger.Write("grammar=none");
                return;
            }

            Logger.Write("grammar=" + (grammar.HasContent
                ? (grammar.FromCache ? "cached" : "ok")
                : "none"));
            _popup.ShowGrammar(grammar);
        }

        private async Task AnalyzeAndShowAsync(string text, bool showLoading)
        {
            if (showLoading) _popup.ShowLoading(text);

            TranslationResult result = new TranslationResult();
            TranslationOutcome outcome = null;
            bool sourceArabic = ArabicText.ContainsArabic(text);
            string target = _config.AutoTarget ? (sourceArabic ? "en" : "ar") : _config.Target;

            result.MaxChars = _config.MaxChars;
            if (text.Length > _config.MaxChars)
            {
                text = text.Substring(0, _config.MaxChars);
                result.Truncated = true;
            }
            result.SourceText = text;
            result.Target = target;
            result.AutoTarget = _config.AutoTarget;
            result.Detected = sourceArabic ? "ar" : null;

            if (sourceArabic && target == "ar")
            {
                result.TashkeelOnly = true;
                result.Translation = text;
                result.Detected = "ar";
            }
            else
            {
                try
                {
                    outcome = await _translator.TranslateAsync(text, target);
                    result.Translation = outcome.Text;
                    result.Detected = outcome.Detected;
                    result.Engine = outcome.Engine;
                }
                catch (Exception ex)
                {
                    result.Error = FriendlyError(ex);
                    Logger.Write("translate: " + ex);
                }
            }

            _popup.ShowResult(result);
            _last = result;
            Logger.Write("result " + (result.Detected == null ? "?" : result.Detected) + "->" + result.Target +
                         (result.Error == null ? " ok" : " error"));

            bool offlineMode = string.Equals(_config.TranslationMode, "offline", StringComparison.OrdinalIgnoreCase);
            Task<GrammarInfo> grammarTask = null;
            if (IsSingleWord(text) && !string.IsNullOrEmpty(result.Detected) && !result.TashkeelOnly)
                grammarTask = _grammar.LookupAsync(text, result.Detected, !offlineMode);

            if (result.Error != null)
            {
                // The cache can still answer a single-word lookup when the
                // translation path failed, for example offline with no model.
                if (grammarTask != null || IsSingleWord(text))
                    await ShowGrammarAsync(grammarTask, text);
                return;
            }

            HistoryStore.Add(ToHistoryEntry(result));

            Task<List<string>> synonymsTask = null;
            Task<List<ExamplePair>> examplesTask = null;
            List<ExamplePair> immediateExamples = null;
            bool shortQuery = IsShortQuery(text);
            bool contextQuery = IsContextQuery(text);
            List<string> immediateSynonyms = shortQuery && outcome != null && outcome.Synonyms.Count > 0
                ? outcome.Synonyms : null;
            if (!offlineMode && shortQuery && immediateSynonyms == null)
                synonymsTask = _translator.GetSynonymsAsync(text, target);
            if (contextQuery && !string.IsNullOrEmpty(result.Detected) &&
                !string.Equals(result.Detected, target, StringComparison.OrdinalIgnoreCase))
            {
                if (offlineMode)
                {
                    immediateExamples = _context.GetLocalExamples(text, result.Detected, target,
                                                                  result.Translation, 4);
                }
                else
                {
                    examplesTask = _context.GetExamplesAsync(text, result.Detected, target, result.Translation);
                }
            }

            string transcription = null;
            string kind = null;
            string role = null;
            string speech = null;
            if (sourceArabic && (result.Detected == "ar" || result.Detected == null))
            {
                transcription = text;
                kind = "TASHKEEL";
                role = "source";
                speech = text;
            }
            else if (!string.IsNullOrEmpty(result.Translation) && ArabicText.ContainsArabic(result.Translation))
            {
                transcription = result.Translation;
                kind = "TASHKEEL";
                role = "translation";
                speech = result.Translation;
            }
            else if (ChineseText.ContainsHan(text) &&
                     (ChineseText.IsChineseLanguage(result.Detected) || result.Detected == null))
            {
                transcription = text;
                kind = "PINYIN";
                role = "source";
                speech = text;
            }
            else if (!string.IsNullOrEmpty(result.Translation) && ChineseText.ContainsHan(result.Translation))
            {
                transcription = result.Translation;
                kind = "PINYIN";
                role = "translation";
                speech = result.Translation;
            }

            if (kind == "PINYIN")
            {
                try
                {
                    string pinyin = await Task.Run(() => _tashkeel.Pinyin(transcription));
                    Logger.Write("pinyin ok: " + role + " chars=" + pinyin.Length);
                    _popup.ShowPinyin(pinyin, speech, role);
                }
                catch (Exception ex)
                {
                    Logger.Write("pinyin: " + ex.Message);
                    _popup.ShowTranscriptionUnavailable("Pinyin");
                }
            }
            else if (kind == "TASHKEEL")
            {
                try
                {
                    string vocalized = await Task.Run(() => _tashkeel.Diacritize(transcription, _config.SpokenLahja));
                    Logger.Write("tashkeel ok: " + role + " chars=" + vocalized.Length);
                    _popup.ShowTashkeel(vocalized, role);
                }
                catch (Exception ex)
                {
                    Logger.Write("tashkeel: " + ex.Message);
                    _popup.ShowTranscriptionUnavailable("Tashkeel");
                }
            }

            if (grammarTask != null || offlineMode)
                await ShowGrammarAsync(grammarTask, text);

            if (immediateSynonyms != null)
            {
                Logger.Write("synonyms=" + immediateSynonyms.Count + " (with translation)");
                _popup.ShowSynonyms(immediateSynonyms);
            }
            else if (synonymsTask != null)
            {
                try
                {
                    List<string> synonyms = await synonymsTask;
                    Logger.Write("synonyms=" + synonyms.Count);
                    _popup.ShowSynonyms(synonyms);
                }
                catch (Exception ex)
                {
                    Logger.Write("synonyms: " + ex.Message);
                }
            }

            if (immediateExamples != null)
            {
                Logger.Write("examples=" + immediateExamples.Count + " (offline phrasebook+concordance)");
                _popup.ShowExamples(immediateExamples);
            }
            else if (examplesTask != null)
            {
                try
                {
                    List<ExamplePair> examples = await examplesTask;
                    Logger.Write("examples=" + examples.Count);
                    _popup.ShowExamples(examples);
                }
                catch (Exception ex)
                {
                    Logger.Write("examples: " + ex.Message);
                }
            }
        }

        private static bool IsSingleWord(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 2 || text.Length > 40) return false;
            bool hasLetter = false;
            foreach (char c in text)
            {
                if (char.IsLetter(c)) { hasLetter = true; continue; }
                if (c == '-' || c == '\'' || c == '\u2019' || ArabicText.IsMark(c)) continue;
                return false;
            }
            return hasLetter;
        }

        private static bool IsShortQuery(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 60) return false;
            return text.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length <= 4;
        }

        private static bool IsContextQuery(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 240) return false;
            return text.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length <= 24;
        }

        private async void TranslateDocument()
        {
            if (_documentRunning)
            {
                System.Windows.MessageBox.Show("A document is already being translated.", "Qalam",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _documentRunning = true;
            string output = null;
            bool outputExisted = false;
            try
            {
                string input = null;
                using (WinForms.OpenFileDialog open = new WinForms.OpenFileDialog())
                {
                    open.Title = "Translate a document";
                    open.Filter = "Documents (*.docx;*.pdf)|*.docx;*.pdf|Word document (*.docx)|*.docx|PDF (*.pdf)|*.pdf";
                    open.CheckFileExists = true;
                    open.Multiselect = false;
                    open.RestoreDirectory = true;
                    open.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    if (open.ShowDialog() != WinForms.DialogResult.OK) return;
                    input = open.FileName;
                }

                if (!DocumentTranslationService.Supports(input))
                {
                    ShowDocumentError("Qalam can translate .docx and .pdf documents only.");
                    return;
                }

                using (CancellationTokenSource cancel = new CancellationTokenSource())
                {
                    string source;
                    try
                    {
                        source = await _documents.DetectSourceAsync(input, cancel.Token);
                    }
                    catch (Exception ex)
                    {
                        Logger.Write("document detect: " + ex);
                        ShowDocumentError(ex.Message);
                        return;
                    }

                    string target = _config.AutoTarget
                        ? (string.Equals(source, "ar", StringComparison.OrdinalIgnoreCase) ? "en" : "ar")
                        : _config.Target;
                    string extension = Path.GetExtension(input).ToLowerInvariant();
                    string suggested = Path.Combine(
                        Path.GetDirectoryName(input),
                        Path.GetFileNameWithoutExtension(input) + "." + target + extension);

                    using (WinForms.SaveFileDialog save = new WinForms.SaveFileDialog())
                    {
                        save.Title = "Save translated document";
                        save.Filter = extension == ".docx"
                            ? "Word document (*.docx)|*.docx"
                            : "PDF (*.pdf)|*.pdf";
                        save.DefaultExt = extension.TrimStart('.');
                        save.AddExtension = true;
                        save.OverwritePrompt = true;
                        save.RestoreDirectory = true;
                        save.FileName = Path.GetFileName(suggested);
                        save.InitialDirectory = Path.GetDirectoryName(input);
                        if (save.ShowDialog() != WinForms.DialogResult.OK) return;
                        output = save.FileName;
                    }

                    if (string.Equals(Path.GetFullPath(input), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
                    {
                        ShowDocumentError("Choose a different output file. Qalam never overwrites the original document.");
                        return;
                    }

                    outputExisted = File.Exists(output);
                    DocumentProgressWindow progressWindow = new DocumentProgressWindow(
                        Path.GetFileName(input), DocumentTranslationService.LanguageName(target));
                    progressWindow.CancelRequested += delegate { cancel.Cancel(); };
                    progressWindow.Show();

                    try
                    {
                        Progress<DocumentProgress> progress = new Progress<DocumentProgress>(progressWindow.UpdateProgress);
                        DocumentTranslationReport report = await Task.Run(async () => await _documents.TranslateAsync(
                            input, output, target, source, progress, cancel.Token).ConfigureAwait(false));

                        progressWindow.AllowClose();
                        progressWindow.Close();
                        ShowDocumentReport(report);
                    }
                    catch (OperationCanceledException)
                    {
                        progressWindow.AllowClose();
                        progressWindow.Close();
                        TryDelete(output + ".part");
                        if (!outputExisted) TryDelete(output);
                        Logger.Write("document cancelled: " + Path.GetFileName(input));
                    }
                    catch (Exception ex)
                    {
                        progressWindow.AllowClose();
                        progressWindow.Close();
                        TryDelete(output + ".part");
                        if (!outputExisted) TryDelete(output);
                        Logger.Write("document: " + ex);
                        ShowDocumentError(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write("document outer: " + ex);
                ShowDocumentError(ex.Message);
            }
            finally
            {
                _documentRunning = false;
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void ShowDocumentError(string message)
        {
            System.Windows.MessageBox.Show(message, "Qalam", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private static void ShowDocumentReport(DocumentTranslationReport report)
        {
            StringBuilder message = new StringBuilder();
            message.Append("Translated ");
            message.Append(report.Translated);
            message.Append(" of ");
            message.Append(report.Units);
            message.Append(' ');
            message.Append(report.UnitLabel);
            message.Append(" to ");
            message.Append(DocumentTranslationService.LanguageName(report.Target));
            message.Append('.');
            if (report.Skipped > 0)
            {
                message.Append(Environment.NewLine);
                message.Append(report.Skipped);
                message.Append(" passage(s) were left unchanged; see the Qalam log for details.");
            }
            if (report.Warnings != null && report.Warnings.Count > 0)
            {
                message.Append(Environment.NewLine);
                message.Append(report.Warnings.Count);
                message.Append(" layout warning(s).");
                for (int i = 0; i < report.Warnings.Count && i < 3; i++)
                {
                    message.Append(Environment.NewLine);
                    message.Append("\u2022 ");
                    message.Append(report.Warnings[i]);
                }
                if (report.Warnings.Count > 3)
                {
                    message.Append(Environment.NewLine);
                    message.Append("\u2022 \u2026and ");
                    message.Append(report.Warnings.Count - 3);
                    message.Append(" more in the log.");
                }
            }
            message.Append(Environment.NewLine);
            message.Append(Environment.NewLine);
            message.Append("Saved to:");
            message.Append(Environment.NewLine);
            message.Append(report.Output);
            message.Append(Environment.NewLine);
            message.Append(Environment.NewLine);
            message.Append("Open the translated document now?");

            MessageBoxResult answer = System.Windows.MessageBox.Show(message.ToString(), "Qalam",
                MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (answer == MessageBoxResult.Yes)
            {
                try { Process.Start(new ProcessStartInfo { FileName = report.Output, UseShellExecute = true }); }
                catch (Exception ex) { Logger.Write("document open: " + ex.Message); }
            }
        }

        private static HistoryEntry ToHistoryEntry(TranslationResult result)
        {
            HistoryEntry entry = new HistoryEntry();
            entry.Source = result.SourceText;
            entry.Translation = result.Translation;
            entry.Detected = result.Detected;
            entry.Target = result.Target;
            entry.Time = DateTime.Now;
            return entry;
        }

        private void CreateTray()
        {
            _tray = new WinForms.NotifyIcon();
            _tray.Icon = _trayIcon;
            _tray.Text = "Qalam \u2014 Highlight, then Ctrl + C + C to translate + Tashkeel";
            _tray.Visible = true;

            WinForms.ContextMenuStrip menu = new WinForms.ContextMenuStrip();

            WinForms.ToolStripMenuItem open = new WinForms.ToolStripMenuItem("Open Qalam");
            open.Click += delegate { ShowMainWindow(); };
            menu.Items.Add(open);

            WinForms.ToolStripMenuItem show = new WinForms.ToolStripMenuItem("Show last result");
            show.Click += delegate { if (_last != null) _popup.ShowAgain(_last); };
            menu.Items.Add(show);

            WinForms.ToolStripMenuItem pronunciation = new WinForms.ToolStripMenuItem("Pronunciation");
            foreach (string code in Pronunciation.Codes)
            {
                string lahja = code;
                WinForms.ToolStripMenuItem entry = new WinForms.ToolStripMenuItem(Pronunciation.Name(lahja));
                entry.Checked = Pronunciation.Normalize(_config.SpokenLahja) == lahja;
                entry.Click += delegate
                {
                    _config.SpokenLahja = lahja;
                    Pronunciation.Current = lahja;
                    _config.Save();
                    foreach (WinForms.ToolStripItem item in pronunciation.DropDownItems)
                    {
                        WinForms.ToolStripMenuItem row = item as WinForms.ToolStripMenuItem;
                        if (row != null) row.Checked = Pronunciation.Normalize(_config.SpokenLahja) == lahja;
                    }
                    Logger.Write("pronunciation register: " + lahja);
                };
                pronunciation.DropDownItems.Add(entry);
            }
            menu.Items.Add(pronunciation);

            WinForms.ToolStripMenuItem characters = new WinForms.ToolStripMenuItem("Chinese Characters\u2026");
            characters.Click += delegate
            {
                string text = "";
                if (_last != null && ChineseText.ContainsHan(_last.SourceText)) text = _last.SourceText;
                else if (_last != null && ChineseText.ContainsHan(_last.Translation)) text = _last.Translation;
                CharacterWindow.ShowFor(text, delegate { ShowMainWindow(); });
            };
            menu.Items.Add(characters);

            WinForms.ToolStripMenuItem document = new WinForms.ToolStripMenuItem("Translate document\u2026");
            document.Click += delegate { TranslateDocument(); };
            menu.Items.Add(document);
            menu.Items.Add(new WinForms.ToolStripSeparator());

            WinForms.ToolStripMenuItem saveFavorite = new WinForms.ToolStripMenuItem("Save last result to favorites");
            saveFavorite.Click += delegate
            {
                if (_last == null) return;
                FavoritesStore.Save(ToHistoryEntry(_last));
                Logger.Write("favorite saved");
            };
            menu.Items.Add(saveFavorite);

            WinForms.ToolStripMenuItem studyLast = new WinForms.ToolStripMenuItem("Study last result");
            studyLast.Click += delegate
            {
                if (_last == null) return;
                StudyStore.Add(ToHistoryEntry(_last));
                StudyWindow.ShowWindow();
            };
            menu.Items.Add(studyLast);

            WinForms.ToolStripMenuItem study = new WinForms.ToolStripMenuItem("Study\u2026");
            study.Click += delegate { StudyWindow.ShowWindow(); };
            menu.Items.Add(study);

            WinForms.ToolStripMenuItem mobile = new WinForms.ToolStripMenuItem("Mobile & web app");
            mobile.Click += delegate { ToggleMobile(); };
            menu.Items.Add(mobile);
            WinForms.ToolStripMenuItem publicLink = new WinForms.ToolStripMenuItem("Copy public HTTPS link (Cloudflare)");
            publicLink.Click += delegate { TogglePublicLink(); };
            menu.Items.Add(publicLink);
            WinForms.ToolStripMenuItem installUpdate = new WinForms.ToolStripMenuItem("Restart to update…");
            installUpdate.Visible = false;
            installUpdate.Click += delegate { UpdateService.InstallPending(true); };

            menu.Opened += delegate
            {
                mobile.Checked = _mobile != null;
                publicLink.Checked = _tunnel != null;
                installUpdate.Visible = UpdateService.HasPendingUpdate;
                if (UpdateService.HasPendingUpdate)
                    installUpdate.Text = "Restart to update to " + UpdateService.PendingVersionText + "\u2026";
            };

            menu.Items.Add(BuildFavoritesMenu());
            menu.Items.Add(BuildHistoryMenu());
            menu.Items.Add(BuildTargetMenu());
            menu.Items.Add(BuildProviderMenu());
            menu.Items.Add(new WinForms.ToolStripSeparator());

            WinForms.ToolStripMenuItem updates = new WinForms.ToolStripMenuItem("Check for updates\u2026");
            updates.Click += delegate { UpdateService.CheckAndDownload(_config, true); };
            menu.Items.Add(updates);
            menu.Items.Add(installUpdate);


            WinForms.ToolStripMenuItem config = new WinForms.ToolStripMenuItem("Open config");
            config.Click += delegate
            {
                try { Process.Start("notepad.exe", Paths.ConfigFile); }
                catch { }
            };
            menu.Items.Add(config);

            WinForms.ToolStripMenuItem log = new WinForms.ToolStripMenuItem("Open log");
            log.Click += delegate
            {
                try { Process.Start("notepad.exe", Logger.LogPath); }
                catch { }
            };
            menu.Items.Add(log);

            WinForms.ToolStripMenuItem report = new WinForms.ToolStripMenuItem("Report a bug\u2026");
            report.Click += delegate { OpenBugReport(); };
            menu.Items.Add(report);

            menu.Items.Add(new WinForms.ToolStripSeparator());
            WinForms.ToolStripMenuItem quit = new WinForms.ToolStripMenuItem("Quit");
            quit.Click += delegate
            {
                if (_main != null) _main.AllowClose();
                System.Windows.Application app = System.Windows.Application.Current;
                if (app != null) app.Shutdown();
            };
            menu.Items.Add(quit);

            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { ShowMainWindow(); };
        }

        private void ToggleMobile()
        {
            if (_mobile != null)
            {
                StopMobile();
                _config.MobileEnabled = false;
                _config.Save();
                return;
            }
            StartMobile(false);
        }

        private bool StartMobile(bool silent)
        {
            if (_mobile != null) return true;
            string token = _config.MobileToken;
            if (string.IsNullOrEmpty(token)) token = MobileServer.NewToken();

            int basePort = _config.MobilePort;
            if (basePort < 1024 || basePort > 65535) basePort = 8787;
            Exception last = null;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    MobileServer server = new MobileServer(_translator, basePort + attempt, token);
                    server.Start(IPAddress.Any);
                    _mobile = server;
                    _mobileUrl = server.UrlFor(MobileServer.LocalAddress());
                    _config.MobileToken = token;
                    _config.MobilePort = server.Port;
                    _config.MobileEnabled = true;
                    _config.Save();
                    Logger.Write("mobile access on port=" + server.Port);
                    if (!silent)
                    {
                        try { Clipboard.SetText(_mobileUrl); }
                        catch { }
                        System.Windows.MessageBox.Show(
                            "Qalam is available to browsers on this network.\n\n" +
                            _mobileUrl + "\n\n" +
                            "The link contains a local access token and is copied to the clipboard. " +
                            "Allow Qalam through Windows Firewall if the phone cannot connect. " +
                            "Use the same tray item to turn it off.",
                            "Qalam mobile & web", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }

            Logger.Write("mobile start failed: " + (last == null ? "unknown" : last.Message));
            _config.MobileEnabled = false;
            _config.Save();
            if (!silent)
                System.Windows.MessageBox.Show("Could not start the local web server.\n\n" +
                    (last == null ? "Unknown error." : last.Message), "Qalam mobile & web",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private void StopMobile()
        {
            StopPublicLink();
            if (_mobile == null) return;
            try { _mobile.Dispose(); }
            catch { }
            _mobile = null;
            _mobileUrl = null;
            Logger.Write("mobile access off");
        }

        private async void TogglePublicLink()
        {
            if (_tunnel != null)
            {
                StopPublicLink();
                return;
            }
            if (_mobile == null && !StartMobile(true)) return;
            MobileServer server = _mobile;
            if (server == null) return;

            CloudflareTunnel tunnel = new CloudflareTunnel();
            try
            {
                string publicUrl = await Task.Run(delegate
                {
                    tunnel.Start(server.Port);
                    return tunnel.WaitForUrl(TimeSpan.FromSeconds(35));
                });
                _tunnel = tunnel;
                string link = publicUrl + "/?token=" + server.Token;
                try { Clipboard.SetText(link); }
                catch { }
                Logger.Write("public tunnel on");
                System.Windows.MessageBox.Show(
                    "Qalam is available through a temporary Cloudflare HTTPS link.\n\n" +
                    link + "\n\n" +
                    "The link contains the local access token and is copied to the clipboard. " +
                    "Anyone with the link can use Qalam until you turn the public link off. " +
                    "Choose the same tray item to stop it.",
                    "Qalam public link", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                try { tunnel.Dispose(); }
                catch { }
                Logger.Write("public tunnel: " + ex.Message);
                System.Windows.MessageBox.Show(
                    "Could not create the public HTTPS link.\n\n" + ex.Message +
                    "\n\ncloudflared must be installed and able to reach Cloudflare.",
                    "Qalam public link", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void StopPublicLink()
        {
            if (_tunnel == null) return;
            try { _tunnel.Dispose(); }
            catch { }
            _tunnel = null;
            Logger.Write("public tunnel off");
        }

        private WinForms.ToolStripMenuItem BuildFavoritesMenu()
        {
            WinForms.ToolStripMenuItem root = new WinForms.ToolStripMenuItem("Favorites");
            root.DropDownOpening += delegate { PopulateStoredMenu(root, true); };
            PopulateStoredMenu(root, true);
            return root;
        }

        private WinForms.ToolStripMenuItem BuildHistoryMenu()
        {
            WinForms.ToolStripMenuItem root = new WinForms.ToolStripMenuItem("History");
            root.DropDownOpening += delegate { PopulateStoredMenu(root, false); };
            PopulateStoredMenu(root, false);
            return root;
        }

        private void PopulateStoredMenu(WinForms.ToolStripMenuItem root, bool favorites)
        {
            root.DropDownItems.Clear();
            List<HistoryEntry> entries = favorites ? FavoritesStore.Load() : HistoryStore.Load();
            if (entries.Count == 0)
            {
                WinForms.ToolStripMenuItem empty = new WinForms.ToolStripMenuItem(favorites ? "No favorites yet" : "No history yet");
                empty.Enabled = false;
                root.DropDownItems.Add(empty);
                return;
            }

            int limit = favorites ? 15 : 12;
            foreach (HistoryEntry entry in entries.Take(limit))
            {
                HistoryEntry captured = entry;
                string pair = (captured.Detected == null ? "?" : captured.Detected.ToUpperInvariant()) + " \u2192 " +
                              (captured.Target == null ? "?" : captured.Target.ToUpperInvariant());
                string label = pair + "  " + MenuLabel(captured.Source, 52);
                WinForms.ToolStripMenuItem item = new WinForms.ToolStripMenuItem(label);
                item.Click += delegate { ShowStoredEntry(captured, favorites ? "favorite" : "history"); };
                root.DropDownItems.Add(item);
            }

            root.DropDownItems.Add(new WinForms.ToolStripSeparator());
            WinForms.ToolStripMenuItem clear = new WinForms.ToolStripMenuItem(favorites ? "Clear favorites" : "Clear history");
            clear.Click += delegate
            {
                if (favorites) FavoritesStore.Clear();
                else HistoryStore.Clear();
            };
            root.DropDownItems.Add(clear);
        }

        private void ShowStoredEntry(HistoryEntry entry, string kind)
        {
            TranslationResult result = new TranslationResult();
            result.SourceText = entry.Source;
            result.Translation = entry.Translation;
            result.Detected = entry.Detected;
            result.Target = entry.Target;
            result.MaxChars = _config.MaxChars;
            _popup.ShowResult(result);
            _last = result;
            Logger.Write(kind + " show " + entry.Detected + "->" + entry.Target);
        }

        private static string MenuLabel(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string clean = text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
            if (clean.Length <= max) return clean;
            return clean.Substring(0, Math.Max(0, max - 1)) + "\u2026";
        }

        private WinForms.ToolStripMenuItem BuildProviderMenu()
        {
            WinForms.ToolStripMenuItem root = new WinForms.ToolStripMenuItem("Translation source");
            string[,] modes = new string[,]
            {
                { "auto", "Auto (local slang + online)" },
                { "online", "Online only" },
                { "offline", "Offline only (Ollama)" }
            };
            for (int i = 0; i < modes.GetLength(0); i++)
            {
                string mode = modes[i, 0];
                WinForms.ToolStripMenuItem item = new WinForms.ToolStripMenuItem(modes[i, 1]);
                item.Tag = mode;
                item.Click += delegate { SetTranslationMode(mode); };
                root.DropDownItems.Add(item);
            }
            root.DropDownOpening += delegate
            {
                foreach (WinForms.ToolStripItem item in root.DropDownItems)
                {
                    WinForms.ToolStripMenuItem entry = item as WinForms.ToolStripMenuItem;
                    if (entry == null) continue;
                    entry.Checked = string.Equals((string)entry.Tag, _config.TranslationMode,
                        StringComparison.OrdinalIgnoreCase);
                }
            };
            return root;
        }

        private async void SetTranslationMode(string mode)
        {
            try
            {
                _config.TranslationMode = TranslationService.NormalizeMode(mode);
                _config.Save();
                _translator.ConfigureProvider(_config.LocalModelEnabled, _config.LocalModelEndpoint,
                    _config.LocalModelName, _config.TranslationMode);
                if (_main != null) _main.RefreshTarget();
                Logger.Write("translation mode=" + _config.TranslationMode);
                if (_last != null) await AnalyzeAndShowAsync(_last.SourceText, true);
            }
            catch (Exception ex) { Logger.Write("set translation mode: " + ex.Message); }
        }

        private WinForms.ToolStripMenuItem BuildTargetMenu()
        {
            WinForms.ToolStripMenuItem root = new WinForms.ToolStripMenuItem("Translate to");

            WinForms.ToolStripMenuItem auto = new WinForms.ToolStripMenuItem("Auto (Arabic \u21c4 English)");
            auto.Tag = "";
            auto.Click += delegate { SetAutoTarget(); };
            root.DropDownItems.Add(auto);
            root.DropDownItems.Add(new WinForms.ToolStripSeparator());

            for (int i = 0; i < LanguageCatalog.Count; i++)
            {
                string code = LanguageCatalog.Code(i);
                string name = LanguageCatalog.Name(i);
                WinForms.ToolStripMenuItem item = new WinForms.ToolStripMenuItem(name + "  (" + code + ")");
                item.Tag = code;
                item.Click += delegate { SetTarget(code); };
                root.DropDownItems.Add(item);
            }

            root.DropDownOpening += delegate { UpdateTargetChecks(root); };
            return root;
        }

        private void UpdateTargetChecks(WinForms.ToolStripMenuItem root)
        {
            foreach (WinForms.ToolStripItem item in root.DropDownItems)
            {
                WinForms.ToolStripMenuItem entry = item as WinForms.ToolStripMenuItem;
                if (entry == null || entry.Tag == null) continue;
                string code = (string)entry.Tag;
                entry.Checked = code.Length == 0 ? _config.AutoTarget : (!_config.AutoTarget && _config.Target == code);
            }
        }

        private async void SetAutoTarget()
        {
            try
            {
                _config.AutoTarget = true;
                _config.Save();
                if (_last != null) await AnalyzeAndShowAsync(_last.SourceText, true);
            }
            catch (Exception ex) { Logger.Write("set auto target: " + ex.Message); }
        }

        private async void SetTarget(string code)
        {
            try
            {
                _config.AutoTarget = false;
                _config.Target = code;
                _config.Save();
                if (_last != null) await AnalyzeAndShowAsync(_last.SourceText, true);
            }
            catch (Exception ex) { Logger.Write("set target: " + ex.Message); }
        }

        private bool IsExcluded(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            return _config.ExcludedProcesses.Any(p => string.Equals(p, processName, StringComparison.OrdinalIgnoreCase));
        }

        private static string ForegroundProcess()
        {
            try
            {
                IntPtr handle = NativeMethods.GetForegroundWindow();
                uint pid;
                NativeMethods.GetWindowThreadProcessId(handle, out pid);
                if (pid == 0) return "";
                using (Process process = Process.GetProcessById((int)pid))
                    return process.ProcessName.ToLowerInvariant();
            }
            catch
            {
                return "";
            }
        }

        private static string ReadClipboard()
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    if (Clipboard.ContainsText()) return Clipboard.GetText();
                    return "";
                }
                catch (Exception)
                {
                    Thread.Sleep(40);
                }
            }
            return "";
        }

        private static string FriendlyError(Exception ex)
        {
            if (ex is HttpRequestException)
                return "Translation service unreachable. Check your connection, then try again.";
            if (ex is TaskCanceledException)
                return "Translation service timed out.";
            string message = ex.Message ?? "";
            if (message.IndexOf("All translation endpoints failed", StringComparison.Ordinal) >= 0)
                return "Translation services are unavailable right now. Try again shortly.";
            if (message.IndexOf("429", StringComparison.Ordinal) >= 0)
                return "Translation service is rate-limiting. Try again in a moment.";
            return "Translation failed: " + message;
        }

        private static System.Drawing.Icon CreateTrayIcon()
        {
            // Prefer the icon embedded in the executable so the tray, Explorer,
            // Start Menu, and taskbar all show the same artwork at every DPI.
            try
            {
                string executable = System.Reflection.Assembly.GetExecutingAssembly().Location;
                using (Drawing.Icon extracted = Drawing.Icon.ExtractAssociatedIcon(executable))
                {
                    if (extracted != null)
                        return new Drawing.Icon(extracted, WinForms.SystemInformation.SmallIconSize);
                }
            }
            catch (Exception ex)
            {
                Logger.Write("tray icon: " + ex.Message);
            }

            // Fallback: draw the Qaaf mark if the resource cannot be read.
            using (Drawing.Bitmap bitmap = new Drawing.Bitmap(32, 32))
            {
                using (Drawing.Graphics graphics = Drawing.Graphics.FromImage(bitmap))
                {
                    graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    graphics.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    using (Drawing.SolidBrush background = new Drawing.SolidBrush(Drawing.Color.FromArgb(255, 18, 22, 30)))
                        graphics.FillEllipse(background, 1, 1, 30, 30);
                    using (Drawing.SolidBrush foreground = new Drawing.SolidBrush(Drawing.Color.FromArgb(255, 232, 200, 120)))
                    using (Drawing.Font font = new Drawing.Font("Tahoma", 18f, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel))
                    using (Drawing.StringFormat format = new Drawing.StringFormat())
                    {
                        format.Alignment = Drawing.StringAlignment.Center;
                        format.LineAlignment = Drawing.StringAlignment.Center;
                        graphics.DrawString("\u0642", font, foreground, new Drawing.RectangleF(0, 0, 32, 32), format);
                    }
                }

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Drawing.Icon temporary = Drawing.Icon.FromHandle(handle))
                        return (Drawing.Icon)temporary.Clone();
                }
                finally
                {
                    NativeMethods.DestroyIcon(handle);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_updateTimer != null)
                {
                    _updateTimer.Stop();
                    _updateTimer = null;
                }
            }
            catch { }
            ThemeManager.SystemThemeChanged -= OnSystemThemeChanged;
            ThemeManager.UnwatchSystem();
            try { _hook.Dispose(); }
            catch { }
            try
            {
                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                }
            }
            catch { }
            StopMobile();
            try { _tashkeel.Dispose(); }
            catch { }
            try
            {
                if (_main != null)
                {
                    _main.AllowClose();
                    _main.Close();
                    _main = null;
                }
            }
            catch { }
            try { _popup.Close(); }
            catch { }
        }
    }

    internal static class SelfTest
    {
        public static int Run(string[] args)
        {
            string output = Arg(args, "--out");
            if (string.IsNullOrEmpty(output)) output = Path.Combine(Paths.AppDir, "selftest");
            Directory.CreateDirectory(output);

            StringBuilder log = new StringBuilder();
            int exitCode = 0;
            try
            {
                try { Console.OutputEncoding = Encoding.UTF8; }
                catch { }
                new System.Windows.Application();

                log.AppendLine("appdir=" + Paths.AppDir);
                log.AppendLine("script=" + Paths.ScriptPath + " exists=" + File.Exists(Paths.ScriptPath));
                log.AppendLine("python=" + Paths.ResolvePython("") + " exists=" + File.Exists(Paths.ResolvePython("")));

                Config config = new Config();
                TashkeelClient tashkeel = new TashkeelClient(config);
                string arabic = "\u0643\u062a\u0628 \u0627\u0644\u0637\u0627\u0644\u0628 \u0627\u0644\u062f\u0631\u0633 \u0641\u064a \u0627\u0644\u0645\u062f\u0631\u0633\u0629 \u0635\u0628\u0627\u062d \u0627\u0644\u064a\u0648\u0645";
                Stopwatch watch = Stopwatch.StartNew();
                string vocalized = tashkeel.Diacritize(arabic);
                watch.Stop();
                log.AppendLine("tashkeel_ms=" + watch.ElapsedMilliseconds);
                log.AppendLine("tashkeel=" + vocalized);

                string translatedArabic = "\u0643\u062a\u0628 \u0627\u0644\u0637\u0627\u0644\u0628 \u0627\u0644\u062f\u0631\u0633 \u0641\u064a \u0627\u0644\u0645\u062f\u0631\u0633\u0629 \u0647\u0630\u0627 \u0627\u0644\u0635\u0628\u0627\u062d.";
                try
                {
                    TranslationService translator = new TranslationService();
                    TranslationOutcome outcome = translator
                        .TranslateAsync("The student wrote the lesson at school this morning.", "ar")
                        .GetAwaiter().GetResult();
                    log.AppendLine("detected=" + outcome.Detected);
                    log.AppendLine("translation=" + outcome.Text);
                    translatedArabic = outcome.Text;
                }
                catch (Exception ex)
                {
                    exitCode = 1;
                    log.AppendLine("translation_FAIL=" + ex.Message);
                    log.AppendLine("translation=" + translatedArabic + " (fallback for render)");
                }

                // Regression check for the colloquial phrase that machine
                // translation used to render as literal nonsense.
                string slang = "Calla chingada, no mames con mamar.";
                List<ExamplePair> slangExamples = new List<ExamplePair>();
                TranslationResult slangResult = new TranslationResult();
                try
                {
                    TranslationService slangTranslator = new TranslationService();
                    TranslationOutcome slangOutcome = slangTranslator.TranslateAsync(slang, "en").GetAwaiter().GetResult();
                    log.AppendLine("slang_detected=" + slangOutcome.Detected);
                    log.AppendLine("slang_engine=" + slangOutcome.Engine);
                    log.AppendLine("slang_translation=" + slangOutcome.Text);
                    if (slangOutcome.Engine != "phrasebook" ||
                        slangOutcome.Text.IndexOf("Shut the fuck up", StringComparison.OrdinalIgnoreCase) < 0 ||
                        slangOutcome.Text.IndexOf("come at me with that bullshit", StringComparison.OrdinalIgnoreCase) < 0)
                        exitCode = 1;

                    slangResult.SourceText = slang;
                    slangResult.Translation = slangOutcome.Text;
                    slangResult.Detected = slangOutcome.Detected;
                    slangResult.Target = "en";
                    slangResult.Engine = slangOutcome.Engine;
                    slangResult.MaxChars = 600;

                    ContextService slangContext = new ContextService();
                    slangExamples = slangContext.GetExamplesAsync(slang, "es", "en").GetAwaiter().GetResult();
                    log.AppendLine("slang_examples=" + slangExamples.Count);
                    foreach (ExamplePair example in slangExamples)
                        log.AppendLine("  slang: " + example.Source + " => " + example.Target);
                }
                catch (Exception ex)
                {
                    exitCode = 1;
                    log.AppendLine("slang_FAIL=" + ex.Message);
                }

                // Exercise the optional on-device path with a sentence the
                // phrasebook cannot fully cover. Ollama is optional, so this is
                // reported but never fails the build when it is absent.
                try
                {
                    TranslationService naturalTranslator = new TranslationService();
                    TranslationOutcome naturalOutcome = naturalTranslator
                        .TranslateAsync("No mames, ese cabr\u00f3n me rob\u00f3 el chingado lugar.", "en")
                        .GetAwaiter().GetResult();
                    log.AppendLine("natural_engine=" + naturalOutcome.Engine);
                    log.AppendLine("natural_translation=" + naturalOutcome.Text);
                }
                catch (Exception ex)
                {
                    log.AppendLine("natural_FAIL=" + ex.Message);
                }

                // Exercise the open-data grammar/conjugation lookup. The source
                // is network-backed, so a missing entry is reported, not fatal.
                GrammarInfo spanishGrammar = null;
                GrammarInfo arabicGrammar = null;
                try
                {
                    GrammarService grammarService = new GrammarService();
                    spanishGrammar = grammarService.LookupAsync("hablar", "es").GetAwaiter().GetResult();
                    log.AppendLine("grammar_es_has=" + spanishGrammar.HasContent);
                    log.AppendLine("grammar_es_pos=" + spanishGrammar.PartOfSpeech);
                    log.AppendLine("grammar_es_defs=" + spanishGrammar.Definitions.Count);
                    log.AppendLine("grammar_es_tenses=" + spanishGrammar.Tenses.Count);
                    if (spanishGrammar.Tenses.Count > 0)
                    {
                        GrammarTense present = spanishGrammar.Tenses[0];
                        StringBuilder formLine = new StringBuilder();
                        foreach (GrammarForm form in present.Forms)
                            if (form != null) formLine.Append(form.Value).Append(' ');
                        log.AppendLine("grammar_es_present=" + formLine.ToString().Trim());
                    }

                    arabicGrammar = grammarService.LookupAsync("\u0643\u062a\u0628", "ar").GetAwaiter().GetResult();
                    log.AppendLine("grammar_ar_has=" + arabicGrammar.HasContent);
                    log.AppendLine("grammar_ar_pos=" + arabicGrammar.PartOfSpeech);
                    log.AppendLine("grammar_ar_defs=" + arabicGrammar.Definitions.Count);
                    log.AppendLine("grammar_ar_tenses=" + arabicGrammar.Tenses.Count);
                    foreach (GrammarTense tense in arabicGrammar.Tenses)
                    {
                        StringBuilder arabicForms = new StringBuilder();
                        for (int f = 0; f < tense.Forms.Count; f++)
                        {
                            if (tense.Forms[f] == null) continue;
                            if (arabicForms.Length > 0) arabicForms.Append(" | ");
                            string label = tense.Labels != null && f < tense.Labels.Length ? tense.Labels[f] + " " : "";
                            arabicForms.Append(label).Append(tense.Forms[f].Value);
                        }
                        log.AppendLine("  " + tense.Name + ": " + arabicForms.ToString());
                    }
                    if (arabicGrammar.Tenses.Count < 3 || arabicGrammar.NonFinite.Count < 2) exitCode = 1;
                }
                catch (Exception ex)
                {
                    log.AppendLine("grammar_FAIL=" + ex.Message);
                }

                // Arabic dialect phrasebook and the English -> Arabic reverse path.
                try
                {
                    TranslationService dialectTranslator = new TranslationService();
                    TranslationOutcome egyptian = dialectTranslator.TranslateAsync("\u0625\u064a\u0647 \u062f\u0647\u061f", "en")
                        .GetAwaiter().GetResult();
                    TranslationOutcome levantine = dialectTranslator.TranslateAsync("\u0645\u0639\u0644\u0634\u060c \u062d\u0635\u0644 \u062e\u064a\u0631.", "en")
                        .GetAwaiter().GetResult();
                    TranslationOutcome vulgar = dialectTranslator.TranslateAsync("Shut the fuck up, don't fuck with me.", "ar")
                        .GetAwaiter().GetResult();
                    log.AppendLine("dialect_egyptian=" + egyptian.Text + " [" + egyptian.Engine + "]");
                    log.AppendLine("dialect_levantine=" + levantine.Text + " [" + levantine.Engine + "]");
                    log.AppendLine("dialect_vulgar_ar=" + vulgar.Text + " [" + vulgar.Engine + "]");
                    if (egyptian.Engine != "phrasebook" || egyptian.Text != "What's this?")
                        exitCode = 1;
                    if (levantine.Engine != "phrasebook" ||
                        levantine.Text.IndexOf("it turned out fine", StringComparison.OrdinalIgnoreCase) < 0)
                        exitCode = 1;
                    if (vulgar.Engine != "phrasebook" || !ArabicText.ContainsArabic(vulgar.Text))
                        exitCode = 1;

                    // Exact expressions added in the second phrasebook pass. A
                    // partial sentence is intentionally left to the model, so
                    // these checks use standalone phrases only.
                    string[][] phraseChecks = new string[][]
                    {
                        new string[] { "vete a la verga", "Go fuck yourself" },
                        new string[] { "me lleva la chingada", "I'm screwed" },
                        new string[] { "est\u00e1 chupado", "piece of cake" },
                        new string[] { "me da palo", "can't be bothered" },
                        new string[] { "\u0625\u0632\u064a\u0643", "How are you?" },
                        new string[] { "\u0625\u0650\u0632\u0650\u064a\u0643", "How are you?" },
                        new string[] { "\u0634\u0648 \u0631\u0627\u064a\u0643", "What do you think?" },
                        new string[] { "\u0637\u064a\u0628", "Okay" },
                        new string[] { "\u0628\u0639\u062f \u0628\u062e\u0644\u0635 \u0645\u0646 \u0627\u0644\u0634\u063a\u0644", "after I finish work" },
                        new string[] { "\u0643\u064a\u0641\u0646\u0643", "How are you?" },
                        new string[] { "\u0647\u0633\u064a", "now" },
                        new string[] { "\u0634\u0643\u0648 \u0645\u0627\u0643\u0648", "What's up" },
                        new string[] { "\u062f\u062d\u064a\u0646", "now" },
                        new string[] { "\u0628\u0631\u0634\u0627", "a lot" }
                    };
                    foreach (string[] check in phraseChecks)
                    {
                        TranslationOutcome phrase = dialectTranslator.TranslateAsync(check[0], "en")
                            .GetAwaiter().GetResult();
                        log.AppendLine("phrase=" + check[0] + " => " + phrase.Text + " [" + phrase.Engine + "]");
                        if (phrase.Engine != "phrasebook" ||
                            phrase.Text.IndexOf(check[1], StringComparison.OrdinalIgnoreCase) < 0)
                            exitCode = 1;
                    }

                    // Every Arabic phrasebook entry carries the lahja (dialect) it
                    // belongs to, several when the phrase is shared.
                    string sudaneseLahja = Colloquial.LahjaLabelFor(
                        "\u0628\u0639\u062f \u0628\u062e\u0644\u0635 \u0645\u0646 \u0627\u0644\u0634\u063a\u0644", "ar");
                    log.AppendLine("lahja_sudanese=" + sudaneseLahja);
                    if (sudaneseLahja != "Egyptian/Gulf/Sudanese") exitCode = 1;

                    string iraqiLahja = Colloquial.LahjaLabelFor("\u0634\u0643\u0648 \u0645\u0627\u0643\u0648", "ar");
                    log.AppendLine("lahja_iraqi=" + iraqiLahja);
                    if (iraqiLahja != "Iraqi") exitCode = 1;

                    string hijaziLahja = Colloquial.LahjaLabelFor("\u062f\u062d\u064a\u0646", "ar");
                    log.AppendLine("lahja_hijazi=" + hijaziLahja);
                    if (hijaziLahja != "Hijazi") exitCode = 1;

                    log.AppendLine("phrasebook_unlabelled=" + Colloquial.UnlabelledArabicEntries);
                    if (Colloquial.UnlabelledArabicEntries != 0) exitCode = 1;

                    string[][] englishChecks = new string[][]
                    {
                        new string[] { "good luck", "\u0628\u0627\u0644\u062a\u0648\u0641\u064a\u0642" },
                        new string[] { "what happened", "\u0634\u0648 \u0635\u0627\u0631" },
                        new string[] { "no problem", "\u0645\u0627 \u0641\u064a \u0645\u0634\u0643\u0644\u0629" }
                    };
                    foreach (string[] check in englishChecks)
                    {
                        TranslationOutcome phrase = dialectTranslator.TranslateAsync(check[0], "ar")
                            .GetAwaiter().GetResult();
                        log.AppendLine("english_phrase=" + check[0] + " => " + phrase.Text + " [" + phrase.Engine + "]");
                        if (phrase.Engine != "phrasebook" || phrase.Text.IndexOf(check[1], StringComparison.Ordinal) < 0)
                            exitCode = 1;
                    }
                }
                catch (Exception ex)
                {
                    exitCode = 1;
                    log.AppendLine("dialect_FAIL=" + ex.Message);
                }

                TranslationService wordTranslator = new TranslationService();
                TranslationOutcome wordOutcome = null;
                string wordTranslation = "\u0637\u0627\u0644\u0628";
                List<string> liveSynonyms = new List<string>();
                List<ExamplePair> liveExamples = new List<ExamplePair>();
                try
                {
                    wordOutcome = wordTranslator.TranslateAsync("student", "ar").GetAwaiter().GetResult();
                    wordTranslation = wordOutcome.Text;
                    liveSynonyms = wordOutcome.Synonyms;
                    log.AppendLine("word_translation=" + wordOutcome.Text);
                    log.AppendLine("word_synonyms_in_translation=" + liveSynonyms.Count);
                }
                catch (Exception ex)
                {
                    exitCode = 1;
                    log.AppendLine("word_translation_FAIL=" + ex.Message);
                }

                try
                {
                    if (liveSynonyms.Count == 0)
                        liveSynonyms = wordTranslator.GetSynonymsAsync("student", "ar").GetAwaiter().GetResult();
                    log.AppendLine("synonyms=" + liveSynonyms.Count + " [" + string.Join("|", liveSynonyms.ToArray()) + "]");
                }
                catch (Exception ex)
                {
                    exitCode = 1;
                    log.AppendLine("synonyms_FAIL=" + ex.Message);
                }

                try
                {
                    ContextService contextService = new ContextService();
                    liveExamples = contextService.GetExamplesAsync("student", "en", "ar").GetAwaiter().GetResult();
                    log.AppendLine("examples=" + liveExamples.Count);
                    foreach (ExamplePair example in liveExamples)
                        log.AppendLine("  " + example.Source + " => " + example.Target);
                }
                catch (Exception ex)
                {
                    exitCode = 1;
                    log.AppendLine("examples_FAIL=" + ex.Message);
                }

                // The local packs are optional at build time, so report what is
                // installed without failing the suite when a user has none.
                try
                {
                    ConcordanceStore concordance = ConcordanceStore.Shared;
                    List<ExamplePair> corpusStudent = concordance.Search("student", "en", "ar", 3, null);
                    log.AppendLine("concordance_packs=" + concordance.PackCount);
                    log.AppendLine("concordance_pairs=" + concordance.PairCount);
                    log.AppendLine("concordance_student=" + corpusStudent.Count);
                    foreach (ExamplePair corpus in corpusStudent)
                        log.AppendLine("  corpus: " + corpus.Source + " => " + corpus.Target);
                }
                catch (Exception ex)
                {
                    log.AppendLine("concordance_FAIL=" + ex.Message);
                }

                string translatedVocalized = tashkeel.Diacritize(translatedArabic);
                log.AppendLine("tashkeel_translation=" + translatedVocalized);

                string pinyinSample = null;
                try
                {
                    pinyinSample = tashkeel.Pinyin("\u4f60\u597d\uff0c\u4e16\u754c\u3002");
                    log.AppendLine("pinyin=" + pinyinSample);
                    if (pinyinSample.IndexOf("\u01d0", StringComparison.Ordinal) < 0) exitCode = 1;
                }
                catch (Exception ex)
                {
                    exitCode = 1;
                    log.AppendLine("pinyin_FAIL=" + ex.Message);
                }

                if (!string.IsNullOrEmpty(pinyinSample))
                {
                    TranslationResult chineseToEnglish = new TranslationResult();
                    chineseToEnglish.SourceText = "\u4f60\u597d\uff0c\u4e16\u754c\u3002";
                    chineseToEnglish.Translation = "Hello, world.";
                    chineseToEnglish.Detected = "zh-CN";
                    chineseToEnglish.Target = "en";
                    chineseToEnglish.MaxChars = 600;
                    PopupWindow pinyinPopup = new PopupWindow();
                    pinyinPopup.PrepareForRender(chineseToEnglish, null, null);
                    pinyinPopup.ShowPinyin(pinyinSample, chineseToEnglish.SourceText, "source");
                    Render(pinyinPopup, Path.Combine(output, "popup-pinyin-zh-en.png"));
                    log.AppendLine("rendered=popup-pinyin-zh-en.png");
                }

                TranslationResult englishToArabic = new TranslationResult();
                englishToArabic.SourceText = "The student wrote the lesson at school this morning.";
                englishToArabic.Translation = translatedArabic;
                englishToArabic.Detected = "en";
                englishToArabic.Target = "ar";
                englishToArabic.MaxChars = 600;
                PopupWindow first = new PopupWindow();
                first.PrepareForRender(englishToArabic, translatedVocalized, "translation");
                Render(first, Path.Combine(output, "popup-en-ar.png"));
                log.AppendLine("rendered=popup-en-ar.png");

                if (liveSynonyms.Count == 0)
                {
                    liveSynonyms.Add("\u0637\u0627\u0644\u0628");
                    liveSynonyms.Add("\u062a\u0644\u0645\u064a\u0630");
                }
                if (liveExamples.Count == 0)
                    liveExamples.Add(new ExamplePair { Source = "They're students.", Target = "\u0647\u0645 \u0637\u0644\u0628\u0629." });

                if (!string.IsNullOrEmpty(slangResult.Translation))
                {
                    PopupWindow slangPopup = new PopupWindow();
                    slangPopup.PrepareForRender(slangResult, null, null);
                    slangPopup.PrepareExtras(new List<string>(), slangExamples);
                    Render(slangPopup, Path.Combine(output, "popup-slang-es-en.png"));
                    log.AppendLine("rendered=popup-slang-es-en.png");
                }

                TranslationResult wordResult = new TranslationResult();
                wordResult.SourceText = "student";
                wordResult.Translation = wordTranslation;
                wordResult.Detected = "en";
                wordResult.Target = "ar";
                wordResult.MaxChars = 600;
                PopupWindow wordPopup = new PopupWindow();
                wordPopup.PrepareForRender(wordResult, null, null);
                wordPopup.PrepareExtras(liveSynonyms, liveExamples);
                Render(wordPopup, Path.Combine(output, "popup-word-en-ar.png"));
                log.AppendLine("rendered=popup-word-en-ar.png");

                try
                {
                    string switched = null;
                    PopupWindow switchPopup = new PopupWindow();
                    switchPopup.TargetChanged += delegate(string code) { switched = code; };
                    switchPopup.SelectTarget("fr");
                    log.AppendLine("popup_target_event=" + (switched == null ? "none" : switched));
                    log.AppendLine("popup_target_menu_items=" + switchPopup.TargetMenuItems);
                    // Every catalog language + Auto + the separator between them.
                    if (switched != "fr" || switchPopup.TargetMenuItems != LanguageCatalog.Count + 2) exitCode = 1;
                }
                catch (Exception ex)
                {
                    exitCode = 1;
                    log.AppendLine("popup_target_FAIL=" + ex.Message);
                }

                if (spanishGrammar != null && spanishGrammar.HasContent)
                {
                    TranslationResult grammarResult = new TranslationResult();
                    grammarResult.SourceText = "hablar";
                    grammarResult.Translation = "to talk; to speak";
                    grammarResult.Detected = "es";
                    grammarResult.Target = "en";
                    grammarResult.MaxChars = 600;
                    PopupWindow grammarPopup = new PopupWindow();
                    grammarPopup.PrepareForRender(grammarResult, null, null);
                    grammarPopup.ShowGrammar(spanishGrammar);
                    Render(grammarPopup, Path.Combine(output, "popup-grammar-es-en.png"));
                    log.AppendLine("rendered=popup-grammar-es-en.png");
                }

                if (arabicGrammar != null && arabicGrammar.HasContent)
                {
                    TranslationResult arabicGrammarResult = new TranslationResult();
                    arabicGrammarResult.SourceText = "\u0643\u062a\u0628";
                    arabicGrammarResult.Translation = "to write";
                    arabicGrammarResult.Detected = "ar";
                    arabicGrammarResult.Target = "en";
                    arabicGrammarResult.MaxChars = 600;
                    PopupWindow arabicGrammarPopup = new PopupWindow();
                    arabicGrammarPopup.PrepareForRender(arabicGrammarResult, null, null);
                    arabicGrammarPopup.ShowGrammar(arabicGrammar);
                    Render(arabicGrammarPopup, Path.Combine(output, "popup-grammar-ar-en.png"));
                    log.AppendLine("rendered=popup-grammar-ar-en.png");
                }

                TranslationResult arabicToEnglish = new TranslationResult();
                arabicToEnglish.SourceText = arabic;
                arabicToEnglish.Translation = "The student wrote the lesson at school this morning.";
                arabicToEnglish.Detected = "ar";
                arabicToEnglish.Target = "en";
                arabicToEnglish.MaxChars = 600;
                PopupWindow second = new PopupWindow();
                second.PrepareForRender(arabicToEnglish, vocalized, "source");
                Render(second, Path.Combine(output, "popup-ar-en.png"));
                log.AppendLine("rendered=popup-ar-en.png");
                tashkeel.Dispose();
            }
            catch (Exception ex)
            {
                exitCode = 1;
                log.AppendLine("FAIL: " + ex);
            }

            string logPath = Path.Combine(output, "selftest.log");
            File.WriteAllText(logPath, log.ToString(), Utf8.NoBom);
            Console.WriteLine(log.ToString());
            return exitCode;
        }

        private static void Render(PopupWindow popup, string path)
        {
            FrameworkElement root = popup.Content as FrameworkElement;
            root.Measure(new Size(528, double.PositiveInfinity));
            double height = root.DesiredSize.Height;
            root.Arrange(new Rect(0, 0, 528, height));
            root.UpdateLayout();

            int widthPx = (int)Math.Ceiling(528.0 * 2);
            int heightPx = (int)Math.Ceiling(height * 2.0);
            RenderTargetBitmap bitmap = new RenderTargetBitmap(widthPx, heightPx, 192, 192, PixelFormats.Pbgra32);
            bitmap.Render(root);

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path))
                encoder.Save(stream);
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }
    }
}
