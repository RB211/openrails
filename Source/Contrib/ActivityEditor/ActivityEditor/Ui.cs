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

// Small helpers replacing WinForms features which Avalonia does not have built in:
// GroupBox, MessageBox, synchronous modal dialogs and the strongly typed image resources.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ActivityEditor
{
    /// <summary>
    /// A titled frame, equivalent to System.Windows.Forms.GroupBox.
    /// </summary>
    public class GroupBox : Border
    {
        readonly TextBlock HeaderText = new TextBlock { FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
        readonly DockPanel Panel = new DockPanel();
        Control content;

        public GroupBox()
        {
            BorderThickness = new Thickness(1);
            BorderBrush = Brushes.Gray;
            CornerRadius = new CornerRadius(3);
            Padding = new Thickness(6, 4);
            Margin = new Thickness(2);
            DockPanel.SetDock(HeaderText, Dock.Top);
            Panel.Children.Add(HeaderText);
            Child = Panel;
        }

        public string Text
        {
            get { return HeaderText.Text; }
            set { HeaderText.Text = value; }
        }

        public Control Content
        {
            get { return content; }
            set
            {
                if (content != null)
                    Panel.Children.Remove(content);
                content = value;
                if (content != null)
                    Panel.Children.Add(content);
            }
        }
    }

    public static class Dialogs
    {
        /// <summary>The window that should own a new dialog: the active window, else the main window.</summary>
        public static Window Owner
        {
            get
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    return desktop.Windows.LastOrDefault(w => w.IsActive) ?? desktop.Windows.LastOrDefault(w => w.IsVisible) ?? desktop.MainWindow;
                return null;
            }
        }

        /// <summary>
        /// Show <paramref name="window"/> modally (replacement for Form.ShowDialog()); returns the value passed to Close(result).
        /// </summary>
        public static async Task<object> ShowDialog(Window window)
        {
            var owner = Owner;
            if (owner == null || owner == window)
            {
                var tcs = new TaskCompletionSource<object>();
                window.Closed += (s, e) => tcs.TrySetResult(null);
                window.Show();
                return await tcs.Task;
            }
            return await window.ShowDialog<object>(owner);
        }

        public static Task Show(string message, string title = "")
        {
            return ShowMessage(message, title, new[] { "OK" });
        }

        /// <summary>Equivalent of MessageBox.Show(message, title, MessageBoxButtons.OKCancel) == DialogResult.OK.</summary>
        public static async Task<bool> AskOkCancel(string message, string title)
        {
            return await ShowMessage(message, title, new[] { "OK", "Cancel" }) == "OK";
        }

        static async Task<string> ShowMessage(string message, string title, string[] buttons)
        {
            var dialog = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                MinWidth = 250,
                MaxWidth = 700,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
            foreach (var text in buttons)
            {
                var button = new Button { Content = text, MinWidth = 70, HorizontalContentAlignment = HorizontalAlignment.Center };
                button.IsDefault = text == buttons[0];
                button.IsCancel = text == buttons[buttons.Length - 1];
                button.Click += (s, e) => dialog.Close(text);
                buttonPanel.Children.Add(button);
            }
            dialog.Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 16,
                Children =
                {
                    new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    buttonPanel,
                },
            };
            return await ShowDialog(dialog) as string;
        }
    }

    /// <summary>
    /// Images formerly in Properties/Resources.resx.
    /// </summary>
    public static class AppResources
    {
        static readonly Dictionary<string, Bitmap> Cache = new Dictionary<string, Bitmap>();

        public static Bitmap Load(string path)
        {
            if (!Cache.TryGetValue(path, out var bitmap))
            {
                try
                {
                    using (var stream = AssetLoader.Open(new Uri("avares://Contrib.ActivityEditor/" + path)))
                        bitmap = new Bitmap(stream);
                }
                catch (Exception error)
                {
                    System.Diagnostics.Trace.TraceWarning("Unable to load resource {0}: {1}", path, error.Message);
                    bitmap = null;
                }
                Cache[path] = bitmap;
            }
            return bitmap;
        }

        public static Drawing.Image LoadImage(string path)
        {
            var bitmap = Load(path);
            return bitmap == null ? null : new Drawing.Image(bitmap);
        }

        public static Bitmap _12 => Load("Resources/12.png");
        public static Bitmap _31 => Load("Resources/31.png");
        public static Bitmap _32 => Load("Resources/32.png");
        public static Bitmap _64 => Load("Resources/64.png");
        public static Bitmap _77 => Load("Resources/77.png");
        public static Bitmap _78 => Load("Resources/78.png");
        public static Bitmap _98 => Load("Resources/98.png");
        public static Bitmap _118 => Load("Resources/118.png");
        public static Bitmap metasegment => Load("Resources/metasegment.png");
        public static Bitmap object_move => Load("icon/object_move.png");
        public static Bitmap object_rotate => Load("icon/object_rotate.png");
        public static Bitmap tag => Load("icon/tag.gif");
        public static Bitmap SignalBox => Load("icon/SignalBox.ico");
        public static Bitmap Activity_start => Load("icon/Activity_start.png");
        public static Bitmap Activity_stop => Load("icon/Activity_stop.png");
        public static Bitmap Activity_wait => Load("icon/Activity_wait.png");
        public static Bitmap Activity_check => Load("icon/Activity_check.png");
        public static Bitmap Action => Load("icon/Action.png");
    }
}
