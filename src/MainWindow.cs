// The normal, visible Qalam window. Ctrl + C + C still opens the compact
// popup; this window is the launcher/taskbar surface and a full translation
// workspace. Closing it hides the window and leaves the hook running.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace Qalam
{
    internal sealed class MainWindow : Window
    {
        private readonly Config _config;
        private readonly TranslationService _translator;
        private readonly TashkeelClient _tashkeel;
        private readonly GrammarService _grammar;
        private readonly Action<TranslationResult> _onResult;
        private readonly Action _onTranslateDocument;

        private readonly TextBox _input;
        private readonly TextBlock _placeholder;
        private readonly TextBlock _status;
        private readonly TextBlock _sourceText;
        private readonly TextBlock _translation;
        private readonly TextBlock _langPair;
        private readonly TextBlock _tashkeelText;
        private readonly TextBlock _tashkeelTitle;
        private string _transcriptionSpeech = "";
        private string _transcriptionSpeechLang = "ar";
        private string _transcriptionKind = "TASHKEEL";
        private readonly Border _resultCard;
        private readonly Border _tashkeelCard;
        private readonly Border _grammarCard;
        private readonly TextBlock _grammarTitle;
        private readonly TextBlock _grammarText;
        private readonly Button _targetButton;
        private readonly Button _providerButton;
        private readonly Button _translateButton;
        private readonly Button _copyButton;
        private readonly ContextMenu _targetMenu;
        private readonly ContextMenu _providerMenu;
        private readonly Button _themeButton;
        private readonly ContextMenu _themeMenu;
        private readonly Button _wordingButton;
        private readonly Border _wordingCard;
        private readonly TextBlock _wordingText;

        private bool _busy;
        private bool _allowClose;
        private string _lastTranslation = "";
        private TranslationResult _currentResult;
        private int _requestId;

        public MainWindow(Config config, TranslationService translator, TashkeelClient tashkeel,
            GrammarService grammar, Action<TranslationResult> onResult, Action onTranslateDocument)
        {
            _config = config;
            _translator = translator;
            _tashkeel = tashkeel;
            _grammar = grammar;
            _onResult = onResult;
            _onTranslateDocument = onTranslateDocument;

            Title = "Qalam \u2014 Translation";
            Width = 760;
            Height = 650;
            MinWidth = 640;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Ui.Window;
            FontFamily = new FontFamily("Segoe UI");
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            try { Icon = LoadWindowIcon(); }
            catch { }

            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                if (_allowClose) return;
                e.Cancel = true;
                Hide();
            };

            Border shell = new Border();
            shell.Padding = new Thickness(28, 24, 28, 26);
            StackPanel stack = new StackPanel();
            shell.Child = stack;

            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border mark = new Border();
            mark.Width = 46;
            mark.Height = 46;
            mark.CornerRadius = new CornerRadius(13);
            mark.Background = Ui.Mark;
            mark.BorderBrush = Ui.Gold;
            mark.BorderThickness = new Thickness(1);
            TextBlock qaaf = Ui.Label("\u0642", 25, Ui.Gold, FontWeights.SemiBold);
            qaaf.HorizontalAlignment = HorizontalAlignment.Center;
            qaaf.VerticalAlignment = VerticalAlignment.Center;
            mark.Child = qaaf;
            Grid.SetColumn(mark, 0);
            header.Children.Add(mark);

            StackPanel titleStack = new StackPanel();
            titleStack.Margin = new Thickness(14, 0, 0, 0);
            titleStack.VerticalAlignment = VerticalAlignment.Center;
            TextBlock title = Ui.Label("Qalam", 25, Ui.Bright, FontWeights.SemiBold);
            titleStack.Children.Add(title);
            TextBlock subtitle = Ui.Label("Translation, Context, Arabic Tashkeel, Chinese Characters & PinYin", 10.5, Ui.Muted, FontWeights.Normal);
            subtitle.TextWrapping = TextWrapping.Wrap;
            subtitle.Margin = new Thickness(0, 2, 8, 0);
            titleStack.Children.Add(subtitle);
            Grid.SetColumn(titleStack, 1);
            header.Children.Add(titleStack);

            _providerButton = Ui.SmallOutlineButton("Auto", delegate { OpenProviderMenu(); });
            _providerButton.FontSize = 11.5;
            _providerButton.Padding = new Thickness(0);
            _providerButton.VerticalAlignment = VerticalAlignment.Center;
            _providerButton.MinWidth = 108;
            _providerButton.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetColumn(_providerButton, 2);
            header.Children.Add(_providerButton);

            _targetButton = Ui.SmallOutlineButton("Arabic", delegate { OpenTargetMenu(); });
            _targetButton.FontSize = 11.5;
            _targetButton.Padding = new Thickness(0);
            _targetButton.VerticalAlignment = VerticalAlignment.Center;
            _targetButton.MinWidth = 146;
            Grid.SetColumn(_targetButton, 3);
            header.Children.Add(_targetButton);

            _themeButton = Ui.SmallOutlineButton("System  \u25be", delegate { OpenThemeMenu(); });
            _themeButton.FontSize = 11.5;
            _themeButton.Padding = new Thickness(0);
            _themeButton.VerticalAlignment = VerticalAlignment.Center;
            _themeButton.MinWidth = 88;
            _themeButton.Margin = new Thickness(8, 0, 0, 0);
            _themeButton.ToolTip = "Theme: System follows the Windows app theme";
            Grid.SetColumn(_themeButton, 4);
            header.Children.Add(_themeButton);

            _targetMenu = BuildTargetMenu();
            _providerMenu = BuildProviderMenu();
            _themeMenu = BuildThemeMenu();
            stack.Children.Add(header);

            TextBlock inputLabel = Ui.Label("TEXT", 9.5, Ui.Gold, FontWeights.SemiBold);
            inputLabel.Margin = new Thickness(0, 24, 0, 7);
            stack.Children.Add(inputLabel);

            Border inputBorder = new Border();
            inputBorder.Background = Ui.Input;
            inputBorder.BorderBrush = Ui.InputBorder;
            inputBorder.BorderThickness = new Thickness(1);
            inputBorder.CornerRadius = new CornerRadius(11);
            inputBorder.Padding = new Thickness(1);

            Grid inputHost = new Grid();
            _input = new TextBox();
            _input.AcceptsReturn = true;
            _input.TextWrapping = TextWrapping.Wrap;
            _input.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _input.VerticalContentAlignment = VerticalAlignment.Top;
            _input.Background = Brushes.Transparent;
            _input.Foreground = Ui.Bright;
            _input.CaretBrush = Ui.Gold;
            _input.SelectionBrush = Ui.Selection;
            _input.BorderThickness = new Thickness(0);
            _input.FontSize = 15;
            _input.FontFamily = new FontFamily("Segoe UI");
            _input.Padding = new Thickness(14, 12, 14, 12);
            _input.MinHeight = 150;
            _input.MaxHeight = 260;
            _input.TextChanged += delegate
            {
                UpdatePlaceholder();
                if (_wordingCard != null) _wordingCard.Visibility = Visibility.Collapsed;
            };
            _input.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    e.Handled = true;
                    Translate();
                }
            };
            inputHost.Children.Add(_input);

            _placeholder = Ui.Label("Type or paste text to translate\u2026", 15, Ui.Faint, FontWeights.Normal);
            _placeholder.IsHitTestVisible = false;
            _placeholder.Margin = new Thickness(15, 14, 15, 0);
            _placeholder.VerticalAlignment = VerticalAlignment.Top;
            inputHost.Children.Add(_placeholder);
            inputBorder.Child = inputHost;
            stack.Children.Add(inputBorder);

            Grid actions = new Grid();
            actions.Margin = new Thickness(0, 12, 0, 0);
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _translateButton = Ui.PrimaryButton("Translate", delegate { Translate(); });
            _translateButton.FontSize = 12.5;
            _translateButton.MinWidth = 108;
            Grid.SetColumn(_translateButton, 0);
            actions.Children.Add(_translateButton);

            Button document = Ui.SmallOutlineButton("Translate document\u2026", delegate
            {
                if (_onTranslateDocument != null) _onTranslateDocument();
            });
            document.Margin = new Thickness(9, 0, 0, 0);
            Grid.SetColumn(document, 1);
            actions.Children.Add(document);

            Button clear = Ui.SmallOutlineButton("Clear", delegate
            {
                _input.Clear();
                _resultCard.Visibility = Visibility.Collapsed;
                _tashkeelCard.Visibility = Visibility.Collapsed;
                _grammarCard.Visibility = Visibility.Collapsed;
                _wordingCard.Visibility = Visibility.Collapsed;
                _status.Text = "";
                _input.Focus();
            });
            clear.Margin = new Thickness(9, 0, 0, 0);
            Grid.SetColumn(clear, 2);
            actions.Children.Add(clear);

            Button characters = Ui.SmallOutlineButton("Characters", delegate
            {
                string text = _input.Text;
                if (!ChineseText.ContainsHan(text) && _currentResult != null &&
                    ChineseText.ContainsHan(_currentResult.Translation)) text = _currentResult.Translation;
                CharacterWindow.ShowFor(text, delegate { Activate(); });
            });
            characters.Margin = new Thickness(9, 0, 0, 0);
            Grid.SetColumn(characters, 3);
            actions.Children.Add(characters);

            _wordingButton = Ui.SmallOutlineButton("Improve", delegate { ImproveWording(); });
            _wordingButton.Margin = new Thickness(9, 0, 0, 0);
            _wordingButton.MinWidth = 82;
            _wordingButton.ToolTip = "Suggest more natural wording with the local Ollama model";
            Grid.SetColumn(_wordingButton, 4);
            actions.Children.Add(_wordingButton);

            _status = Ui.Label("", 11, Ui.Muted, FontWeights.Normal);
            _status.HorizontalAlignment = HorizontalAlignment.Right;
            _status.VerticalAlignment = VerticalAlignment.Center;
            _status.TextTrimming = TextTrimming.CharacterEllipsis;
            _status.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(_status, 5);
            actions.Children.Add(_status);
            stack.Children.Add(actions);

            _wordingCard = new Border();
            _wordingCard.Background = Ui.Panel;
            _wordingCard.BorderBrush = Ui.Line;
            _wordingCard.BorderThickness = new Thickness(1);
            _wordingCard.CornerRadius = new CornerRadius(12);
            _wordingCard.Padding = new Thickness(18, 14, 18, 16);
            _wordingCard.Margin = new Thickness(0, 14, 0, 0);
            _wordingCard.Visibility = Visibility.Collapsed;

            StackPanel wordingStack = new StackPanel();
            Grid wordingHeader = new Grid();
            wordingHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            wordingHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock wordingLabel = Ui.Label("BETTER WORDING", 9.5, Ui.Gold, FontWeights.SemiBold);
            wordingLabel.VerticalAlignment = VerticalAlignment.Center;
            wordingHeader.Children.Add(wordingLabel);
            Button useWording = Ui.SmallOutlineButton("Use wording", delegate { UseWording(); });
            Grid.SetColumn(useWording, 1);
            wordingHeader.Children.Add(useWording);
            wordingStack.Children.Add(wordingHeader);
            _wordingText = Ui.Label("", 14, Ui.Bright, FontWeights.Normal);
            _wordingText.TextWrapping = TextWrapping.Wrap;
            _wordingText.LineHeight = 22;
            _wordingText.Margin = new Thickness(0, 10, 0, 0);
            wordingStack.Children.Add(_wordingText);
            _wordingCard.Child = wordingStack;
            stack.Children.Add(_wordingCard);

            _resultCard = new Border();
            _resultCard.Background = Ui.Panel;
            _resultCard.BorderBrush = Ui.Line;
            _resultCard.BorderThickness = new Thickness(1);
            _resultCard.CornerRadius = new CornerRadius(12);
            _resultCard.Padding = new Thickness(18, 15, 18, 18);
            _resultCard.Margin = new Thickness(0, 20, 0, 0);
            _resultCard.Visibility = Visibility.Collapsed;

            StackPanel resultStack = new StackPanel();
            Grid resultHeader = new Grid();
            resultHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            resultHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            resultHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            resultHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock resultLabel = Ui.Label("TRANSLATION", 9.5, Ui.Gold, FontWeights.SemiBold);
            resultLabel.VerticalAlignment = VerticalAlignment.Center;
            resultHeader.Children.Add(resultLabel);

            _langPair = Ui.Label("", 10.5, Ui.Muted, FontWeights.SemiBold);
            _langPair.VerticalAlignment = VerticalAlignment.Center;
            _langPair.Margin = new Thickness(0, 0, 10, 0);
            Grid.SetColumn(_langPair, 1);
            resultHeader.Children.Add(_langPair);

            Button study = Ui.SmallOutlineButton("Study", delegate { StudyCurrent(); });
            study.Margin = new Thickness(0, 0, 6, 0);
            Grid.SetColumn(study, 2);
            resultHeader.Children.Add(study);

            _copyButton = Ui.SmallOutlineButton("Copy", delegate { CopyTranslation(); });
            Grid.SetColumn(_copyButton, 3);
            resultHeader.Children.Add(_copyButton);
            resultStack.Children.Add(resultHeader);

            _translation = Ui.Label("", 22, Ui.Bright, FontWeights.Normal);
            _translation.TextWrapping = TextWrapping.Wrap;
            _translation.LineHeight = 32;
            _translation.Margin = new Thickness(0, 14, 0, 0);
            resultStack.Children.Add(_translation);

            _sourceText = Ui.Label("", 11, Ui.Faint, FontWeights.Normal);
            _sourceText.TextWrapping = TextWrapping.Wrap;
            _sourceText.MaxHeight = 54;
            _sourceText.ClipToBounds = true;
            _sourceText.Margin = new Thickness(0, 13, 0, 0);
            resultStack.Children.Add(_sourceText);

            _resultCard.Child = resultStack;
            stack.Children.Add(_resultCard);

            _tashkeelCard = new Border();
            _tashkeelCard.Background = Ui.GoldWash;
            _tashkeelCard.CornerRadius = new CornerRadius(12);
            _tashkeelCard.Padding = new Thickness(18, 14, 18, 17);
            _tashkeelCard.Margin = new Thickness(0, 12, 0, 0);
            _tashkeelCard.Visibility = Visibility.Collapsed;

            StackPanel tashkeelStack = new StackPanel();
            Grid tashkeelHeader = new Grid();
            tashkeelHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tashkeelHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tashkeelHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _tashkeelTitle = Ui.Label("TASHKEEL", 9.5, Ui.Gold, FontWeights.SemiBold);
            _tashkeelTitle.VerticalAlignment = VerticalAlignment.Center;
            tashkeelHeader.Children.Add(_tashkeelTitle);

            Button listenTashkeel = Ui.SmallOutlineButton("Listen", delegate { SpeakTashkeel(); });
            listenTashkeel.Margin = new Thickness(0, 0, 6, 0);
            Grid.SetColumn(listenTashkeel, 1);
            tashkeelHeader.Children.Add(listenTashkeel);

            Button copyTashkeel = Ui.SmallOutlineButton("Copy", delegate { CopyTashkeel(); });
            Grid.SetColumn(copyTashkeel, 2);
            tashkeelHeader.Children.Add(copyTashkeel);
            tashkeelStack.Children.Add(tashkeelHeader);

            _tashkeelText = Ui.Label("", 20, Ui.TashText, FontWeights.Normal);
            _tashkeelText.TextWrapping = TextWrapping.Wrap;
            _tashkeelText.LineHeight = 34;
            _tashkeelText.FlowDirection = FlowDirection.RightToLeft;
            _tashkeelText.TextAlignment = TextAlignment.Right;
            _tashkeelText.Margin = new Thickness(0, 8, 0, 0);
            tashkeelStack.Children.Add(_tashkeelText);

            _tashkeelCard.Child = tashkeelStack;
            stack.Children.Add(_tashkeelCard);

            _grammarCard = new Border();
            _grammarCard.Background = Ui.Panel;
            _grammarCard.BorderBrush = Ui.Line;
            _grammarCard.BorderThickness = new Thickness(1);
            _grammarCard.CornerRadius = new CornerRadius(12);
            _grammarCard.Padding = new Thickness(18, 14, 18, 16);
            _grammarCard.Margin = new Thickness(0, 12, 0, 0);
            _grammarCard.Visibility = Visibility.Collapsed;

            StackPanel grammarStack = new StackPanel();
            _grammarTitle = Ui.Label("GRAMMAR", 9.5, Ui.Gold, FontWeights.SemiBold);
            grammarStack.Children.Add(_grammarTitle);
            _grammarText = Ui.Label("", 12, Ui.Bright, FontWeights.Normal);
            _grammarText.TextWrapping = TextWrapping.Wrap;
            _grammarText.LineHeight = 20;
            _grammarText.Margin = new Thickness(0, 7, 0, 0);
            grammarStack.Children.Add(_grammarText);
            TextBlock grammarCredit = Ui.Label("Grammar: Wiktionary via Kaikki  \u00b7  CC BY-SA", 8.5, Ui.Faint, FontWeights.Normal);
            grammarCredit.Margin = new Thickness(0, 7, 0, 0);
            grammarStack.Children.Add(grammarCredit);
            _grammarCard.Child = grammarStack;
            stack.Children.Add(_grammarCard);

            TextBlock hint = Ui.Label(
                "Highlight, then Ctrl + C + C anywhere  \u00b7  Ctrl + Enter to Translate",
                10, Ui.Faint, FontWeights.Normal);
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 22, 0, 0);
            stack.Children.Add(hint);

            StackPanel actionRow = new StackPanel();
            actionRow.Orientation = Orientation.Horizontal;
            actionRow.HorizontalAlignment = HorizontalAlignment.Left;
            actionRow.Margin = new Thickness(0, 10, 0, 0);

            Button report = Ui.SmallOutlineButton("Report a bug", delegate
            {
                BugReportWindow.ShowReport(_config);
            });
            report.Margin = new Thickness(0, 0, 8, 0);
            actionRow.Children.Add(report);

            Button checkUpdates = Ui.SmallOutlineButton("Check for updates", delegate
            {
                UpdateService.CheckAndDownload(_config, true);
            });
            actionRow.Children.Add(checkUpdates);

            stack.Children.Add(actionRow);

            ScrollViewer scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Content = shell;
            Content = scroll;

            UpdatePlaceholder();
            UpdateTargetButton();
            UpdateProviderButton();
            UpdateThemeButton();
            Loaded += delegate { FocusInput(); };
        }

        public void ShowFromTray()
        {
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            try
            {
                Topmost = true;
                Topmost = false;
            }
            catch { }
            FocusInput();
        }

        public void RefreshTarget()
        {
            UpdateTargetButton();
            UpdateProviderButton();
        }

        public void AllowClose()
        {
            _allowClose = true;
            try { _requestId++; }
            catch { }
        }

        private void FocusInput()
        {
            if (_input == null) return;
            _input.Focus();
            _input.CaretIndex = _input.Text.Length;
        }

        private void UpdatePlaceholder()
        {
            if (_placeholder == null) return;
            _placeholder.Visibility = string.IsNullOrEmpty(_input.Text)
                ? Visibility.Visible : Visibility.Collapsed;
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
            auto.Click += delegate { SetTarget(""); };
            menu.Items.Add(auto);
            menu.Items.Add(new Separator());

            for (int i = 0; i < LanguageCatalog.Count; i++)
            {
                string code = LanguageCatalog.Code(i);
                string name = LanguageCatalog.Name(i);
                MenuItem item = new MenuItem();
                item.Header = name + "  (" + code + ")";
                item.Tag = code;
                item.Click += delegate { SetTarget(code); };
                menu.Items.Add(item);
            }

            menu.Opened += delegate
            {
                foreach (object entry in menu.Items)
                {
                    MenuItem item = entry as MenuItem;
                    if (item == null || item.Tag == null) continue;
                    string code = (string)item.Tag;
                    item.IsChecked = code.Length == 0
                        ? _config.AutoTarget
                        : (!_config.AutoTarget && string.Equals(_config.Target, code, StringComparison.OrdinalIgnoreCase));
                }
            };
            return menu;
        }

        private void OpenTargetMenu()
        {
            _targetMenu.PlacementTarget = _targetButton;
            _targetMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            _targetMenu.IsOpen = true;
        }

        private ContextMenu BuildProviderMenu()
        {
            ContextMenu menu = new ContextMenu();
            menu.Background = Ui.Menu;
            menu.Foreground = Ui.Bright;
            menu.BorderBrush = Ui.InputBorder;

            string[,] modes = new string[,]
            {
                { "auto", "Auto (local slang + online)" },
                { "online", "Online only" },
                { "offline", "Offline only (Ollama)" }
            };
            for (int i = 0; i < modes.GetLength(0); i++)
            {
                string mode = modes[i, 0];
                MenuItem item = new MenuItem();
                item.Header = modes[i, 1];
                item.Tag = mode;
                item.Click += delegate { SetTranslationMode(mode); };
                menu.Items.Add(item);
            }
            menu.Opened += delegate
            {
                foreach (object entry in menu.Items)
                {
                    MenuItem item = entry as MenuItem;
                    if (item == null) continue;
                    item.IsChecked = string.Equals((string)item.Tag, _config.TranslationMode,
                        StringComparison.OrdinalIgnoreCase);
                }
            };
            return menu;
        }

        private void OpenProviderMenu()
        {
            _providerMenu.PlacementTarget = _providerButton;
            _providerMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            _providerMenu.IsOpen = true;
        }

        private ContextMenu BuildThemeMenu()
        {
            ContextMenu menu = new ContextMenu();
            menu.Background = Ui.Menu;
            menu.Foreground = Ui.Bright;
            menu.BorderBrush = Ui.InputBorder;

            string[,] themes = new string[,]
            {
                { "system", "System (follow Windows)" },
                { "light", "Light" },
                { "sepia", "Sepia" },
                { "dark", "Dark" }
            };
            for (int i = 0; i < themes.GetLength(0); i++)
            {
                string mode = themes[i, 0];
                MenuItem item = new MenuItem();
                item.Header = themes[i, 1];
                item.Tag = mode;
                item.Click += delegate { SetTheme(mode); };
                menu.Items.Add(item);
            }
            menu.Opened += delegate
            {
                foreach (object entry in menu.Items)
                {
                    MenuItem item = entry as MenuItem;
                    if (item == null) continue;
                    item.IsChecked = string.Equals((string)item.Tag, _config.Theme,
                        StringComparison.OrdinalIgnoreCase);
                }
            };
            return menu;
        }

        private void OpenThemeMenu()
        {
            _themeMenu.PlacementTarget = _themeButton;
            _themeMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            _themeMenu.IsOpen = true;
        }

        private void SetTheme(string mode)
        {
            _config.Theme = ThemeManager.Normalize(mode);
            _config.Save();
            ThemeManager.Apply(_config.Theme);
            UpdateThemeButton();
            _status.Text = "Theme set to " + ThemeManager.DisplayName(_config.Theme) + ".";
            _status.Foreground = Ui.Muted;
        }

        private void UpdateThemeButton()
        {
            if (_themeButton == null) return;
            _themeButton.Content = ThemeManager.DisplayName(_config.Theme) + "  \u25be";
            _themeButton.ToolTip = string.Equals(_config.Theme, ThemeManager.SystemTheme, StringComparison.OrdinalIgnoreCase)
                ? "Theme: System follows the Windows app theme"
                : "Theme: " + ThemeManager.DisplayName(_config.Theme);
        }

        private void SetTranslationMode(string mode)
        {
            _config.TranslationMode = TranslationService.NormalizeMode(mode);
            _config.Save();
            _translator.ConfigureProvider(_config.LocalModelEnabled, _config.LocalModelEndpoint,
                _config.LocalModelName, _config.TranslationMode);
            UpdateProviderButton();
            _status.Text = _config.TranslationMode == "offline"
                ? "Offline mode: translations stay on this device."
                : (_config.TranslationMode == "online" ? "Online mode selected." : "Auto mode selected.");
            _status.Foreground = Ui.Muted;
        }

        private void UpdateProviderButton()
        {
            string label = _config.TranslationMode == "offline" ? "Offline"
                : (_config.TranslationMode == "online" ? "Online" : "Auto");
            _providerButton.Content = label + "  \u25be";
        }

        private void SetTarget(string code)
        {
            _config.AutoTarget = string.IsNullOrEmpty(code);
            if (!string.IsNullOrEmpty(code)) _config.Target = code;
            _config.Save();
            UpdateTargetButton();
        }

        private void UpdateTargetButton()
        {
            string label;
            if (_config.AutoTarget) label = "Auto  \u21c4  Arabic/English";
            else label = LanguageName(_config.Target);
            _targetButton.Content = label + "  \u25be";
        }

        private string ResolveTarget(string text)
        {
            if (_config.AutoTarget)
                return ArabicText.ContainsArabic(text) ? "en" : "ar";
            return string.IsNullOrEmpty(_config.Target) ? "ar" : _config.Target;
        }

        private async void ImproveWording()
        {
            if (_busy) return;
            string text = _input.Text == null ? "" : _input.Text.Trim();
            if (text.Length == 0)
            {
                _status.Text = "Type or paste text first.";
                _status.Foreground = Ui.WarnBrush;
                _input.Focus();
                return;
            }

            _wordingButton.IsEnabled = false;
            _status.Text = "Looking for more natural wording\\u2026";
            _status.Foreground = Ui.WarnBrush;
            try
            {
                string language = Colloquial.DetectLanguage(text);
                if (string.IsNullOrEmpty(language))
                    language = ArabicText.ContainsArabic(text) ? "ar" : "en";
                string context = _currentResult != null &&
                    string.Equals(_currentResult.SourceText, text, StringComparison.Ordinal)
                    ? _currentResult.Translation : "";
                TranslationOutcome outcome = await _translator.SuggestWordingAsync(text, language, context);
                if (outcome == null || string.IsNullOrEmpty(outcome.Text))
                {
                    _status.Text = "No wording suggestion available. Start Ollama and install a local model, or use Translate.";
                    _status.Foreground = Ui.WarnBrush;
                    return;
                }
                _wordingText.Text = outcome.Text;
                _wordingCard.Visibility = Visibility.Visible;
                _status.Text = "Suggested more natural wording.";
                _status.Foreground = Ui.Muted;
            }
            catch (Exception ex)
            {
                Logger.Write("improve wording: " + ex.Message);
                _status.Text = "Wording suggestion failed.";
                _status.Foreground = Ui.ErrorBrush;
            }
            finally
            {
                _wordingButton.IsEnabled = true;
            }
        }

        private void UseWording()
        {
            if (string.IsNullOrEmpty(_wordingText.Text)) return;
            _input.Text = _wordingText.Text;
            _wordingCard.Visibility = Visibility.Collapsed;
            FocusInput();
        }

        private async void Translate()
        {
            if (_busy) return;
            string text = _input.Text == null ? "" : _input.Text.Trim();
            if (text.Length == 0)
            {
                _status.Text = "Type or paste text first.";
                _status.Foreground = Ui.WarnBrush;
                _input.Focus();
                return;
            }

            bool truncated = false;
            if (text.Length > _config.MaxChars)
            {
                text = text.Substring(0, _config.MaxChars);
                truncated = true;
            }

            int request = ++_requestId;
            _busy = true;
            _translateButton.IsEnabled = false;
            _status.Text = "Translating\u2026";
            _status.Foreground = Ui.WarnBrush;
            _tashkeelCard.Visibility = Visibility.Collapsed;
            _grammarCard.Visibility = Visibility.Collapsed;
            string target = ResolveTarget(text);
            _langPair.Text = (ArabicText.ContainsArabic(text) ? "AR" : "\u2026") + "  \u2192  " + target.ToUpperInvariant();
            bool offlineMode = string.Equals(_config.TranslationMode, "offline", StringComparison.OrdinalIgnoreCase);

            try
            {
                TranslationOutcome outcome = await _translator.TranslateAsync(text, target);
                if (request != _requestId) return;

                TranslationResult result = new TranslationResult();
                result.SourceText = text;
                result.Translation = outcome == null ? "" : outcome.Text;
                result.Detected = outcome == null ? null : outcome.Detected;
                result.Engine = outcome == null ? null : outcome.Engine;
                result.Target = target;
                result.AutoTarget = _config.AutoTarget;
                result.MaxChars = _config.MaxChars;
                result.Truncated = truncated;
                _currentResult = result;
                _lastTranslation = result.Translation ?? "";
                if (_onResult != null) _onResult(result);

                ShowResult(result);
                if (!string.IsNullOrEmpty(result.Translation))
                {
                    HistoryStore.Add(ToHistoryEntry(result));
                    _status.Text = truncated ? "Translated (first " + _config.MaxChars + " characters)" :
                        (result.Engine == "phrasebook" ? "Translated \u00b7 colloquial meaning" :
                        (result.Engine == "local" ? "Translated \u00b7 slang register preserved on this device" :
                        (result.Engine == "offline" ? "Translated \u00b7 offline on this device" : "Translated")));
                    _status.Foreground = Ui.Muted;
                    Task<GrammarInfo> grammarTask = IsSingleWord(text) && !string.IsNullOrEmpty(result.Detected)
                        ? _grammar.LookupAsync(text, result.Detected, !offlineMode) : null;
                    await ShowTranscriptionAsync(text, result, request);
                    if (grammarTask != null)
                    {
                        GrammarInfo grammar = await grammarTask;
                        if (request == _requestId)
                        {
                            if (grammar != null) ShowGrammar(grammar);
                            else if (offlineMode) ShowCachedGrammar(text, request);
                        }
                    }
                    else if (offlineMode)
                    {
                        ShowCachedGrammar(text, request);
                    }
                }
                else
                {
                    _status.Text = "The service returned no translation.";
                    _status.Foreground = Ui.WarnBrush;
                    ShowCachedGrammar(text, request);
                }
            }
            catch (Exception ex)
            {
                if (request != _requestId) return;
                Logger.Write("main translate: " + ex.Message);
                _status.Text = FriendlyError(ex);
                _status.Foreground = Ui.ErrorBrush;
                ShowCachedGrammar(text, request);
            }
            finally
            {
                if (request == _requestId)
                {
                    _busy = false;
                    _translateButton.IsEnabled = true;
                }
            }
        }

        private void ShowResult(TranslationResult result)
        {
            _resultCard.Visibility = Visibility.Visible;
            _translation.Text = result.Translation ?? "";
            _sourceText.Text = result.SourceText ?? "";
            _langPair.Text = (string.IsNullOrEmpty(result.Detected) ? "?" : result.Detected.ToUpperInvariant())
                + "  \u2192  " + (result.Target == null ? "?" : result.Target.ToUpperInvariant());
            bool rtl = IsRtl(result.Target);
            _translation.FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            _translation.TextAlignment = rtl ? TextAlignment.Right : TextAlignment.Left;
            _translation.FontFamily = rtl ? new FontFamily("Segoe UI") : new FontFamily("Segoe UI");
            _copyButton.IsEnabled = !string.IsNullOrEmpty(result.Translation);
        }

        private void ShowCachedGrammar(string text, int request)
        {
            if (!IsSingleWord(text)) return;
            GrammarInfo cached = _grammar.LookupCached(text);
            if (cached != null && request == _requestId) ShowGrammar(cached);
        }

        private void ShowGrammar(GrammarInfo info)
        {
            if (info == null || !info.HasContent)
            {
                _grammarCard.Visibility = Visibility.Collapsed;
                return;
            }
            string title = "GRAMMAR  \u00b7  " + info.Language.ToUpperInvariant();
            if (!string.IsNullOrEmpty(info.PartOfSpeech)) title += "  \u00b7  " + info.PartOfSpeech.ToUpperInvariant();
            if (info.FromCache) title += "  \u00b7  CACHED";
            _grammarTitle.Text = title;
            // The card mixes English labels with Arabic forms; keep it LTR so the
            // bullets and label order stay stable.
            _grammarText.Text = GrammarText.Summary(info);
            _grammarText.FlowDirection = FlowDirection.LeftToRight;
            _grammarText.TextAlignment = TextAlignment.Left;
            _grammarCard.Visibility = Visibility.Visible;
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

        private async Task ShowTranscriptionAsync(string source, TranslationResult result, int request)
        {
            string transcription = null;
            string kind = null;
            string role = null;
            string speech = null;
            string speechLang = null;
            if (ArabicText.ContainsArabic(source) &&
                (string.Equals(result.Detected, "ar", StringComparison.OrdinalIgnoreCase) || result.Detected == null))
            {
                transcription = source;
                kind = "TASHKEEL";
                role = "SOURCE";
                speech = source;
                speechLang = Pronunciation.TtsLanguage(_config.SpokenLahja);
            }
            else if (!string.IsNullOrEmpty(result.Translation) && ArabicText.ContainsArabic(result.Translation))
            {
                transcription = result.Translation;
                kind = "TASHKEEL";
                role = "TRANSLATION";
                speech = result.Translation;
                speechLang = "ar";
            }
            else if (ChineseText.ContainsHan(source) &&
                     (ChineseText.IsChineseLanguage(result.Detected) || result.Detected == null))
            {
                transcription = source;
                kind = "PINYIN";
                role = "SOURCE";
                speech = source;
                speechLang = "zh-CN";
            }
            else if (!string.IsNullOrEmpty(result.Translation) && ChineseText.ContainsHan(result.Translation))
            {
                transcription = result.Translation;
                kind = "PINYIN";
                role = "TRANSLATION";
                speech = result.Translation;
                speechLang = "zh-CN";
            }

            if (string.IsNullOrEmpty(transcription))
            {
                _tashkeelCard.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                string shown = kind == "PINYIN"
                    ? await Task.Run(() => _tashkeel.Pinyin(transcription))
                    : await Task.Run(() => _tashkeel.Diacritize(transcription, _config.SpokenLahja));
                if (request != _requestId) return;
                if (string.IsNullOrEmpty(shown))
                {
                    _tashkeelCard.Visibility = Visibility.Collapsed;
                    return;
                }

                bool rightToLeft = kind == "TASHKEEL";
                _transcriptionKind = kind;
                _transcriptionSpeech = speech;
                _transcriptionSpeechLang = speechLang;
                _tashkeelTitle.Text = kind + "  \u00b7  " + role;
                _tashkeelText.Text = shown;
                _tashkeelText.FontSize = rightToLeft ? 20 : 17;
                _tashkeelText.LineHeight = rightToLeft ? 34 : 28;
                _tashkeelText.FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                _tashkeelText.TextAlignment = rightToLeft ? TextAlignment.Right : TextAlignment.Left;
                _tashkeelCard.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logger.Write("main transcription: " + ex.Message);
                if (request == _requestId) _tashkeelCard.Visibility = Visibility.Collapsed;
            }
        }

        private void StudyCurrent()
        {
            if (_currentResult == null) return;
            int added = StudyStore.Add(ToHistoryEntry(_currentResult));
            _status.Text = added > 0 ? "Added to study." : "Already in study.";
            _status.Foreground = Ui.Muted;
            StudyWindow.ShowWindow();
        }

        private void CopyTranslation()
        {
            try
            {
                if (!string.IsNullOrEmpty(_lastTranslation)) Clipboard.SetText(_lastTranslation);
                _status.Text = "Copied translation.";
                _status.Foreground = Ui.Muted;
            }
            catch (Exception ex)
            {
                Logger.Write("main copy: " + ex.Message);
                _status.Text = "Clipboard is busy; try again.";
                _status.Foreground = Ui.WarnBrush;
            }
        }

        private void CopyTashkeel()
        {
            try
            {
                if (!string.IsNullOrEmpty(_tashkeelText.Text)) Clipboard.SetText(_tashkeelText.Text);
                _status.Text = _transcriptionKind == "PINYIN" ? "Copied Pinyin." : "Copied Tashkeel.";
                _status.Foreground = Ui.Muted;
            }
            catch (Exception ex)
            {
                Logger.Write("main tashkeel copy: " + ex.Message);
                _status.Text = "Clipboard is busy; try again.";
                _status.Foreground = Ui.WarnBrush;
            }
        }

        private async void SpeakTashkeel()
        {
            if (string.IsNullOrEmpty(_tashkeelText.Text)) return;
            try
            {
                _status.Text = "Loading audio\u2026";
                _status.Foreground = Ui.Muted;
                string speech = string.IsNullOrEmpty(_transcriptionSpeech) ? _tashkeelText.Text : _transcriptionSpeech;
                await SpeechService.SpeakAsync(speech, _transcriptionSpeechLang);
                _status.Text = "";
            }
            catch (Exception ex)
            {
                Logger.Write("main tts: " + ex.Message);
                _status.Text = "Audio unavailable.";
                _status.Foreground = Ui.WarnBrush;
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

        private static bool IsRtl(string code)
        {
            return string.Equals(code, "ar", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, "fa", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, "ur", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, "he", StringComparison.OrdinalIgnoreCase);
        }

        private static string LanguageName(string code)
        {
            return string.IsNullOrEmpty(code) ? "Arabic" : LanguageCatalog.NameOf(code);
        }

        private static string FriendlyError(Exception ex)
        {
            if (ex == null) return "Translation failed.";
            string message = ex.Message == null ? "" : ex.Message.Trim();
            if (message.Length > 180) message = message.Substring(0, 177) + "\u2026";
            return message.Length == 0 ? "Translation failed." : message;
        }

        private static ImageSource LoadWindowIcon()
        {
            try
            {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                using (Drawing.Icon icon = Drawing.Icon.ExtractAssociatedIcon(exe))
                {
                    if (icon == null) return null;
                    BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
