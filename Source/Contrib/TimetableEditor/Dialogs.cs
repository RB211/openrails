using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Orts.TimetableEditor
{
    /// <summary>
    /// Helpers replacing the synchronous WPF dialog APIs (Window.ShowDialog, MessageBox.Show),
    /// which in Avalonia are asynchronous and need an owner window.
    /// </summary>
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

        /// <summary>Show <paramref name="window"/> modally; returns true when it was closed with <c>Close(true)</c>.</summary>
        public static async Task<bool> ShowDialog(Window window)
        {
            var owner = Owner;
            if (owner == null || owner == window)
            {
                var tcs = new TaskCompletionSource<bool>();
                window.Closed += (s, e) => tcs.TrySetResult(false);
                window.Show();
                await tcs.Task;
                return false;
            }
            var result = await window.ShowDialog<object>(owner);
            return result is bool b && b;
        }

        public static Task Show(string message)
        {
            return ShowMessage(message, "", false);
        }

        public static Task Show(string message, string title)
        {
            return ShowMessage(message, title, false);
        }

        /// <summary>Equivalent of MessageBox.Show(message, title, MessageBoxButton.YesNo) == MessageBoxResult.Yes.</summary>
        public static Task<bool> AskYesNo(string message, string title)
        {
            return ShowMessage(message, title, true);
        }

        static async Task<bool> ShowMessage(string message, string title, bool yesNo)
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
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
            if (yesNo)
            {
                var yes = new Button { Content = "Yes", MinWidth = 70, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
                yes.Click += (s, e) => dialog.Close(true);
                var no = new Button { Content = "No", MinWidth = 70, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };
                no.Click += (s, e) => dialog.Close(false);
                buttons.Children.Add(yes);
                buttons.Children.Add(no);
            }
            else
            {
                var ok = new Button { Content = "OK", MinWidth = 70, IsDefault = true, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };
                ok.Click += (s, e) => dialog.Close(true);
                buttons.Children.Add(ok);
            }
            dialog.Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 16,
                Children =
                {
                    new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    buttons,
                },
            };
            return await ShowDialog(dialog);
        }

        /// <summary>
        /// Show a "please wait" window while <paramref name="work"/> runs on a background thread.
        /// </summary>
        public static async Task RunWithWaitingWindow(Action work)
        {
            var waiting = new Views.waiting();
            var owner = Owner;
            if (owner != null)
                waiting.Show(owner);
            else
                waiting.Show();
            try
            {
                await Task.Run(work);
            }
            catch (Exception error)
            {
                await Show(error.Message, "Error");
            }
            finally
            {
                waiting.Close();
            }
        }
    }

    /// <summary>
    /// MSTS content was authored on Windows and uses arbitrary casing for file and folder names
    /// (e.g. "ACTIVITIES\OpenRails" vs "Activities\openrails", "*.CON" vs "*.con").
    /// These helpers resolve such paths on case-sensitive file systems.
    /// </summary>
    public static class PathCase
    {
        static readonly EnumerationOptions InsensitiveOptions = new EnumerationOptions
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
        };

        /// <summary>
        /// Combine <paramref name="basePath"/> with relative <paramref name="parts"/> (which may themselves contain
        /// '\' or '/' separators), matching each component case-insensitively against what exists on disk.
        /// Components that do not exist are appended as given.
        /// </summary>
        public static string Combine(string basePath, params string[] parts)
        {
            var path = basePath;
            foreach (var part in parts.SelectMany(p => p.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)))
            {
                var candidate = Path.Combine(path, part);
                if (!OperatingSystem.IsWindows() && !Path.Exists(candidate) && Directory.Exists(path) && part.IndexOfAny(new[] { '*', '?' }) < 0)
                {
                    var match = Directory.EnumerateFileSystemEntries(path, part, InsensitiveOptions).FirstOrDefault();
                    if (match != null)
                        candidate = match;
                }
                path = candidate;
            }
            return path;
        }

        /// <summary>Case-insensitive equivalent of <see cref="Directory.GetFiles(string, string)"/>.</summary>
        public static string[] GetFiles(string path, string searchPattern)
        {
            if (!Directory.Exists(path))
                return new string[0];
            return Directory.GetFiles(path, searchPattern, InsensitiveOptions);
        }
    }
}
