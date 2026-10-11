// COPYRIGHT 2026 by the Open Rails project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;

namespace Menu
{
    public enum MessageBoxButtons { OK, OKCancel, YesNo }

    public enum MessageBoxResult { None, OK, Cancel, Yes, No }

    /// <summary>
    /// A minimal replacement for the WinForms MessageBox (Avalonia has none).
    /// </summary>
    public class MessageBox : Window
    {
        MessageBoxResult Result = MessageBoxResult.None;

        MessageBox(string text, string caption, MessageBoxButtons buttons, MessageBoxResult defaultResult)
        {
            Title = caption ?? App.ProductName;
            Icon = App.LoadIcon();
            SizeToContent = SizeToContent.WidthAndHeight;
            CanResize = false;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            MinWidth = 300;

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 16, 0, 0) };
            switch (buttons)
            {
                case MessageBoxButtons.OK:
                    AddButton(buttonPanel, Localization.GetString("OK"), MessageBoxResult.OK, defaultResult, isCancel: true);
                    break;
                case MessageBoxButtons.OKCancel:
                    AddButton(buttonPanel, Localization.GetString("OK"), MessageBoxResult.OK, defaultResult);
                    AddButton(buttonPanel, Localization.GetString("Cancel"), MessageBoxResult.Cancel, defaultResult, isCancel: true);
                    break;
                case MessageBoxButtons.YesNo:
                    AddButton(buttonPanel, Localization.GetString("Yes"), MessageBoxResult.Yes, defaultResult);
                    AddButton(buttonPanel, Localization.GetString("No"), MessageBoxResult.No, defaultResult, isCancel: true);
                    break;
            }

            var message = new SelectableTextBlock
            {
                Text = text,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 640,
            };
            var scroller = new ScrollViewer { Content = message, MaxHeight = 500, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };

            Content = new DockPanel
            {
                Margin = new Thickness(16),
                Children = { buttonPanel, scroller },
            };
            DockPanel.SetDock(buttonPanel, Dock.Bottom);
        }

        void AddButton(StackPanel panel, string text, MessageBoxResult result, MessageBoxResult defaultResult, bool isCancel = false)
        {
            var button = new Button { Content = text, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, IsCancel = isCancel };
            button.IsDefault = defaultResult == MessageBoxResult.None ? panel.Children.Count == 0 : result == defaultResult;
            button.Click += (sender, e) =>
            {
                Result = result;
                Close();
            };
            panel.Children.Add(button);
            if (button.IsDefault)
                Opened += (sender, e) => button.Focus();
        }

        /// <summary>
        /// Shows a modal message box. When <paramref name="owner"/> is null the main window (or any visible window) is used.
        /// </summary>
        public static async System.Threading.Tasks.Task<MessageBoxResult> Show(Window owner, string text, string caption = null, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            var box = new MessageBox(text, caption, buttons, defaultResult);
            owner ??= GetDefaultOwner();
            if (owner != null && owner.IsVisible)
            {
                await box.ShowDialog(owner);
            }
            else
            {
                var closed = new System.Threading.Tasks.TaskCompletionSource<bool>();
                box.Closed += (sender, e) => closed.TrySetResult(true);
                box.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                box.Show();
                await closed.Task;
            }
            // Closing with the window manager is the same as cancelling.
            if (box.Result == MessageBoxResult.None)
                box.Result = buttons == MessageBoxButtons.YesNo ? MessageBoxResult.No : buttons == MessageBoxButtons.OKCancel ? MessageBoxResult.Cancel : MessageBoxResult.OK;
            return box.Result;
        }

        static Window GetDefaultOwner()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.Windows.LastOrDefault(w => w.IsActive && w.IsVisible) ?? (desktop.MainWindow?.IsVisible == true ? desktop.MainWindow : null);
            return null;
        }
    }
}
