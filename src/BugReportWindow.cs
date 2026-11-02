using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Qalam
{
    internal sealed class BugReportWindow : Window
    {
        private static BugReportWindow _open;
        private readonly Config _config;
        private readonly TextBox _details;
        private readonly TextBox _steps;
        private readonly TextBox _expected;
        private readonly TextBlock _status;

        public static void ShowReport(Config config)
        {
            if (_open != null)
            {
                _open.Activate();
                return;
            }
            _open = new BugReportWindow(config);
            _open.Closed += delegate { _open = null; };
            _open.Show();
        }

        private BugReportWindow(Config config)
        {
            _config = config;
            Title = "Report a bug \u2014 Qalam";
            Width = 700;
            Height = 660;
            MinWidth = 560;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Ui.Window;
            Foreground = Ui.Bright;
            FontFamily = new FontFamily("Segoe UI");
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            StackPanel stack = new StackPanel();
            stack.Margin = new Thickness(26, 22, 26, 26);

            TextBlock eyebrow = Ui.Label("REPORT A BUG", 9.5, Ui.Gold, FontWeights.SemiBold);
            stack.Children.Add(eyebrow);
            TextBlock title = Ui.Label("Help us find the rough edges.", 23, Ui.Bright, FontWeights.SemiBold);
            title.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(title);
            TextBlock note = Ui.Label(
                "Nothing is sent automatically. Copy the report or save it, then attach it wherever you report the issue.",
                11, Ui.Muted, FontWeights.Normal);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(note);

            TextBlock privacy = Ui.Label(
                "Included: Qalam build, Windows version, theme, provider mode, target language, and the notes below.\n" +
                "Not included: translated text, history, saved cards, or log contents.",
                10, Ui.Faint, FontWeights.Normal);
            privacy.TextWrapping = TextWrapping.Wrap;
            privacy.Margin = new Thickness(0, 14, 0, 0);
            stack.Children.Add(privacy);

            stack.Children.Add(FieldLabel("WHAT HAPPENED?"));
            _details = Field(118);
            stack.Children.Add(_details);

            stack.Children.Add(FieldLabel("STEPS TO REPRODUCE"));
            _steps = Field(82);
            stack.Children.Add(_steps);

            stack.Children.Add(FieldLabel("EXPECTED BEHAVIOR (OPTIONAL)"));
            _expected = Field(68);
            stack.Children.Add(_expected);

            _status = Ui.Label("", 10.5, Ui.Muted, FontWeights.Normal);
            _status.TextWrapping = TextWrapping.Wrap;
            _status.Margin = new Thickness(0, 13, 0, 0);
            stack.Children.Add(_status);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.Margin = new Thickness(0, 14, 0, 0);

            Button copy = Ui.PrimaryButton("Copy report", delegate { CopyReport(); });
            copy.MinWidth = 118;
            actions.Children.Add(copy);

            Button save = Ui.SmallOutlineButton("Save report\u2026", delegate { SaveReport(); });
            save.Margin = new Thickness(9, 0, 0, 0);
            actions.Children.Add(save);

            Button folder = Ui.SmallOutlineButton("Open reports folder", delegate { OpenReportsFolder(); });
            folder.Margin = new Thickness(9, 0, 0, 0);
            actions.Children.Add(folder);

            Button close = Ui.SmallOutlineButton("Close", delegate { Close(); });
            close.Margin = new Thickness(9, 0, 0, 0);
            actions.Children.Add(close);
            stack.Children.Add(actions);

            ScrollViewer scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Content = stack;
            Content = scroll;

            Loaded += delegate { _details.Focus(); };
        }

        private static TextBlock FieldLabel(string text)
        {
            TextBlock label = Ui.Label(text, 9.5, Ui.Gold, FontWeights.SemiBold);
            label.Margin = new Thickness(0, 17, 0, 7);
            return label;
        }

        private static TextBox Field(int height)
        {
            TextBox box = new TextBox();
            box.AcceptsReturn = true;
            box.TextWrapping = TextWrapping.Wrap;
            box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            box.Height = height;
            box.Padding = new Thickness(11, 9, 11, 9);
            box.Background = Ui.Input;
            box.Foreground = Ui.Bright;
            box.BorderBrush = Ui.InputBorder;
            box.BorderThickness = new Thickness(1);
            box.CaretBrush = Ui.Gold;
            box.SelectionBrush = Ui.Selection;
            box.FontSize = 12.5;
            box.FontFamily = new FontFamily("Segoe UI");
            return box;
        }

        private string BuildReport()
        {
            if (string.IsNullOrWhiteSpace(_details.Text))
            {
                _status.Text = "Describe what happened first.";
                _status.Foreground = Ui.WarnBrush;
                _details.Focus();
                return null;
            }
            return BugReport.Build(_config, _details.Text, _steps.Text, _expected.Text);
        }

        private void CopyReport()
        {
            string report = BuildReport();
            if (report == null) return;
            try
            {
                Clipboard.SetText(report);
                _status.Text = "Copied. Nothing was sent automatically.";
                _status.Foreground = Ui.Muted;
            }
            catch (Exception ex)
            {
                Logger.Write("bug report copy: " + ex.Message);
                _status.Text = "Clipboard is busy; try again or save the report.";
                _status.Foreground = Ui.WarnBrush;
            }
        }

        private void SaveReport()
        {
            string report = BuildReport();
            if (report == null) return;
            try
            {
                Directory.CreateDirectory(BugReport.ReportsDirectory);
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Title = "Save Qalam bug report";
                dialog.Filter = "Markdown report (*.md)|*.md|Text report (*.txt)|*.txt";
                dialog.DefaultExt = ".md";
                dialog.AddExtension = true;
                dialog.OverwritePrompt = true;
                dialog.InitialDirectory = BugReport.ReportsDirectory;
                dialog.FileName = "qalam-bug-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".md";
                if (dialog.ShowDialog(this) != true) return;
                File.WriteAllText(dialog.FileName, report, new UTF8Encoding(false));
                _status.Text = "Saved. Nothing was sent automatically.";
                _status.Foreground = Ui.Muted;
            }
            catch (Exception ex)
            {
                Logger.Write("bug report save: " + ex.Message);
                _status.Text = "Could not save the report: " + ex.Message;
                _status.Foreground = Ui.ErrorBrush;
            }
        }

        private void OpenReportsFolder()
        {
            try
            {
                Directory.CreateDirectory(BugReport.ReportsDirectory);
                Process.Start(new ProcessStartInfo
                {
                    FileName = BugReport.ReportsDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Write("bug report folder: " + ex.Message);
                _status.Text = "Could not open the reports folder.";
                _status.Foreground = Ui.WarnBrush;
            }
        }
    }
}
