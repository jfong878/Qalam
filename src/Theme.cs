using System;
using System.Windows.Media;
using Microsoft.Win32;

namespace Qalam
{
    internal sealed class ThemePalette
    {
        public Color Window;
        public Color Mark;
        public Color Input;
        public Color InputBorder;
        public Color Menu;
        public Color Deep;
        public Color Popup;
        public Color PopupLine;
        public Color Bright;
        public Color Muted;
        public Color Faint;
        public Color Gold;
        public Color Accent;
        public Color Line;
        public Color Panel;
        public Color TashText;
        public Color Error;
        public Color Warn;
        public Color GoldWash;
        public Color Selection;
        public Color HoverWash;
        public Color Outline;
        public Color OutlineHover;
        public Color Primary;
        public Color PrimaryHover;
        public Color PrimaryText;
        public Color SelectedRow;
        public Color StrokeHint;
        public Color Canvas;
    }

    internal static class ThemeManager
    {
        public const string SystemTheme = "system";

        // Raised on a Windows preference thread. Subscribers must marshal to
        // their UI thread before changing WPF brushes.
        public static event EventHandler SystemThemeChanged;

        private static bool _watching;
        private static int _lastSystemDark = -1;

        public static string Normalize(string value)
        {
            string mode = (value ?? "").Trim().ToLowerInvariant();
            if (mode == "light" || mode == "sepia" || mode == "dark") return mode;
            return SystemTheme;
        }

        public static string DisplayName(string value)
        {
            string mode = Normalize(value);
            if (mode == "light") return "Light";
            if (mode == "sepia") return "Sepia";
            if (mode == "dark") return "Dark";
            return "System";
        }

        public static string Resolve(string value)
        {
            string mode = Normalize(value);
            if (mode == SystemTheme) return IsSystemDark() ? "dark" : "light";
            return mode;
        }

        public static void Apply(string value)
        {
            Ui.ApplyPalette(PaletteFor(Resolve(value)));
        }

