$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot 'src\MainWindow.cs'
$text = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)

function Replace-Text([string]$old, [string]$new, [string]$label) {
    $count = ([regex]::Matches($script:text, [regex]::Escape($old))).Count
    if ($count -ne 1) { throw "Expected one '$label' block, found $count" }
    $script:text = $script:text.Replace($old, $new)
}

Replace-Text @'
        private readonly ContextMenu _targetMenu;
        private readonly ContextMenu _providerMenu;
'@ @'
        private readonly ContextMenu _targetMenu;
        private readonly ContextMenu _providerMenu;
        private readonly Button _themeButton;
        private readonly ContextMenu _themeMenu;
'@ 'fields'

Replace-Text '            Background = Ui.MakeBrush(0xFF, 0x0E, 0x12, 0x18);' '            Background = Ui.Window;' 'window background'
Replace-Text '            mark.Background = Ui.MakeBrush(0xFF, 0x18, 0x1E, 0x28);' '            mark.Background = Ui.Mark;' 'mark background'

Replace-Text @'
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
'@ @'
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
'@ 'header columns'

Replace-Text @'
            _targetButton.MinWidth = 146;
            Grid.SetColumn(_targetButton, 3);
            header.Children.Add(_targetButton);

            _targetMenu = BuildTargetMenu();
            _providerMenu = BuildProviderMenu();
            stack.Children.Add(header);
'@ @'
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
'@ 'header theme button'

Replace-Text '            inputBorder.Background = Ui.MakeBrush(0xFF, 0x15, 0x1A, 0x22);' '            inputBorder.Background = Ui.Input;' 'input background'
Replace-Text '            inputBorder.BorderBrush = Ui.MakeBrush(0xFF, 0x2A, 0x34, 0x42);' '            inputBorder.BorderBrush = Ui.InputBorder;' 'input border'
Replace-Text '            _input.SelectionBrush = Ui.MakeBrush(0x55, 0xD9, 0xB9, 0x6A);' '            _input.SelectionBrush = Ui.Selection;' 'selection brush'
Replace-Text '            _tashkeelCard.Background = Ui.MakeBrush(0x18, 0xD9, 0xB9, 0x6A);' '            _tashkeelCard.Background = Ui.GoldWash;' 'tashkeel background'

$oldConstructorEnd = @'
            UpdatePlaceholder();
            UpdateTargetButton();
            UpdateProviderButton();
            Loaded += delegate { FocusInput(); };
'@
$newConstructorEnd = @'
            UpdatePlaceholder();
            UpdateTargetButton();
            UpdateProviderButton();
            UpdateThemeButton();
            Loaded += delegate { FocusInput(); };
'@
Replace-Text $oldConstructorEnd $newConstructorEnd 'constructor end'

$menuOld = @'
            menu.Background = Ui.MakeBrush(0xFF, 0x1A, 0x20, 0x2A);
            menu.Foreground = Ui.Bright;
            menu.BorderBrush = Ui.MakeBrush(0xFF, 0x2A, 0x34, 0x42);
'@
$menuNew = @'
            menu.Background = Ui.Menu;
            menu.Foreground = Ui.Bright;
            menu.BorderBrush = Ui.InputBorder;
'@
$count = ([regex]::Matches($script:text, [regex]::Escape($menuOld))).Count
if ($count -ne 2) { throw "Expected two menu blocks, found $count" }
$script:text = $script:text.Replace($menuOld, $menuNew)

Replace-Text @'
        private void OpenProviderMenu()
        {
            _providerMenu.PlacementTarget = _providerButton;
            _providerMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            _providerMenu.IsOpen = true;
        }

        private void SetTranslationMode(string mode)
'@ @'
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
'@ 'theme methods'

[IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false)))
Write-Host 'MainWindow theme patch applied'
