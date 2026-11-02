// The Chinese character window: a bundled Unihan lookup (pinyin, radical,
// stroke count, gloss, traditional/simplified variants) with an animated
// stroke-order view built from cached Hanzi Writer stroke outlines.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Qalam
{
    internal sealed class CharacterWindow : Window
    {
        private static CharacterWindow _instance;

        private readonly TextBox _search;
        private readonly StackPanel _list;
        private readonly TextBlock _listHeader;
        private readonly TextBlock _radicals;
        private readonly TextBlock _dictionaryStatus;
        private readonly Canvas _canvas;
        private readonly TextBlock _charLabel;
        private readonly TextBlock _pinyinLabel;
        private readonly TextBlock _metaLabel;
        private readonly TextBlock _definitionLabel;
        private readonly TextBlock _variantLabel;
        private readonly TextBlock _strokeStatus;
        private readonly Button _playButton;
        private readonly Button _stepButton;
        private readonly Button _resetButton;
        private readonly DispatcherTimer _timer;

        private CharacterInfo _selected;
        private Border _selectedRow;
        private StrokeData _strokes;
        private int _visible;
        private int _request;
        private bool _headless;
        private Action _onReturn;

        public CharacterWindow()
        {
            Title = "Qalam \u2014 Chinese Characters";
            Width = 980;
            Height = 740;
            MinWidth = 820;
            MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Ui.Window;
            FontFamily = new FontFamily("Segoe UI");
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            Border shell = new Border();
            shell.Background = Background;
            shell.Padding = new Thickness(26, 22, 26, 24);
            StackPanel root = new StackPanel();
            shell.Child = root;

            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Button back = Ui.SmallOutlineButton("\u2190 Back", delegate { Return(); });
            back.VerticalAlignment = VerticalAlignment.Top;
            back.Margin = new Thickness(0, 3, 14, 0);
            back.ToolTip = "Back to the previous window (Esc)";
            heading.Children.Add(back);

            StackPanel headingText = new StackPanel();
            headingText.VerticalAlignment = VerticalAlignment.Center;
            headingText.Children.Add(Ui.Label("Chinese Characters", 24, Ui.Bright, FontWeights.SemiBold));
            TextBlock subtitle = Ui.Label("Radicals, definitions, and stroke order  \u00b7  Unihan 18.0", 11, Ui.Muted, FontWeights.Normal);
            subtitle.Margin = new Thickness(0, 3, 0, 0);
            headingText.Children.Add(subtitle);
            Grid.SetColumn(headingText, 1);
            heading.Children.Add(headingText);

            root.Children.Add(heading);

            Grid searchRow = new Grid();
            searchRow.Margin = new Thickness(0, 16, 0, 0);
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border searchBorder = new Border();
            searchBorder.Background = Ui.Input;
            searchBorder.BorderBrush = Ui.InputBorder;
            searchBorder.BorderThickness = new Thickness(1);
            searchBorder.CornerRadius = new CornerRadius(9);
            searchBorder.Padding = new Thickness(1);

            _search = new TextBox();
            _search.Background = Brushes.Transparent;
            _search.Foreground = Ui.Bright;
            _search.CaretBrush = Ui.Gold;
            _search.BorderThickness = new Thickness(0);
            _search.FontSize = 14;
            _search.Padding = new Thickness(12, 8, 12, 8);
            _search.FontFamily = new FontFamily("Microsoft YaHei, Segoe UI");
            _search.ToolTip = "Type or paste Chinese text";
            _search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) LookUp();
            };
            searchBorder.Child = _search;
            searchRow.Children.Add(searchBorder);

            Button lookUp = Ui.PrimaryButton("Look up", delegate { LookUp(); });
            lookUp.Margin = new Thickness(9, 0, 0, 0);
            lookUp.MinWidth = 92;
            Grid.SetColumn(lookUp, 1);
            searchRow.Children.Add(lookUp);
            root.Children.Add(searchRow);

            Grid body = new Grid();
            body.Margin = new Thickness(0, 18, 0, 0);
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400) });

            Border left = new Border();
            left.Background = Ui.Panel;
            left.BorderBrush = Ui.Line;
            left.BorderThickness = new Thickness(1);
            left.CornerRadius = new CornerRadius(12);
            left.Padding = new Thickness(16, 14, 16, 16);

            StackPanel leftStack = new StackPanel();
            _listHeader = Ui.Label("CHARACTERS", 9.5, Ui.Gold, FontWeights.SemiBold);
            leftStack.Children.Add(_listHeader);

            _list = new StackPanel();
            _list.Margin = new Thickness(0, 8, 0, 0);
            leftStack.Children.Add(_list);

            TextBlock radicalHeader = Ui.Label("RADICALS", 9.5, Ui.Gold, FontWeights.SemiBold);
            radicalHeader.Margin = new Thickness(0, 12, 0, 0);
            leftStack.Children.Add(radicalHeader);
            _radicals = Ui.Label("", 11, Ui.Muted, FontWeights.Normal);
            _radicals.TextWrapping = TextWrapping.Wrap;
            _radicals.LineHeight = 19;
            _radicals.Margin = new Thickness(0, 5, 0, 0);
            leftStack.Children.Add(_radicals);

            _dictionaryStatus = Ui.Label("", 9.5, Ui.Faint, FontWeights.Normal);
            _dictionaryStatus.TextWrapping = TextWrapping.Wrap;
            _dictionaryStatus.Margin = new Thickness(0, 12, 0, 0);
            leftStack.Children.Add(_dictionaryStatus);

            ScrollViewer listScroll = new ScrollViewer();
            listScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            listScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            listScroll.Content = leftStack;
            left.Child = listScroll;
            body.Children.Add(left);

            Border right = new Border();
            right.Background = Ui.Panel;
            right.BorderBrush = Ui.Line;
            right.BorderThickness = new Thickness(1);
            right.CornerRadius = new CornerRadius(12);
            right.Padding = new Thickness(18, 16, 18, 16);
            right.Margin = new Thickness(14, 0, 0, 0);

            StackPanel detail = new StackPanel();
            Border charBox = new Border();
            charBox.Width = 300;
            charBox.Height = 300;
            charBox.CornerRadius = new CornerRadius(14);
            charBox.Background = Ui.Deep;
            charBox.BorderBrush = Ui.Line;
            charBox.BorderThickness = new Thickness(1);
            charBox.HorizontalAlignment = HorizontalAlignment.Center;

            _canvas = new Canvas();
            _canvas.Width = 300;
            _canvas.Height = 300;
            charBox.Child = _canvas;
            detail.Children.Add(charBox);

            _charLabel = Ui.Label("\u5b57", 30, Ui.TashText, FontWeights.Normal);
            _charLabel.FontFamily = new FontFamily("Microsoft YaHei, Segoe UI");
            _charLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _charLabel.Margin = new Thickness(0, 10, 0, 0);
            detail.Children.Add(_charLabel);

            _pinyinLabel = Ui.Label("", 15, Ui.Gold, FontWeights.SemiBold);
            _pinyinLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _pinyinLabel.Margin = new Thickness(0, 1, 0, 0);
            detail.Children.Add(_pinyinLabel);

            _metaLabel = Ui.Label("", 10.5, Ui.Muted, FontWeights.Normal);
            _metaLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _metaLabel.TextAlignment = TextAlignment.Center;
            _metaLabel.TextWrapping = TextWrapping.Wrap;
            _metaLabel.Margin = new Thickness(0, 5, 0, 0);
            detail.Children.Add(_metaLabel);

            _definitionLabel = Ui.Label("", 12.5, Ui.Bright, FontWeights.Normal);
            _definitionLabel.TextWrapping = TextWrapping.Wrap;
            _definitionLabel.LineHeight = 20;
            _definitionLabel.Margin = new Thickness(0, 12, 0, 0);
            detail.Children.Add(_definitionLabel);

            _variantLabel = Ui.Label("", 10, Ui.Faint, FontWeights.Normal);
            _variantLabel.TextWrapping = TextWrapping.Wrap;
            _variantLabel.Margin = new Thickness(0, 6, 0, 0);
            detail.Children.Add(_variantLabel);

            StackPanel controls = new StackPanel();
            controls.Orientation = Orientation.Horizontal;
            controls.HorizontalAlignment = HorizontalAlignment.Center;
            controls.Margin = new Thickness(0, 14, 0, 0);

            _playButton = Ui.PrimaryButton("Play", delegate { Play(); });
            _playButton.MinWidth = 74;
            _playButton.IsEnabled = false;
            controls.Children.Add(_playButton);

            _stepButton = Ui.SmallOutlineButton("Step", delegate { Step(); });
            _stepButton.Margin = new Thickness(8, 0, 0, 0);
            _stepButton.IsEnabled = false;
            controls.Children.Add(_stepButton);

            _resetButton = Ui.SmallOutlineButton("Restart", delegate { Reset(); });
            _resetButton.Margin = new Thickness(8, 0, 0, 0);
            _resetButton.IsEnabled = false;
            controls.Children.Add(_resetButton);
            detail.Children.Add(controls);

            _strokeStatus = Ui.Label("", 10.5, Ui.Faint, FontWeights.Normal);
            _strokeStatus.HorizontalAlignment = HorizontalAlignment.Center;
            _strokeStatus.TextWrapping = TextWrapping.Wrap;
            _strokeStatus.Margin = new Thickness(0, 10, 0, 0);
            detail.Children.Add(_strokeStatus);

            TextBlock credit = Ui.Label("Stroke data: Hanzi Writer / Make Me a Hanzi  \u00b7  Arphic Public License", 8.5, Ui.Faint, FontWeights.Normal);
            credit.HorizontalAlignment = HorizontalAlignment.Center;
            credit.Margin = new Thickness(0, 14, 0, 0);
            detail.Children.Add(credit);

            right.Child = detail;
            Grid.SetColumn(right, 1);
            body.Children.Add(right);

            root.Children.Add(body);

            ScrollViewer scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Content = shell;
            Content = scroll;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(460);
            _timer.Tick += delegate { Advance(); };
            Closed += delegate { _timer.Stop(); };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key != Key.Escape) return;
                e.Handled = true;
                Return();
            };

            LoadText("");
        }

        public static void ShowFor(string text, Action onReturn = null)
        {
            if (_instance == null)
            {
                _instance = new CharacterWindow();
                _instance.Closed += delegate { _instance = null; };
            }
            _instance._onReturn = onReturn;
            _instance.Search(text);
            if (!_instance.IsVisible) _instance.Show();
            _instance.Activate();
        }

        private void Return()
        {
            Action back = _onReturn;
            _onReturn = null;
            Close();
            if (back != null) back();
        }

        public void PrepareForRender(string text)
        {
            _headless = true;
            _search.Text = text;
            LoadText(text);
        }

        public void Search(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                _search.Text = text;
                LoadText(text);
            }
            else if (string.IsNullOrEmpty(_search.Text))
            {
                LoadText("");
            }
            _search.Focus();
            _search.CaretIndex = _search.Text.Length;
        }

        private void LookUp()
        {
            LoadText(_search.Text);
        }

        private void LoadText(string text)
        {
            _request++;
            _timer.Stop();
            _list.Children.Clear();
            _selected = null;
            _selectedRow = null;
            _strokes = null;
            _visible = 0;
            _canvas.Children.Clear();

            List<CharacterInfo> characters = CharacterDictionary.ForText(text, 32);
            _listHeader.Text = characters.Count == 0
                ? "CHARACTERS"
                : characters.Count + " CHARACTER" + (characters.Count == 1 ? "" : "S") +
                  "  \u00b7  " + CharacterDictionary.Count.ToString("N0") + " IN DICTIONARY";

            if (characters.Count == 0)
            {
                ClearDetail();
                _radicals.Text = "";
                _dictionaryStatus.Text = CharacterDictionary.Count == 0
                    ? CharacterDictionary.Error
                    : "Type Chinese text above, or highlight it anywhere and press Ctrl + C + C.";
                return;
            }

            foreach (CharacterInfo info in characters)
                _list.Children.Add(BuildRow(info));

            UpdateRadicals(characters);
            _dictionaryStatus.Text = CharacterDictionary.Count == 0 ? CharacterDictionary.Error : "";
            SelectCharacter(characters[0], _list.Children[0] as Border);
        }

        private Border BuildRow(CharacterInfo info)
        {
            Border row = new Border();
            row.Background = Brushes.Transparent;
            row.CornerRadius = new CornerRadius(9);
            row.Padding = new Thickness(10, 7, 10, 8);
            row.Margin = new Thickness(0, 0, 0, 4);
            row.Cursor = Cursors.Hand;

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock glyph = Ui.Label(info.Char.ToString(), 30, Ui.TashText, FontWeights.Normal);
            glyph.FontFamily = new FontFamily("Microsoft YaHei, Segoe UI");
            glyph.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(glyph);

            StackPanel words = new StackPanel();
            words.Margin = new Thickness(4, 0, 0, 0);
            words.VerticalAlignment = VerticalAlignment.Center;

            TextBlock pinyin = Ui.Label(string.IsNullOrEmpty(info.Pinyin) ? "\u2014" : info.Pinyin,
                                        12.5, Ui.Gold, FontWeights.SemiBold);
            words.Children.Add(pinyin);

            TextBlock meta = Ui.Label(MetaText(info), 9.5, Ui.Faint, FontWeights.Normal);
            meta.Margin = new Thickness(0, 1, 0, 0);
            meta.TextWrapping = TextWrapping.Wrap;
            words.Children.Add(meta);

            string definition = string.IsNullOrEmpty(info.Definition) ? "No Unihan entry for this character." : info.Definition;
            TextBlock gloss = Ui.Label(definition, 11, Ui.Muted, FontWeights.Normal);
            gloss.TextWrapping = TextWrapping.Wrap;
            gloss.MaxHeight = 34;
            gloss.TextTrimming = TextTrimming.CharacterEllipsis;
            gloss.Margin = new Thickness(0, 3, 0, 0);
            words.Children.Add(gloss);

            Grid.SetColumn(words, 1);
            grid.Children.Add(words);
            row.Child = grid;
            row.Tag = info;
            row.MouseLeftButtonUp += delegate { SelectCharacter(info, row); };
            return row;
        }

        private static string MetaText(CharacterInfo info)
        {
            string text = "";
            if (info.RadicalNumber > 0)
            {
                text = "Radical " + info.RadicalNumber;
                if (!string.IsNullOrEmpty(info.Radical)) text += "  " + info.Radical;
                string meaning = CharacterDictionary.ShortMeaning(info.RadicalMeaning, 26);
                if (meaning.Length > 0) text += " (" + meaning + ")";
            }
            if (info.TotalStrokes > 0)
                text += (text.Length > 0 ? "  \u00b7  " : "") + info.TotalStrokes + " strokes";
            if (info.ExtraStrokes > 0)
                text += "  \u00b7  +" + info.ExtraStrokes + " beyond the radical";
            return text.Length == 0 ? "No radical data in Unihan." : text;
        }

        private static string VariantText(CharacterInfo info)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(info.Traditional) && info.Traditional[0] != info.Char)
                parts.Add("Traditional  " + info.Traditional);
            if (!string.IsNullOrEmpty(info.Simplified) && info.Simplified[0] != info.Char)
                parts.Add("Simplified  " + info.Simplified);
            return string.Join("   \u00b7   ", parts.ToArray());
        }

        private void UpdateRadicals(List<CharacterInfo> characters)
        {
            List<string> parts = new List<string>();
            List<char> seen = new List<char>();
            foreach (CharacterInfo info in characters)
            {
                if (info.RadicalNumber <= 0 || string.IsNullOrEmpty(info.Radical)) continue;
                char radical = info.Radical[0];
                if (seen.Contains(radical)) continue;
                seen.Add(radical);
                string meaning = CharacterDictionary.ShortMeaning(info.RadicalMeaning, 24);
                parts.Add(info.Radical + (meaning.Length == 0 ? "" : "  " + meaning) +
                          "  (#" + info.RadicalNumber + ")");
            }
            _radicals.Text = parts.Count == 0
                ? "No radical information for these characters."
                : string.Join("   \u00b7   ", parts.ToArray());
        }

        private async void SelectCharacter(CharacterInfo info, Border row)
        {
            if (info == null) return;
            _selected = info;
            if (_selectedRow != null) _selectedRow.Background = Brushes.Transparent;
            _selectedRow = row;
            if (row != null) row.Background = Ui.SelectedRow;

            int request = ++_request;
            _timer.Stop();
            _charLabel.Text = info.Char.ToString();
            _pinyinLabel.Text = string.IsNullOrEmpty(info.Pinyin) ? "\u2014" : info.Pinyin;
            _metaLabel.Text = MetaText(info);
            _definitionLabel.Text = string.IsNullOrEmpty(info.Definition)
                ? "No definition is available in Unihan for this character."
                : info.Definition;
            _variantLabel.Text = VariantText(info);
            _strokes = null;
            _visible = 0;
            _canvas.Children.Clear();
            _playButton.IsEnabled = false;
            _stepButton.IsEnabled = false;
            _resetButton.IsEnabled = false;
            _strokeStatus.Foreground = Ui.Muted;
            _strokeStatus.Text = "Loading stroke data\u2026";

            if (_headless)
            {
                try
                {
                    ApplyStrokes(StrokeService.LoadAsync(info.Char).GetAwaiter().GetResult(), request);
                }
                catch (Exception ex)
                {
                    Logger.Write("strokes: " + ex.Message);
                }
                return;
            }

            try
            {
                StrokeData data = await StrokeService.LoadAsync(info.Char);
                ApplyStrokes(data, request);
            }
            catch (Exception ex)
            {
                if (request != _request) return;
                Logger.Write("strokes: " + ex.Message);
                _strokeStatus.Foreground = Ui.WarnBrush;
                _strokeStatus.Text = "Stroke data needs a connection the first time; cached characters still work.";
            }
        }

        private void ApplyStrokes(StrokeData data, int request)
        {
            if (request != _request || data == null) return;
            _strokes = data;
            _visible = data.Strokes.Count;
            RenderStrokes();
            bool hasStrokes = data.Strokes.Count > 0;
            _playButton.IsEnabled = hasStrokes;
            _stepButton.IsEnabled = hasStrokes;
            _resetButton.IsEnabled = hasStrokes;
            _strokeStatus.Foreground = Ui.Muted;
            _strokeStatus.Text = hasStrokes
                ? data.Strokes.Count + " strokes  \u00b7  Play to replay"
                : "No stroke data for this character.";
        }

        private void ClearDetail()
        {
            _selected = null;
            _charLabel.Text = "\u5b57";
            _pinyinLabel.Text = "";
            _metaLabel.Text = "";
            _definitionLabel.Text = "Select a character to see its radical, definition, and stroke order.";
            _variantLabel.Text = "";
            _canvas.Children.Clear();
            _strokes = null;
            _visible = 0;
            _playButton.IsEnabled = false;
            _stepButton.IsEnabled = false;
            _resetButton.IsEnabled = false;
            _strokeStatus.Text = "";
        }

        private void RenderStrokes()
        {
            _canvas.Children.Clear();
            if (_strokes == null) return;
            foreach (System.Windows.Shapes.Path path in StrokeService.BuildPaths(_strokes, _visible, 300))
                _canvas.Children.Add(path);
        }

        private void Play()
        {
            if (_strokes == null || _strokes.Strokes.Count == 0) return;
            _timer.Stop();
            _visible = 0;
            RenderStrokes();
            _strokeStatus.Foreground = Ui.Muted;
            _strokeStatus.Text = "0 / " + _strokes.Strokes.Count + " strokes";
            _timer.Start();
        }

        private void Step()
        {
            if (_strokes == null || _strokes.Strokes.Count == 0) return;
            _timer.Stop();
            if (_visible < _strokes.Strokes.Count) _visible++;
            RenderStrokes();
            _strokeStatus.Foreground = Ui.Muted;
            _strokeStatus.Text = _visible + " / " + _strokes.Strokes.Count + " strokes";
        }

        private void Reset()
        {
            if (_strokes == null) return;
            _timer.Stop();
            _visible = 0;
            RenderStrokes();
            _strokeStatus.Foreground = Ui.Muted;
            _strokeStatus.Text = "0 / " + _strokes.Strokes.Count + " strokes";
        }

        private void Advance()
        {
            if (_strokes == null || _visible >= _strokes.Strokes.Count)
            {
                _timer.Stop();
                if (_strokes != null)
                    _strokeStatus.Text = _strokes.Strokes.Count + " strokes  \u00b7  Play to replay";
                return;
            }
            _visible++;
            RenderStrokes();
            _strokeStatus.Text = _visible + " / " + _strokes.Strokes.Count + " strokes";
        }

        public static void RenderStrokesPng(StrokeData data, string path, int visible)
        {
            Canvas canvas = new Canvas();
            canvas.Width = 300;
            canvas.Height = 300;
            canvas.Background = Ui.Canvas;
            foreach (System.Windows.Shapes.Path stroke in StrokeService.BuildPaths(data, visible, 300))
                canvas.Children.Add(stroke);

            canvas.Measure(new Size(300, 300));
            canvas.Arrange(new Rect(0, 0, 300, 300));
            canvas.UpdateLayout();

            RenderTargetBitmap bitmap = new RenderTargetBitmap(300, 300, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }
    }

    internal static class CharacterSelfTest
    {
        public static int Run(string outputPath)
        {
            StringBuilder log = new StringBuilder();
            int code = 0;

            log.AppendLine("data=" + CharacterDictionary.DataFile);
            log.AppendLine("count=" + CharacterDictionary.Count);
            if (CharacterDictionary.Count < 20000) code = 1;

            CharacterInfo hao = CharacterDictionary.Lookup('\u597d');
            log.AppendLine("hao=" + Describe(hao));
            if (hao == null || hao.RadicalNumber != 38 || hao.TotalStrokes != 6 || hao.Pinyin != "h\u01ceo")
                code = 1;

            CharacterInfo xue = CharacterDictionary.Lookup('\u5b78');
            log.AppendLine("xue=" + Describe(xue));
            if (xue == null || xue.Simplified != "\u5b66" || xue.TotalStrokes != 16) code = 1;

            CharacterInfo ai = CharacterDictionary.Lookup('\u7231');
            log.AppendLine("ai=" + Describe(ai));
            if (ai == null || ai.Traditional != "\u611b") code = 1;

            CharacterInfo chang = CharacterDictionary.Lookup('\u957f');
            log.AppendLine("chang=" + Describe(chang));
            if (chang == null || chang.RadicalNumber != 168) code = 1;

            List<CharacterInfo> text = CharacterDictionary.ForText("\u4f60\u597d\uff0c\u4e16\u754c\u3002", 8);
            log.AppendLine("text_chars=" + text.Count);
            if (text.Count != 4) code = 1;

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string png = Path.Combine(directory ?? ".", "character-stroke-hao.png");
            try
            {
                StrokeData strokes = StrokeService.LoadAsync('\u597d').GetAwaiter().GetResult();
                log.AppendLine("stroke_count=" + strokes.Strokes.Count);
                if (strokes.Strokes.Count != 6) code = 1;
                CharacterWindow.RenderStrokesPng(strokes, png, strokes.Strokes.Count);
                log.AppendLine("rendered=" + png);
            }
            catch (Exception ex)
            {
                code = 1;
                log.AppendLine("strokes_FAIL=" + ex.Message);
            }

            try
            {
                string windowPng = Path.Combine(directory ?? ".", "character-window.png");
                CharacterWindow window = new CharacterWindow();
                window.PrepareForRender("\u4f60\u597d\uff0c\u4e16\u754c\u3002");
                FrameworkElement content = window.Content as FrameworkElement;
                content.Measure(new Size(980, 740));
                content.Arrange(new Rect(0, 0, 980, 740));
                content.UpdateLayout();
                RenderTargetBitmap bitmap = new RenderTargetBitmap(980, 740, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (FileStream stream = File.Create(windowPng)) encoder.Save(stream);
                log.AppendLine("rendered=" + windowPng);
            }
            catch (Exception ex)
            {
                code = 1;
                log.AppendLine("window_FAIL=" + ex.Message);
            }

            File.WriteAllText(outputPath, log.ToString(), new UTF8Encoding(false));
            Console.WriteLine(log.ToString());
            return code;
        }

        private static string Describe(CharacterInfo info)
        {
            if (info == null) return "missing";
            return info.Char + " pinyin=" + info.Pinyin + " radical=" + info.RadicalNumber + info.Radical +
                   " extra=" + info.ExtraStrokes + " strokes=" + info.TotalStrokes +
                   " trad=" + info.Traditional + " simp=" + info.Simplified +
                   " def=" + info.Definition;
        }
    }
}
