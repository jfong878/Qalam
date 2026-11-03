using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;

namespace Qalam
{
    internal sealed class UpdateManifest
    {
        public string Version { get; set; }
        public string Url { get; set; }
        public string Sha256 { get; set; }
        public string Notes { get; set; }
        public bool Mandatory { get; set; }
    }

    internal sealed class UpdateInfo
    {
        public Version Version;
        public Uri SetupUri;
        public string Sha256;
        public string Notes;
        public bool Mandatory;

        public string VersionText
        {
            get { return Version == null ? "" : Version.ToString(); }
        }
    }

    internal static class UpdateService
    {
        private static readonly HttpClient Http = CreateClient();
        private static readonly object Gate = new object();
        private static int _running;
        private static int _installing;
        private static UpdateInfo _pending;
        private static string _pendingSetup;

        public static Version CurrentVersion
        {
            get
            {
                try
                {
                    Version version = Assembly.GetExecutingAssembly().GetName().Version;
                    return version == null ? new Version(0, 0) : version;
                }
                catch
                {
                    return new Version(0, 0);
                }
            }
        }

        public static bool HasPendingUpdate
        {
            get
            {
                lock (Gate) return _pending != null && !string.IsNullOrEmpty(_pendingSetup);
            }
        }

        public static string PendingVersionText
        {
            get
            {
                lock (Gate) return _pending == null ? "" : _pending.VersionText;
            }
        }

        public static void CheckAndDownload(Config config, bool manual)
        {
            CheckAndDownloadAsync(config, manual);
        }

