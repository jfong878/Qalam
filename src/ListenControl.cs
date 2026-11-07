// One Listen button, three actions: play, pause, slow. The label shows what
// the next click will do, so the button never lies about its state.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Qalam
{
    internal sealed class ListenControl
    {
        private enum Mode { Idle, Playing, Paused, SlowPlaying, SlowPaused }

        private static readonly List<WeakReference> All = new List<WeakReference>();
        private readonly Button _button;
        private readonly Func<string> _text;
        private readonly Func<string> _language;
        private Mode _mode = Mode.Idle;
        private bool _busy;
        private bool _loading;
        private int _generation;

        public ListenControl(Button button, Func<string> text, Func<string> language)
        {
            _button = button;
            _text = text;
            _language = language;
            _button.Click += OnClick;
            _button.MouseEnter += delegate { Prewarm(); };
            _button.GotKeyboardFocus += delegate { Prewarm(); };
            _button.ToolTip = "Play · pause · play slowly (0.65×)";
            lock (All) All.Add(new WeakReference(this));
            Update();
        }

        public static void ResetAll()
        {
            SpeechService.Stop();
            foreach (ListenControl control in Live())
                control.Reset();
        }

        public static void NotifyEnded()
        {
            foreach (ListenControl control in Live())
                if (control._mode == Mode.Playing || control._mode == Mode.SlowPlaying)
                    control.Reset();
        }

        // A word click can start speech without touching the button. Mark the
        // control that owns this passage as playing and put the others back.
        public static void NotifyStarted()
        {
            foreach (ListenControl control in Live())
            {
                string text = control._text == null ? "" : (control._text() ?? "");
                string language = control._language == null ? "" : (control._language() ?? "");
                if (SpeechService.Matches(text, language))
                    control.MarkPlaying();
                else if (control._mode == Mode.Playing || control._mode == Mode.SlowPlaying)
                    control.Reset();
            }
        }

        private void MarkPlaying()
        {
            _mode = SpeechService.Speed < 0.99 ? Mode.SlowPlaying : Mode.Playing;
            Update();
        }

        private static List<ListenControl> Live()
        {
            List<ListenControl> live = new List<ListenControl>();
            lock (All)
            {
                for (int i = All.Count - 1; i >= 0; i--)
                {
                    ListenControl control = All[i].Target as ListenControl;
                    if (control == null) All.RemoveAt(i);
                    else live.Add(control);
                }
            }
            return live;
        }

        private void Reset()
        {
            _generation++;
            _busy = false;
            _loading = false;
            _mode = Mode.Idle;
            Update();
        }

        private void Prewarm()
        {
            if (_busy || _mode != Mode.Idle) return;
            string text = _text == null ? "" : (_text() ?? "");
            string language = _language == null ? "" : (_language() ?? "");
            if (text.Length == 0 || language.Length == 0) return;
            SpeechService.Prewarm(text, language);
        }

        private async void OnClick(object sender, System.Windows.RoutedEventArgs e)
        {
            string text = _text == null ? "" : (_text() ?? "");
            string language = _language == null ? "" : (_language() ?? "");
            if (text.Length == 0 || language.Length == 0) return;
            if (_busy)
            {
                // The first chunk can take a moment; let a second click cancel
                // instead of silently ignoring it.
                if (_loading)
                {
                    _generation++;
                    _busy = false;
                    _loading = false;
                    SpeechService.Stop();
                    _mode = Mode.Idle;
                    Update();
                }
                return;
            }

            _busy = true;
            _loading = false;
            int generation = ++_generation;
            if (!SpeechService.Matches(text, language) && _mode != Mode.Idle)
                _mode = Mode.Idle;

            try
            {
                switch (_mode)
                {
                    case Mode.Idle:
                        // The first chunk is usually ready in a fraction of a
                        // second; show that the request is alive while it lands.
                        _loading = true;
                        _button.Content = "Cancel";
                        await SpeechService.PlayAsync(text, language, 1.0, true);
                        if (generation != _generation) return;
                        _loading = false;
                        _mode = Mode.Playing;
                        break;
                    case Mode.Playing:
                        SpeechService.Pause();
                        _mode = Mode.Paused;
                        break;
                    case Mode.Paused:
                        await SpeechService.PlayAsync(text, language, 0.65, false);
                        if (generation != _generation) return;
                        _mode = Mode.SlowPlaying;
                        break;
                    case Mode.SlowPlaying:
                        SpeechService.Pause();
                        _mode = Mode.SlowPaused;
                        break;
                    case Mode.SlowPaused:
                        await SpeechService.PlayAsync(text, language, 1.0, false);
                        if (generation != _generation) return;
                        _mode = Mode.Playing;
                        break;
                }
                if (generation != _generation) return;
            }
            catch (Exception ex)
            {
                Logger.Write("listen: " + ex.Message);
                if (generation == _generation) _mode = Mode.Idle;
            }
            finally
            {
                if (generation == _generation)
                {
                    _busy = false;
                    _loading = false;
                    Update();
                }
            }
        }

        private void Update()
        {
            switch (_mode)
            {
                case Mode.Playing: _button.Content = "Pause"; break;
                case Mode.Paused: _button.Content = "Slow"; break;
                case Mode.SlowPlaying: _button.Content = "Pause"; break;
                case Mode.SlowPaused: _button.Content = "Play"; break;
                default: _button.Content = "Listen"; break;
            }
        }
    }

    // Renders a passage as ordinary text whose words are individually
    // clickable. Clicking a word starts the voice at that word.
    internal static class SpokenText
    {
        public static void Render(TextBlock block, string text, string language)
        {
            if (block == null) return;
            block.Inlines.Clear();
            if (string.IsNullOrEmpty(text))
            {
                block.Text = "";
                return;
            }

            System.Text.RegularExpressions.MatchCollection words =
                System.Text.RegularExpressions.Regex.Matches(text, @"\S+");
            int total = words.Count;
            int cursor = 0;
            for (int i = 0; i < total; i++)
            {
                System.Text.RegularExpressions.Match word = words[i];
                if (word.Index > cursor)
                    block.Inlines.Add(new Run(text.Substring(cursor, word.Index - cursor)));

                int index = i;
                Hyperlink link = new Hyperlink(new Run(word.Value));
                link.Foreground = Ui.Bright;
                link.TextDecorations = null;
                link.Cursor = Cursors.Hand;
                link.Focusable = false;
                link.ToolTip = "Play from this word";
                link.Click += delegate { SpeechService.SeekToWord(text, language, index, total); };
                block.Inlines.Add(link);
                cursor = word.Index + word.Length;
            }
            if (cursor < text.Length)
                block.Inlines.Add(new Run(text.Substring(cursor)));
        }
    }
}
