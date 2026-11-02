using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace Qalam
{
    internal static class BugReport
    {
        private const int MaxDetails = 4000;
        private const int MaxSteps = 2000;

        public static string ReportsDirectory
        {
            get { return Path.Combine(Paths.DataDir, "reports"); }
        }

        public static string Build(Config config, string details, string steps, string expected)
        {
            string version = "unknown";
            string built = "unknown";
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                Version assemblyVersion = assembly.GetName().Version;
                if (assemblyVersion != null) version = assemblyVersion.ToString();
                if (!string.IsNullOrEmpty(assembly.Location) && File.Exists(assembly.Location))
                    built = File.GetLastWriteTime(assembly.Location).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            }
            catch { }

            string theme = config == null ? "unknown" : ThemeManager.DisplayName(config.Theme) +
                " (" + ThemeManager.Resolve(config.Theme) + ")";
            string provider = config == null ? "unknown" : (config.TranslationMode ?? "unknown");
            string target = config == null ? "unknown" :
                (config.AutoTarget ? "auto" : (config.Target ?? "unknown"));

            StringBuilder report = new StringBuilder();
            report.AppendLine("Qalam bug report");
            report.AppendLine("================");
            report.AppendLine();
            report.AppendLine("Version: " + version);
            report.AppendLine("Build: " + built);
            report.AppendLine("OS: " + Environment.OSVersion.VersionString);
            report.AppendLine("Runtime: .NET " + Environment.Version);
            report.AppendLine("Process: " + (Environment.Is64BitProcess ? "64-bit" : "32-bit"));
            report.AppendLine("Culture: " + CultureInfo.CurrentCulture.Name);
            report.AppendLine("Theme: " + theme);
            report.AppendLine("Provider mode: " + provider);
            report.AppendLine("Target: " + target);
            report.AppendLine("Languages: " + LanguageCatalog.Count);
            report.AppendLine("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture));
            report.AppendLine();
            report.AppendLine("What happened");
            report.AppendLine("------------");
            report.AppendLine(Clean(details, MaxDetails, "(not provided)"));
            report.AppendLine();
            report.AppendLine("Steps to reproduce");
            report.AppendLine("------------------");
            report.AppendLine(Clean(steps, MaxSteps, "(not provided)"));
            report.AppendLine();
            report.AppendLine("Expected behavior");
            report.AppendLine("-----------------");
            report.AppendLine(Clean(expected, MaxSteps, "(not provided)"));
            report.AppendLine();
            report.AppendLine("Privacy note");
            report.AppendLine("------------");
            report.AppendLine("This report contains only the diagnostics above and the notes entered by the user.");
            report.AppendLine("It does not include translated text, history, saved cards, or log contents.");
            return report.ToString();
        }

        public static string Save(string report)
        {
            if (string.IsNullOrEmpty(report)) throw new ArgumentException("Report is empty.", "report");
            Directory.CreateDirectory(ReportsDirectory);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string path = Path.Combine(ReportsDirectory, "qalam-bug-" + stamp + ".md");
            int suffix = 2;
            while (File.Exists(path))
            {
                path = Path.Combine(ReportsDirectory, "qalam-bug-" + stamp + "-" + suffix + ".md");
                suffix++;
            }
            File.WriteAllText(path, report, new UTF8Encoding(false));
            return path;
        }

        private static string Clean(string value, int max, string fallback)
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            string text = value.Replace("\0", "").Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (text.Length > max) text = text.Substring(0, max) + "\n(truncated)";
            return text.Length == 0 ? fallback : text;
        }
    }
}