        public static bool IsSystemDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object raw = key == null ? null : key.GetValue("AppsUseLightTheme");
                    if (raw is int) return (int)raw == 0;
                    if (raw is long) return (long)raw == 0;
                }
            }
            catch { }
            // Windows defaults to the light app theme when the preference is
            // missing or unreadable.
            return false;
        }

        public static void WatchSystem()
        {
            if (_watching) return;
            try
            {
                _lastSystemDark = IsSystemDark() ? 1 : 0;
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
                _watching = true;
            }
            catch (Exception ex)
            {
                Logger.Write("theme watch: " + ex.Message);
            }
        }

        public static void UnwatchSystem()
        {
            if (!_watching) return;
            try { SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged; }
            catch { }
            _watching = false;
        }

        private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            try
            {
                int dark = IsSystemDark() ? 1 : 0;
                if (dark == _lastSystemDark) return;
                _lastSystemDark = dark;
                EventHandler handler = SystemThemeChanged;
                if (handler != null) handler(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.Write("theme change: " + ex.Message);
            }
        }

        private static Color C(uint argb)
        {
            return Color.FromArgb(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF));
        }

        private static ThemePalette PaletteFor(string mode)
        {
            ThemePalette p = new ThemePalette();
            if (mode == "light")
            {
                p.Window = C(0xFFF4F1E8);
                p.Mark = C(0xFF102B32);
                p.Input = C(0xFFFFFDF8);
                p.InputBorder = C(0xFFD8D5C9);
                p.Menu = C(0xFFFFFDF8);
                p.Deep = C(0xFFECE7DB);
                p.Popup = C(0xFFFFFDF8);
                p.PopupLine = C(0x33000000);
                p.Bright = C(0xFF1B2424);
                p.Muted = C(0xFF5B6668);
                p.Faint = C(0xFF7F8A89);
                p.Gold = C(0xFF9A6A28);
                p.Accent = C(0xFFB58532);
                p.Line = C(0xFFE0DCCF);
                p.Panel = C(0xFFFFFCF4);
                p.TashText = C(0xFF4A3520);
                p.Error = C(0xFFB44738);
                p.Warn = C(0xFFA66A1F);
                p.GoldWash = C(0x22B58532);
                p.Selection = C(0x55B58532);
                p.HoverWash = C(0x14000000);
                p.Outline = C(0x55B58532);
                p.OutlineHover = C(0x1FB58532);
                p.Primary = C(0xFFC9A14E);
                p.PrimaryHover = C(0xFFD9B365);
                p.PrimaryText = C(0xFF141820);
                p.SelectedRow = C(0x22B58532);
                p.StrokeHint = C(0x1B1B2424);
                p.Canvas = C(0xFFECE7DB);
            }
            else if (mode == "sepia")
            {
                p.Window = C(0xFFEFE2CB);
                p.Mark = C(0xFF28473D);
                p.Input = C(0xFFFBF2DF);
                p.InputBorder = C(0xFFD2BE9D);
                p.Menu = C(0xFFFBF2DF);
                p.Deep = C(0xFFE3D2B5);
                p.Popup = C(0xFFFBF2DF);
                p.PopupLine = C(0x33000000);
                p.Bright = C(0xFF3A2B1D);
                p.Muted = C(0xFF6F5B45);
                p.Faint = C(0xFF8C765C);
                p.Gold = C(0xFF8A5B24);
                p.Accent = C(0xFFA8742E);
                p.Line = C(0xFFD8C4A4);
                p.Panel = C(0xFFFFF8EA);
                p.TashText = C(0xFF5A3C1D);
                p.Error = C(0xFFA84936);
                p.Warn = C(0xFF92601E);
                p.GoldWash = C(0x22A8742E);
                p.Selection = C(0x55A8742E);
                p.HoverWash = C(0x14000000);
                p.Outline = C(0x55A8742E);
                p.OutlineHover = C(0x1FA8742E);
                p.Primary = C(0xFFD1A34E);
                p.PrimaryHover = C(0xFFE0B866);
                p.PrimaryText = C(0xFF2A1D10);
                p.SelectedRow = C(0x22A8742E);
                p.StrokeHint = C(0x1B3A2B1D);
                p.Canvas = C(0xFFE3D2B5);
            }
            else
            {
                p.Window = C(0xFF0E1218);
                p.Mark = C(0xFF181E28);
                p.Input = C(0xFF151A22);
                p.InputBorder = C(0xFF2A3442);
                p.Menu = C(0xFF1A202A);
                p.Deep = C(0xFF0B0F16);
                p.Popup = C(0xF80E121A);
                p.PopupLine = C(0x26FFFFFF);
                p.Bright = C(0xFFF2F4F8);
                p.Muted = C(0xFF929CAE);
                p.Faint = C(0xFF636C7C);
                p.Gold = C(0xFFD9B96A);
                p.Accent = C(0xFFE8C878);
                p.Line = C(0x1AFFFFFF);
                p.Panel = C(0x12FFFFFF);
                p.TashText = C(0xFFF0E2C2);
                p.Error = C(0xFFFF8A80);
                p.Warn = C(0xFFE0B060);
                p.GoldWash = C(0x18D9B96A);
                p.Selection = C(0x55D9B96A);
                p.HoverWash = C(0x1FFFFFFF);
                p.Outline = C(0x33D9B96A);
                p.OutlineHover = C(0x1AD9B96A);
                p.Primary = C(0xFFE8C878);
                p.PrimaryHover = C(0xFFF2D992);
                p.PrimaryText = C(0xFF141820);
                p.SelectedRow = C(0x1FD9B96A);
                p.StrokeHint = C(0x1BF2F4F8);
                p.Canvas = C(0xFF0E121A);
            }
            return p;
        }
    }
}
