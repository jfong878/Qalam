$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot 'src\MainWindow.cs'
$text = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)

function Replace-Text([string]$old, [string]$new, [string]$label) {
    $count = ([regex]::Matches($script:text, [regex]::Escape($old))).Count
    if ($count -ne 1) { throw "Expected one '$label', found $count" }
    $script:text = $script:text.Replace($old, $new)
}

Replace-Text @'
        private readonly Button _themeButton;
        private readonly ContextMenu _themeMenu;
'@ @'
        private readonly Button _themeButton;
        private readonly ContextMenu _themeMenu;
        private readonly Button _wordingButton;
        private readonly Border _wordingCard;
        private readonly TextBlock _wordingText;
'@ 'fields'

Replace-Text @'
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
'@ @'
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
'@ 'actions columns'

Replace-Text @'
            characters.Margin = new Thickness(9, 0, 0, 0);
            Grid.SetColumn(characters, 3);
            actions.Children.Add(characters);

            _status = Ui.Label("", 11, Ui.Muted, FontWeights.Normal);
'@ @'
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
'@ 'wording button'

Replace-Text '            Grid.SetColumn(_status, 4);' '            Grid.SetColumn(_status, 5);' 'status column'

Replace-Text @'
                _resultCard.Visibility = Visibility.Collapsed;
                _tashkeelCard.Visibility = Visibility.Collapsed;
                _grammarCard.Visibility = Visibility.Collapsed;
                _status.Text = "";
'@ @'
                _resultCard.Visibility = Visibility.Collapsed;
                _tashkeelCard.Visibility = Visibility.Collapsed;
                _grammarCard.Visibility = Visibility.Collapsed;
                _wordingCard.Visibility = Visibility.Collapsed;
                _status.Text = "";
'@ 'clear wording'

Replace-Text '            _input.TextChanged += delegate { UpdatePlaceholder(); };' @'
            _input.TextChanged += delegate
            {
                UpdatePlaceholder();
                if (_wordingCard != null) _wordingCard.Visibility = Visibility.Collapsed;
            };
'@ 'text changed'

Replace-Text '            stack.Children.Add(actions);' @'
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
'@ 'wording card'

Replace-Text @'
        private async void Translate()
'@ @'
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
'@ 'wording methods'

[IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false)))
Write-Host 'MainWindow wording patch applied'