        private static async void CheckAndDownloadAsync(Config config, bool manual)
        {
            if (config == null) return;
            if (string.IsNullOrWhiteSpace(config.UpdateUrl))
            {
                if (manual) ShowMessage("No update channel is configured.\n\nSet UpdateUrl in config.json to the HTTPS address of latest.json.", "Qalam update", MessageBoxImage.Information);
                return;
            }
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            {
                if (manual) ShowMessage("An update check is already running.", "Qalam update", MessageBoxImage.Information);
                return;
            }

            try
            {
                if (HasPendingUpdate)
                {
                    if (manual) Post(delegate { PromptReady(); });
                    return;
                }

                UpdateInfo update = await FindUpdateAsync(config.UpdateUrl).ConfigureAwait(false);
                if (update == null)
                {
                    if (manual) ShowMessage("Qalam " + CurrentVersion + " is up to date.", "Qalam update", MessageBoxImage.Information);
                    return;
                }

                Logger.Write("update available: " + update.VersionText);
                string setup = await DownloadAsync(update).ConfigureAwait(false);
                lock (Gate)
                {
                    _pending = update;
                    _pendingSetup = setup;
                }
                Logger.Write("update downloaded: " + update.VersionText);
                Post(delegate { PromptReady(); });
            }
            catch (Exception ex)
            {
                Logger.Write("update check: " + ex.Message);
                if (manual) ShowMessage("Could not check for updates.\n\n" + ex.Message, "Qalam update", MessageBoxImage.Warning);
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }

        private static async Task<UpdateInfo> FindUpdateAsync(string manifestUrl)
        {
            Uri manifestUri;
            if (!Uri.TryCreate((manifestUrl ?? "").Trim(), UriKind.Absolute, out manifestUri))
                throw new InvalidOperationException("UpdateUrl is not an absolute URL.");
            if (!AllowedUri(manifestUri))
                throw new InvalidOperationException("UpdateUrl must use HTTPS (HTTP is allowed only for loopback testing).");

            using (HttpResponseMessage response = await Http.GetAsync(manifestUri).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (json.Length > 256 * 1024) throw new InvalidOperationException("The update manifest is too large.");

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 256 * 1024;
                UpdateManifest manifest = serializer.Deserialize<UpdateManifest>(json);
                if (manifest == null) throw new InvalidOperationException("The update manifest is empty.");

                string versionText = (manifest.Version ?? "").Trim();
                if (versionText.StartsWith("v", StringComparison.OrdinalIgnoreCase)) versionText = versionText.Substring(1);
                Version latest;
                if (!Version.TryParse(versionText, out latest)) throw new InvalidOperationException("The update manifest has an invalid version.");
                if (latest.CompareTo(CurrentVersion) <= 0) return null;

                string package = (manifest.Url ?? "").Trim();
                if (package.Length == 0) throw new InvalidOperationException("The update manifest has no installer URL.");
                Uri setupUri = new Uri(manifestUri, package);
                if (!AllowedUri(setupUri)) throw new InvalidOperationException("The update installer must use HTTPS (HTTP is allowed only for loopback testing).");

                string hash = (manifest.Sha256 ?? "").Trim().ToLowerInvariant();
                if (!IsSha256(hash)) throw new InvalidOperationException("The update manifest has an invalid SHA-256.");

                UpdateInfo info = new UpdateInfo();
                info.Version = latest;
                info.SetupUri = setupUri;
                info.Sha256 = hash;
                info.Notes = manifest.Notes == null ? "" : manifest.Notes.Trim();
                info.Mandatory = manifest.Mandatory;
                return info;
            }
        }

        private static async Task<string> DownloadAsync(UpdateInfo update)
        {
            string directory = Path.Combine(Paths.DataDir, "updates");
            Directory.CreateDirectory(directory);
            string target = Path.Combine(directory, "Qalam-Setup-" + update.VersionText + ".exe");
            if (File.Exists(target) && string.Equals(HashFile(target), update.Sha256, StringComparison.OrdinalIgnoreCase))
                return target;

            string part = target + ".part";
            try
            {
                if (File.Exists(part)) File.Delete(part);
                using (HttpResponseMessage response = await Http.GetAsync(update.SetupUri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    long? length = response.Content.Headers.ContentLength;
                    if (length.HasValue && length.Value > 300L * 1024 * 1024)
                        throw new InvalidOperationException("The update installer is larger than 300 MB.");
                    using (Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (FileStream output = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        await input.CopyToAsync(output).ConfigureAwait(false);
                    }
                }

                string actual = HashFile(part);
                if (!string.Equals(actual, update.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The update installer checksum does not match the manifest.");
                if (File.Exists(target)) File.Delete(target);
                File.Move(part, target);
                return target;
            }
            catch
            {
                try { if (File.Exists(part)) File.Delete(part); }
                catch { }
                throw;
            }
        }

        private static void PromptReady()
        {
            UpdateInfo update;
            string setup;
            if (!TryGetPending(out update, out setup)) return;

            string notes = string.IsNullOrEmpty(update.Notes) ? "" : "\n\n" + update.Notes;
            string message = "Qalam " + update.VersionText + " is ready to install." + notes +
                "\n\nIt will install when you quit. Restart now?";
            MessageBoxResult answer = MessageBox.Show(message, "Qalam update",
                MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (answer == MessageBoxResult.Yes) InstallPending(true);
        }

        public static void InstallPending(bool restart)
        {
            UpdateInfo update;
            string setup;
            if (!BeginInstall(out update, out setup)) return;
            try
            {
                StartUpdater(update, setup, restart);
                if (restart)
                {
                    Application app = Application.Current;
                    if (app != null) app.Shutdown();
                }
            }
            catch (Exception ex)
            {
                lock (Gate) _installing = 0;
                Logger.Write("update start: " + ex.Message);
                if (restart) ShowMessage("Could not start the update.\n\n" + ex.Message, "Qalam update", MessageBoxImage.Warning);
            }
        }

        public static void InstallPendingOnExit()
        {
            UpdateInfo update;
            string setup;
            if (!BeginInstall(out update, out setup)) return;
            try
            {
                StartUpdater(update, setup, false);
            }
            catch (Exception ex)
            {
                lock (Gate) _installing = 0;
                Logger.Write("update on exit: " + ex.Message);
            }
        }

        private static bool TryGetPending(out UpdateInfo update, out string setup)
        {
            lock (Gate)
            {
                update = _pending;
                setup = _pendingSetup;
            }
            return update != null && !string.IsNullOrEmpty(setup) && File.Exists(setup);
        }

        private static bool BeginInstall(out UpdateInfo update, out string setup)
        {
            update = null;
            setup = null;
            lock (Gate)
            {
                if (_installing != 0) return false;
                if (_pending == null || string.IsNullOrEmpty(_pendingSetup)) return false;
                if (!File.Exists(_pendingSetup)) return false;
                _installing = 1;
                update = _pending;
                setup = _pendingSetup;
                return true;
            }
        }

        private static void StartUpdater(UpdateInfo update, string setup, bool restart)
        {
            string script = Path.Combine(Paths.AppDir, "update.ps1");
            if (!File.Exists(script)) throw new InvalidOperationException("The update helper is missing. Reinstall Qalam.");

            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe");
            if (!File.Exists(powershell)) throw new InvalidOperationException("Windows PowerShell was not found.");

            string arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File " + Quote(script) +
                " -Setup " + Quote(setup) +
                " -Sha256 " + update.Sha256 +
                " -WaitPid " + Process.GetCurrentProcess().Id +
                (restart ? " -Restart" : "");

            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = powershell;
            start.Arguments = arguments;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(start);
            Logger.Write("update started: " + update.VersionText + (restart ? " (restart)" : " (on quit)"));
        }

        private static string Quote(string value)
        {
            return "\"" + (value == null ? "" : value.Replace("\"", "\\\"")) + "\"";
        }

        private static bool AllowedUri(Uri uri)
        {
            if (uri == null) return false;
            if (uri.Scheme == Uri.UriSchemeHttps) return true;
            return uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            for (int i = 0; i < value.Length; i++)
                if (!Uri.IsHexDigit(value[i])) return false;
            return true;
        }

        private static string HashFile(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                StringBuilder text = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) text.Append(hash[i].ToString("x2"));
                return text.ToString();
            }
        }

        private static HttpClient CreateClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Qalam");
            return client;
        }

        private static void Post(Action action)
        {
            Application app = Application.Current;
            if (app == null) return;
            try { app.Dispatcher.BeginInvoke(action); }
            catch { }
        }

        private static void ShowMessage(string message, string title, MessageBoxImage icon)
        {
            Post(delegate { MessageBox.Show(message, title, MessageBoxButton.OK, icon); });
        }
    }
}
