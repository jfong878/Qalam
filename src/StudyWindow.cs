// Study review window: one card at a time, reveal the saved translation, then
// grade it. The deck itself lives in Study.cs and stays on disk.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Qalam
{
    internal sealed class StudyWindow : Window
    {
        private static StudyWindow _instance;

        private readonly TextBlock _stats;
        private readonly TextBlock _pair;
        private readonly TextBlock _counter;
        private readonly TextBlock _front;
        private readonly TextBlock _back;
        private readonly TextBlock _status;
        private readonly TextBlock _emptyTitle;
        private readonly TextBlock _emptyBody;
        private readonly StackPanel _backBlock;
        private readonly Border _cardPanel;
        private readonly Border _emptyPanel;
        private readonly Button _revealButton;
        private readonly Button _reviewAhead;
        private readonly StackPanel _gradeRow;

        private List<StudyCard> _queue;
        private int _index;
        private StudyCard _current;
        private bool _revealed;
        private bool _reviewAll;

        public StudyWindow()
        {
            Title = "Qalam \u2014 Study";
            Width = 760;
            Height = 660;
            MinWidth = 640;
            MinHeight = 540;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Ui.Window;
            FontFamily = new FontFamily("Segoe UI");
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            Border shell = new Border();
            shell.Background = Background;
            shell.Padding = new Thickness(28, 24, 28, 24);
            StackPanel root = new StackPanel();
            shell.Child = root;

            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel heading = new StackPanel();
            heading.Children.Add(Ui.Label("Study", 24, Ui.Bright, FontWeights.SemiBold));
            TextBlock subtitle = Ui.Label("Spaced repetition from your favorites and history", 11, Ui.Muted, FontWeights.Normal);
            subtitle.Margin = new Thickness(0, 3, 0, 0);
            heading.Children.Add(subtitle);
            header.Children.Add(heading);

            _stats = Ui.Label("", 11, Ui.Gold, FontWeights.SemiBold);
            _stats.VerticalAlignment = VerticalAlignment.Center;
            _stats.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(_stats, 1);
            header.Children.Add(_stats);
            root.Children.Add(header);

            _cardPanel = new Border();
            _cardPanel.Background = Ui.Panel;
            _cardPanel.BorderBrush = Ui.Line;
            _cardPanel.BorderThickness = new Thickness(1);
            _cardPanel.CornerRadius = new CornerRadius(12);
            _cardPanel.Padding = new Thickness(22, 18, 22, 20);
            _cardPanel.Margin = new Thickness(0, 18, 0, 0);
            _cardPanel.Visibility = Visibility.Collapsed;

            StackPanel card = new StackPanel();
            Grid meta = new Grid();
            meta.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            meta.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _pair = Ui.Label("", 9.5, Ui.Gold, FontWeights.SemiBold);
            meta.Children.Add(_pair);
            _counter = Ui.Label("", 10, Ui.Muted, FontWeights.SemiBold);
            Grid.SetColumn(_counter, 1);
            meta.Children.Add(_counter);
            card.Children.Add(meta);

            _front = Ui.Label("", 24, Ui.Bright, FontWeights.Normal);
            _front.TextWrapping = TextWrapping.Wrap;
            _front.LineHeight = 34;
            _front.Margin = new Thickness(0, 16, 0, 0);
            ScrollViewer frontScroll = new ScrollViewer();
            frontScroll.MaxHeight = 190;
            frontScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            frontScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            frontScroll.Content = _front;
            card.Children.Add(frontScroll);

            _backBlock = new StackPanel();
            _backBlock.Visibility = Visibility.Collapsed;
            TextBlock backLabel = Ui.Label("BACK", 9.5, Ui.Gold, FontWeights.SemiBold);
            backLabel.Margin = new Thickness(0, 18, 0, 0);
            _backBlock.Children.Add(backLabel);
            _back = Ui.Label("", 20, Ui.Bright, FontWeights.Normal);
            _back.TextWrapping = TextWrapping.Wrap;
            _back.LineHeight = 29;
            _back.Margin = new Thickness(0, 9, 0, 0);
            ScrollViewer backScroll = new ScrollViewer();
            backScroll.MaxHeight = 160;
            backScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            backScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            backScroll.Content = _back;
            _backBlock.Children.Add(backScroll);
            card.Children.Add(_backBlock);

            _status = Ui.Label("Recall the meaning, then reveal.", 10.5, Ui.Faint, FontWeights.Normal);
            _status.TextWrapping = TextWrapping.Wrap;
            _status.Margin = new Thickness(0, 16, 0, 0);
            card.Children.Add(_status);

            _revealButton = Ui.PrimaryButton("Reveal answer", delegate { Reveal(); });
            _revealButton.MinWidth = 150;
            _revealButton.HorizontalAlignment = HorizontalAlignment.Left;
            _revealButton.Margin = new Thickness(0, 14, 0, 0);
            card.Children.Add(_revealButton);

            _gradeRow = new StackPanel();
            _gradeRow.Orientation = Orientation.Horizontal;
            _gradeRow.Margin = new Thickness(0, 14, 0, 0);
            _gradeRow.Visibility = Visibility.Collapsed;
            Button again = Ui.SmallOutlineButton("Again (1)", delegate { Grade(StudyGrade.Again); });
            again.Margin = new Thickness(0, 0, 6, 0);
            _gradeRow.Children.Add(again);
            Button hard = Ui.SmallOutlineButton("Hard (2)", delegate { Grade(StudyGrade.Hard); });
            hard.Margin = new Thickness(0, 0, 6, 0);
            _gradeRow.Children.Add(hard);
            Button good = Ui.SmallOutlineButton("Good (3)", delegate { Grade(StudyGrade.Good); });
            good.Margin = new Thickness(0, 0, 6, 0);
            _gradeRow.Children.Add(good);
            Button easy = Ui.SmallOutlineButton("Easy (4)", delegate { Grade(StudyGrade.Easy); });
            _gradeRow.Children.Add(easy);
            card.Children.Add(_gradeRow);

            _cardPanel.Child = card;
            root.Children.Add(_cardPanel);

            _emptyPanel = new Border();
            _emptyPanel.Background = Ui.Panel;
            _emptyPanel.BorderBrush = Ui.Line;
            _emptyPanel.BorderThickness = new Thickness(1);
            _emptyPanel.CornerRadius = new CornerRadius(12);
            _emptyPanel.Padding = new Thickness(22, 20, 22, 22);
            _emptyPanel.Margin = new Thickness(0, 18, 0, 0);

            StackPanel empty = new StackPanel();
            _emptyTitle = Ui.Label("Your study deck is empty", 18, Ui.Bright, FontWeights.SemiBold);
            empty.Children.Add(_emptyTitle);
            _emptyBody = Ui.Label("Save translations to favorites, then import them here.", 11.5, Ui.Muted, FontWeights.Normal);
            _emptyBody.TextWrapping = TextWrapping.Wrap;
            _emptyBody.LineHeight = 19;
            _emptyBody.Margin = new Thickness(0, 8, 0, 0);
            empty.Children.Add(_emptyBody);
            _reviewAhead = Ui.SmallOutlineButton("Review ahead", delegate
            {
                _reviewAll = true;
                Reload();
            });
            _reviewAhead.Margin = new Thickness(0, 16, 0, 0);
            _reviewAhead.Visibility = Visibility.Collapsed;
            empty.Children.Add(_reviewAhead);
            _emptyPanel.Child = empty;
            root.Children.Add(_emptyPanel);

            Grid footer = new Grid();
            footer.Margin = new Thickness(0, 18, 0, 0);
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Button import = Ui.SmallOutlineButton("Import favorites & history", delegate { ImportSaved(); });
            footer.Children.Add(import);
            TextBlock hint = Ui.Label("Space reveal  \u00b7  1\u20134 grade  \u00b7  Esc close", 10, Ui.Faint, FontWeights.Normal);
            hint.HorizontalAlignment = HorizontalAlignment.Right;
            hint.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(hint, 1);
            footer.Children.Add(hint);
            root.Children.Add(footer);

            Content = shell;

            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    Close();
                    return;
                }
                if (!_revealed && (e.Key == Key.Space || e.Key == Key.Enter))
                {
                    e.Handled = true;
                    Reveal();
                    return;
                }
                StudyGrade grade;
                if (!_revealed || !TryGradeKey(e.Key, out grade)) return;
                e.Handled = true;
                Grade(grade);
            };
        }

        public static void ShowWindow()
        {
            if (_instance == null)
            {
                _instance = new StudyWindow();
                _instance.Closed += delegate { _instance = null; };
            }
            _instance._reviewAll = false;
            _instance.Reload();
            if (!_instance.IsVisible) _instance.Show();
            _instance.Activate();
        }

        private void Reload()
        {
            _queue = _reviewAll ? StudyStore.All() : StudyStore.Due();
            _index = 0;
            UpdateStats();
            ShowNext();
        }

        private void ShowNext()
        {
            if (_queue == null || _index >= _queue.Count)
            {
                ShowEmpty();
                return;
            }

            _current = _queue[_index];
            _revealed = false;
            _cardPanel.Visibility = Visibility.Visible;
            _emptyPanel.Visibility = Visibility.Collapsed;
            _front.Text = _current.Source ?? "";
            _back.Text = _current.Translation ?? "";
            SetDirection(_front, _front.Text);
            SetDirection(_back, _back.Text);
            _pair.Text = PairLabel(_current);
            _counter.Text = (_index + 1) + " / " + _queue.Count;
            _backBlock.Visibility = Visibility.Collapsed;
            _gradeRow.Visibility = Visibility.Collapsed;
            _revealButton.Visibility = Visibility.Visible;
            _status.Text = "Recall the meaning, then reveal.";
            _status.Foreground = Ui.Faint;
            UpdateStats();
        }

        private void ShowEmpty()
        {
            _current = null;
            _revealed = false;
            _cardPanel.Visibility = Visibility.Collapsed;
            _emptyPanel.Visibility = Visibility.Visible;
            StudyStats stats = StudyStore.Stats();
            _stats.Text = stats.Due + " due  \u00b7  " + stats.Total + " cards";

            if (_index > 0 && _queue != null && _index >= _queue.Count)
            {
                _emptyTitle.Text = "Session complete";
                _emptyBody.Text = "You reviewed " + _index + " card" + (_index == 1 ? "" : "s") + ". " +
                                  (stats.Due > 0 ? stats.Due + " still due." : "Nothing else is due right now.");
            }
            else if (stats.Total == 0)
            {
                _emptyTitle.Text = "Your study deck is empty";
                _emptyBody.Text = "Save translations to favorites, then import them here. You can also import your history.";
            }
            else
            {
                _emptyTitle.Text = "No cards due";
                string next = stats.NextDue.HasValue ? " Next card " + FormatDue(stats.NextDue.Value) + "." : "";
                _emptyBody.Text = stats.Total + " card" + (stats.Total == 1 ? "" : "s") + " in the deck." + next;
            }
            _reviewAhead.Visibility = stats.Total > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Reveal()
        {
            if (_current == null || _revealed) return;
            _revealed = true;
            _back.Text = string.IsNullOrEmpty(_current.Translation) ? "No translation saved." : _current.Translation;
            SetDirection(_back, _back.Text);
            _backBlock.Visibility = Visibility.Visible;
            _gradeRow.Visibility = Visibility.Visible;
            _revealButton.Visibility = Visibility.Collapsed;
            _status.Text = "How well did you recall it?  1 Again  \u00b7  2 Hard  \u00b7  3 Good  \u00b7  4 Easy";
            _status.Foreground = Ui.Muted;
        }

        private void Grade(StudyGrade grade)
        {
            if (!_revealed || _current == null) return;
            StudyStore.Grade(_current, grade);
            _index++;
            ShowNext();
        }

        private void ImportSaved()
        {
            List<HistoryEntry> entries = new List<HistoryEntry>();
            entries.AddRange(FavoritesStore.Load());
            entries.AddRange(HistoryStore.Load());
            int added = StudyStore.Import(entries);
            _reviewAll = false;
            Reload();
            if (entries.Count == 0)
            {
                _status.Text = "No favorites or history to import yet.";
                if (_emptyPanel.Visibility == Visibility.Visible)
                    _emptyBody.Text = "No favorites or history to import yet. Save a result first.";
            }
            else if (added == 0)
            {
                _status.Text = "Everything saved is already in the deck.";
                if (_emptyPanel.Visibility == Visibility.Visible)
                    _emptyBody.Text = "Everything saved is already in the deck.";
            }
            else
            {
                _status.Text = "Imported " + added + " card" + (added == 1 ? "" : "s") + ".";
                if (_emptyPanel.Visibility == Visibility.Visible)
                    _emptyBody.Text = "Imported " + added + " card" + (added == 1 ? "" : "s") + ". Nothing is due right now.";
            }
            _status.Foreground = Ui.Muted;
        }

        private void UpdateStats()
        {
            StudyStats stats = StudyStore.Stats();
            _stats.Text = stats.Due + " due  \u00b7  " + stats.Total + " cards";
        }

        private static string PairLabel(StudyCard card)
        {
            if (card == null) return "";
            string from = string.IsNullOrEmpty(card.Detected) ? "?" : card.Detected.ToUpperInvariant();
            string to = string.IsNullOrEmpty(card.Target) ? "?" : card.Target.ToUpperInvariant();
            return from + "  \u2192  " + to;
        }

        private static string FormatDue(DateTime due)
        {
            DateTime now = DateTime.Now;
            if (due.Date == now.Date) return "today at " + due.ToString("HH:mm");
            if (due.Date == now.Date.AddDays(1)) return "tomorrow at " + due.ToString("HH:mm");
            return due.ToString("MMM d 'at' HH:mm");
        }

        private static bool TryGradeKey(Key key, out StudyGrade grade)
        {
            grade = StudyGrade.Good;
            if (key == Key.D1 || key == Key.NumPad1) { grade = StudyGrade.Again; return true; }
            if (key == Key.D2 || key == Key.NumPad2) { grade = StudyGrade.Hard; return true; }
            if (key == Key.D3 || key == Key.NumPad3) { grade = StudyGrade.Good; return true; }
            if (key == Key.D4 || key == Key.NumPad4) { grade = StudyGrade.Easy; return true; }
            return false;
        }

        private static void SetDirection(TextBlock block, string text)
        {
            bool rtl = ArabicText.ContainsArabic(text);
            block.FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            block.TextAlignment = rtl ? TextAlignment.Right : TextAlignment.Left;
            block.FontFamily = ChineseText.ContainsHan(text)
                ? new FontFamily("Microsoft YaHei, Segoe UI")
                : new FontFamily("Segoe UI");
        }

        public void PrepareForRender(StudyCard card)
        {
            _current = card;
            _queue = new List<StudyCard>();
            if (card != null) _queue.Add(card);
            _index = 0;
            _revealed = true;
            _cardPanel.Visibility = Visibility.Visible;
            _emptyPanel.Visibility = Visibility.Collapsed;
            _stats.Text = "PREVIEW";
            _pair.Text = PairLabel(card);
            _counter.Text = "1 / 1";
            _front.Text = card == null ? "" : (card.Source ?? "");
            _back.Text = card == null ? "" : (card.Translation ?? "");
            SetDirection(_front, _front.Text);
            SetDirection(_back, _back.Text);
            _backBlock.Visibility = Visibility.Visible;
            _gradeRow.Visibility = Visibility.Visible;
            _revealButton.Visibility = Visibility.Collapsed;
            _status.Text = "How well did you recall it?  1 Again  \u00b7  2 Hard  \u00b7  3 Good  \u00b7  4 Easy";
            _status.Foreground = Ui.Muted;
        }

        public void RenderToPng(string path)
        {
            FrameworkElement root = Content as FrameworkElement;
            if (root == null) return;
            root.Measure(new Size(760, 660));
            root.Arrange(new Rect(0, 0, 760, 660));
            root.UpdateLayout();
            RenderTargetBitmap bitmap = new RenderTargetBitmap(760, 660, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }
    }

    internal static class StudySelfTest
    {
        public static int Run(string outputPath)
        {
            StringBuilder log = new StringBuilder();
            int code = 0;
            DateTime now = new DateTime(2026, 11, 1, 12, 0, 0);

            StudyCard card = new StudyCard();
            card.Source = "No mames, g\u00fcey.";
            card.Translation = "No way, dude.";
            card.Detected = "es";
            card.Target = "en";
            card.Added = now;
            card.Due = now;
            card.Ease = 2.5;

            StudyScheduler.Apply(card, StudyGrade.Good, now);
            log.AppendLine("good1 interval=" + card.IntervalDays + " reps=" + card.Repetitions + " due=" + card.Due.ToString("s"));
            if (card.IntervalDays != 1 || card.Repetitions != 1 || card.Due <= now) code = 1;

            StudyScheduler.Apply(card, StudyGrade.Good, card.Due);
            log.AppendLine("good2 interval=" + card.IntervalDays + " reps=" + card.Repetitions);
            if (card.IntervalDays != 3 || card.Repetitions != 2) code = 1;

            StudyScheduler.Apply(card, StudyGrade.Easy, card.Due);
            log.AppendLine("easy interval=" + card.IntervalDays + " reps=" + card.Repetitions + " ease=" + card.Ease);
            if (card.IntervalDays < 4 || card.Ease <= 2.5) code = 1;

            StudyScheduler.Apply(card, StudyGrade.Again, card.Due);
            log.AppendLine("again interval=" + card.IntervalDays + " reps=" + card.Repetitions + " lapses=" + card.Lapses + " due=" + card.Due.ToString("s"));
            if (card.IntervalDays != 0 || card.Repetitions != 0 || card.Lapses != 1) code = 1;

            string originalSync = Paths.SyncFolder;
            string temp = Path.Combine(Path.GetTempPath(), "qalam-study-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temp);
                Paths.SyncFolder = temp;
                HistoryEntry entry = new HistoryEntry();
                entry.Source = "No mames, g\u00fcey.";
                entry.Translation = "No way, dude.";
                entry.Detected = "es";
                entry.Target = "en";
                entry.Time = now;

                int added = StudyStore.Add(entry);
                int duplicate = StudyStore.Add(entry);
                List<StudyCard> loaded = StudyStore.Load();
                log.AppendLine("store added=" + added + " duplicate=" + duplicate + " cards=" + loaded.Count);
                if (added != 1 || duplicate != 0 || loaded.Count != 1) code = 1;

                StudyCard saved = StudyStore.Grade(loaded[0], StudyGrade.Good);
                StudyCard after = StudyStore.Load()[0];
                log.AppendLine("store interval=" + saved.IntervalDays + " persisted=" + after.IntervalDays);
                if (saved.IntervalDays != 1 || after.IntervalDays != 1) code = 1;
            }
            catch (Exception ex)
            {
                code = 1;
                log.AppendLine("store_FAIL=" + ex.Message);
            }
            finally
            {
                Paths.SyncFolder = originalSync;
                try { Directory.Delete(temp, true); }
                catch { }
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string png = Path.Combine(directory ?? ".", "study-window.png");
            try
            {
                StudyWindow window = new StudyWindow();
                window.PrepareForRender(card);
                window.RenderToPng(png);
                log.AppendLine("rendered=" + png);
            }
            catch (Exception ex)
            {
                code = 1;
                log.AppendLine("window_FAIL=" + ex);
            }

            File.WriteAllText(outputPath, log.ToString(), new UTF8Encoding(false));
            Console.WriteLine(log.ToString());
            return code;
        }
    }
}
