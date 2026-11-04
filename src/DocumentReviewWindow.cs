// Review and edit translated document passages before Qalam writes the file.
// Closing the window or pressing Cancel leaves the original untouched.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Qalam
{
    internal sealed class DocumentReviewWindow : Window
    {
        public DocumentReviewWindow(string fileName, string target, IList<DocumentSegment> segments)
        {
            string targetName = DocumentTranslationService.LanguageName(target);
            bool rightToLeft = target == "ar" || target == "fa" || target == "ur" || target == "he";

            Title = "Review translated document";
            Width = 840;
            Height = 680;
            MinWidth = 540;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            ShowInTaskbar = true;
            Background = Ui.Window;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            Border root = new Border();
            root.Background = Ui.Window;
            root.BorderBrush = Ui.Outline;
            root.BorderThickness = new Thickness(1);
            root.Padding = new Thickness(24, 20, 24, 20);
            Content = root;

            DockPanel layout = new DockPanel();
            layout.LastChildFill = true;
            root.Child = layout;

            StackPanel header = new StackPanel();
            DockPanel.SetDock(header, Dock.Top);
            header.Children.Add(Ui.Label("QALAM  \u00b7  REVIEW", 10, Ui.Gold, FontWeights.SemiBold));

            TextBlock title = Ui.Label("Check the translation before saving", 21, Ui.Bright, FontWeights.SemiBold);
            title.Margin = new Thickness(0, 8, 0, 0);
            header.Children.Add(title);

            TextBlock file = Ui.Label(fileName + "  \u2192  " + targetName, 11.5, Ui.Faint, FontWeights.Normal);
            file.TextTrimming = TextTrimming.CharacterEllipsis;
            file.Margin = new Thickness(0, 4, 0, 0);
            header.Children.Add(file);

            TextBlock note = Ui.Label(
                "Edit any passage below. Clearing a passage leaves it in the original language. "
                + "Qalam writes the file only when you choose Save.",
                11, Ui.Muted, FontWeights.Normal);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 10, 0, 12);
            header.Children.Add(note);
            layout.Children.Add(header);

            StackPanel footer = new StackPanel();
            footer.Orientation = Orientation.Horizontal;
            footer.HorizontalAlignment = HorizontalAlignment.Right;
            footer.Margin = new Thickness(0, 14, 0, 0);
            DockPanel.SetDock(footer, Dock.Bottom);

            Button cancel = Ui.SmallOutlineButton("Cancel", delegate
            {
                DialogResult = false;
                Close();
            });
            footer.Children.Add(cancel);

            Button save = Ui.PrimaryButton("Save translated document", delegate
            {
                DialogResult = true;
                Close();
            });
            save.Margin = new Thickness(10, 0, 0, 0);
            footer.Children.Add(save);
            layout.Children.Add(footer);

            ListBox list = new ListBox();
            list.Background = Ui.Deep;
            list.BorderBrush = Ui.InputBorder;
            list.BorderThickness = new Thickness(1);
            list.Foreground = Ui.Bright;
            list.Padding = new Thickness(8);
            list.SelectionMode = SelectionMode.Single;
            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
            VirtualizingStackPanel.SetIsVirtualizing(list, true);
            VirtualizingStackPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
            list.ItemTemplate = BuildTemplate(rightToLeft);
            list.ItemContainerStyle = BuildItemStyle();
            list.ItemsSource = segments;
            layout.Children.Add(list);
        }

        private static DataTemplate BuildTemplate(bool rightToLeft)
        {
            FrameworkElementFactory row = new FrameworkElementFactory(typeof(StackPanel));
            row.SetValue(StackPanel.MarginProperty, new Thickness(0, 0, 0, 4));

            FrameworkElementFactory label = new FrameworkElementFactory(typeof(TextBlock));
            label.SetBinding(TextBlock.TextProperty, new Binding("Label"));
            label.SetValue(TextBlock.ForegroundProperty, Ui.Gold);
            label.SetValue(TextBlock.FontSizeProperty, 9.5);
            label.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            row.AppendChild(label);

            FrameworkElementFactory source = new FrameworkElementFactory(typeof(TextBlock));
            source.SetBinding(TextBlock.TextProperty, new Binding("Source"));
            source.SetValue(TextBlock.ForegroundProperty, Ui.Muted);
            source.SetValue(TextBlock.FontSizeProperty, 11.0);
            source.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            source.SetValue(TextBlock.MarginProperty, new Thickness(0, 2, 0, 5));
            row.AppendChild(source);

            FrameworkElementFactory box = new FrameworkElementFactory(typeof(TextBox));
            Binding binding = new Binding("Translation");
            binding.Mode = BindingMode.TwoWay;
            binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            box.SetBinding(TextBox.TextProperty, binding);
            box.SetValue(TextBox.AcceptsReturnProperty, true);
            box.SetValue(TextBox.TextWrappingProperty, TextWrapping.Wrap);
            box.SetValue(TextBox.MinHeightProperty, 48.0);
            box.SetValue(TextBox.PaddingProperty, new Thickness(7, 5, 7, 5));
            box.SetValue(TextBox.FontSizeProperty, 12.5);
            box.SetValue(TextBox.BackgroundProperty, Ui.Input);
            box.SetValue(TextBox.ForegroundProperty, Ui.Bright);
            box.SetValue(TextBox.BorderBrushProperty, Ui.InputBorder);
            box.SetValue(TextBox.BorderThicknessProperty, new Thickness(1));
            box.SetValue(TextBox.CaretBrushProperty, Ui.Gold);
            box.SetValue(TextBox.SelectionBrushProperty, Ui.Selection);
            if (rightToLeft)
                box.SetValue(TextBox.FlowDirectionProperty, FlowDirection.RightToLeft);
            row.AppendChild(box);

            DataTemplate template = new DataTemplate();
            template.VisualTree = row;
            return template;
        }

        private static Style BuildItemStyle()
        {
            Style style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(ListBoxItem.FocusableProperty, false));
            style.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));

            ControlTemplate container = new ControlTemplate(typeof(ListBoxItem));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border), "Bd");
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            border.SetValue(Border.PaddingProperty, new Thickness(8, 6, 8, 8));
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            border.AppendChild(presenter);
            container.VisualTree = border;

            Trigger selected = new Trigger();
            selected.Property = ListBoxItem.IsSelectedProperty;
            selected.Value = true;
            selected.Setters.Add(new Setter(Border.BackgroundProperty, Ui.SelectedRow, "Bd"));
            container.Triggers.Add(selected);
            style.Setters.Add(new Setter(ListBoxItem.TemplateProperty, container));
            return style;
        }
    }
}
