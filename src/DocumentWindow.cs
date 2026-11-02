// Small non-modal progress window for document translation.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Qalam
{
    internal sealed class DocumentProgressWindow : Window
    {
        private readonly TextBlock _status;
        private readonly TextBlock _detail;
        private readonly ProgressBar _bar;
        private readonly Button _cancel;
        private bool _allowClose;
        private bool _cancelRequested;

        public event Action CancelRequested;

        public DocumentProgressWindow(string fileName, string targetName)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.Height;
            Width = 470;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            Border root = new Border();
            root.Background = Ui.Menu;
            root.BorderBrush = Ui.Outline;
            root.BorderThickness = new Thickness(1);
            root.CornerRadius = new CornerRadius(14);
            root.Padding = new Thickness(22, 18, 22, 20);
            Content = root;

            StackPanel stack = new StackPanel();

            DockPanel header = new DockPanel();
            TextBlock brand = Ui.Label("QALAM", 10.5, Ui.Muted, FontWeights.SemiBold);
            brand.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(brand, Dock.Left);
            header.Children.Add(brand);

            TextBlock kind = Ui.Label("DOCUMENT", 10.5, Ui.Gold, FontWeights.SemiBold);
            kind.HorizontalAlignment = HorizontalAlignment.Right;
            kind.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(kind);
            stack.Children.Add(header);

            TextBlock title = Ui.Label("Translating document", 19, Ui.Bright, FontWeights.SemiBold);
            title.Margin = new Thickness(0, 13, 0, 0);
            stack.Children.Add(title);

            TextBlock file = Ui.Label(fileName, 11.5, Ui.Faint, FontWeights.Normal);
            file.TextTrimming = TextTrimming.CharacterEllipsis;
            file.Margin = new Thickness(0, 3, 0, 0);
            stack.Children.Add(file);

            _bar = new ProgressBar();
            _bar.Height = 5;
            _bar.Minimum = 0;
            _bar.Maximum = 100;
            _bar.Value = 0;
            _bar.Foreground = Ui.Gold;
            _bar.Background = Ui.Panel;
            _bar.BorderThickness = new Thickness(0);
            _bar.Margin = new Thickness(0, 18, 0, 10);
            stack.Children.Add(_bar);

            _status = Ui.Label("Preparing document", 12.5, Ui.Bright, FontWeights.SemiBold);
            _status.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(_status);

            _detail = Ui.Label("Translating to " + targetName, 11, Ui.Muted, FontWeights.Normal);
            _detail.TextWrapping = TextWrapping.Wrap;
            _detail.Margin = new Thickness(0, 3, 0, 0);
            stack.Children.Add(_detail);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.HorizontalAlignment = HorizontalAlignment.Right;
            actions.Margin = new Thickness(0, 15, 0, 0);

            _cancel = Ui.SmallOutlineButton("Cancel", delegate
            {
                if (_cancelRequested) return;
                _cancelRequested = true;
                _cancel.IsEnabled = false;
                _status.Text = "Cancelling\u2026";
                Action handler = CancelRequested;
                if (handler != null) handler();
            });
            actions.Children.Add(_cancel);
            stack.Children.Add(actions);

            root.Child = stack;
        }

        public void UpdateProgress(DocumentProgress progress)
        {
            if (progress == null) return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(delegate { UpdateProgress(progress); }), DispatcherPriority.Background);
                return;
            }

            _bar.Value = progress.Percent;
            if (!string.IsNullOrWhiteSpace(progress.Stage)) _status.Text = progress.Stage;
            string detail = progress.Detail ?? "";
            if (progress.Total > 0) detail += (detail.Length > 0 ? "  \u2022  " : "") + progress.Percent + "%";
            _detail.Text = detail;
        }

        public void AllowClose()
        {
            _allowClose = true;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                if (!_cancelRequested)
                {
                    _cancelRequested = true;
                    _cancel.IsEnabled = false;
                    _status.Text = "Cancelling\u2026";
                    Action handler = CancelRequested;
                    if (handler != null) handler();
                }
            }
            base.OnClosing(e);
        }
    }
}
