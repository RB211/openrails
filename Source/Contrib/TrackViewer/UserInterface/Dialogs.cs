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
//
// Replacement for the WinForms/WPF MessageBox and file dialogs, implemented with Avalonia.
// See UiThread.cs for the threading model. All public methods can be called from the game thread;
// results are delivered through callbacks that run on the game thread.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace ORTS.TrackViewer.UserInterface
{
    /// <summary>Buttons to show in a message box</summary>
    public enum MessageButtons
    {
        /// <summary>Only OK</summary>
        OK,
        /// <summary>OK and Cancel</summary>
        OKCancel,
        /// <summary>Yes and No</summary>
        YesNo,
        /// <summary>Yes, No and Cancel</summary>
        YesNoCancel,
    }

    /// <summary>Result of a message box</summary>
    public enum MessageResult
    {
        /// <summary>Window was closed without pressing a button</summary>
        None,
        /// <summary>OK was pressed</summary>
        OK,
        /// <summary>Cancel was pressed</summary>
        Cancel,
        /// <summary>Yes was pressed</summary>
        Yes,
        /// <summary>No was pressed</summary>
        No,
    }

    /// <summary>
    /// Message boxes, file pickers and generic (modal-like) windows.
    /// </summary>
    public static class Dialogs
    {
        private static int modalCount;
        private static readonly List<Window> trackedWindows = new List<Window>();
        private static Window storageHost;

        /// <summary>Default title for message boxes</summary>
        public const string DefaultTitle = "TrackViewer";

        /// <summary>
        /// True while a dialog is open that, in the original Windows version, was modal.
        /// The game should not act on mouse/keyboard input during that time.
        /// </summary>
        public static bool ModalIsOpen => Volatile.Read(ref modalCount) > 0;

        /// <summary>All windows that are currently open (UI thread only)</summary>
        internal static Window[] TrackedWindows()
        {
            lock (trackedWindows) { return trackedWindows.ToArray(); }
        }

        #region Generic windows
        /// <summary>
        /// Show a (non-modal) window. Creation happens on the UI thread.
        /// </summary>
        /// <param name="createWindow">Function creating the window (runs on the UI thread)</param>
        /// <param name="onClosed">Optional callback (runs on the UI thread) when the window is closed</param>
        public static void Show(Func<Window> createWindow, Action<Window> onClosed = null)
        {
            ShowInternal(createWindow, onClosed, modal: false);
        }

        /// <summary>
        /// Show a window that behaves like a modal dialog: while it is open, the game ignores input.
        /// The game thread does not block.
        /// </summary>
        /// <param name="createWindow">Function creating the window (runs on the UI thread)</param>
        /// <param name="onClosed">Callback (runs on the UI thread) when the window is closed. Read the results
        /// from the window here and GameThread.Post whatever needs to be done with them.</param>
        public static void ShowModal(Func<Window> createWindow, Action<Window> onClosed)
        {
            ShowInternal(createWindow, onClosed, modal: true);
        }

        private static void ShowInternal(Func<Window> createWindow, Action<Window> onClosed, bool modal)
        {
            if (!UiThread.IsAvailable)
            {
                Console.Error.WriteLine("TrackViewer: cannot show window, no UI available");
                return;
            }
            if (modal) Interlocked.Increment(ref modalCount);
            UiThread.Post(() =>
            {
                Window window;
                try
                {
                    window = createWindow();
                }
                catch
                {
                    if (modal) Interlocked.Decrement(ref modalCount);
                    throw;
                }
                lock (trackedWindows) { trackedWindows.Add(window); }
                window.Closed += (sender, e) =>
                {
                    lock (trackedWindows) { trackedWindows.Remove(window); }
                    try
                    {
                        onClosed?.Invoke(window);
                    }
                    finally
                    {
                        if (modal) Interlocked.Decrement(ref modalCount);
                    }
                };
                if (modal)
                {
                    window.Topmost = true;
                }
                window.Show();
                window.Activate();
            });
        }
        #endregion

        #region Message boxes
        /// <summary>
        /// Show an informational message. Does not block and does not stop the game from handling input.
        /// </summary>
        public static void ShowMessage(string message, string title = DefaultTitle)
        {
            Console.Error.WriteLine("TrackViewer: " + message);
            ShowMessageBox(message, title, MessageButtons.OK, null, modal: false);
        }

        /// <summary>
        /// Ask a question. The answer is delivered on the game thread.
        /// </summary>
        /// <param name="message">The question</param>
        /// <param name="title">Window title</param>
        /// <param name="buttons">Which buttons to show</param>
        /// <param name="onAnswer">Callback on the game thread with the button that was pressed</param>
        public static void Ask(string message, string title, MessageButtons buttons, Action<MessageResult> onAnswer)
        {
            if (!UiThread.IsAvailable)
            {
                // Without UI, assume the safe answer.
                MessageResult fallback = (buttons == MessageButtons.YesNo || buttons == MessageButtons.YesNoCancel) ? MessageResult.No : MessageResult.Cancel;
                GameThread.Post(() => onAnswer(fallback));
                return;
            }
            ShowMessageBox(message, title, buttons, onAnswer, modal: true);
        }

        private static void ShowMessageBox(string message, string title, MessageButtons buttons, Action<MessageResult> onAnswer, bool modal)
        {
            MessageResult result = MessageResult.None;
            Func<Window> create = () =>
            {
                var window = new Window
                {
                    Title = title ?? DefaultTitle,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    MaxWidth = 900,
                    CanResize = false,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                };
                var text = new SelectableTextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Thickness(16, 16, 16, 8),
                    MaxWidth = 860,
                };
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Thickness(16, 8, 16, 16),
                };
                void AddButton(string label, MessageResult buttonResult, bool isDefault, bool isCancel)
                {
                    var button = new Button { Content = TrackViewer.catalog.GetString(label), IsDefault = isDefault, IsCancel = isCancel, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
                    button.Click += (s, e) => { result = buttonResult; window.Close(); };
                    buttonPanel.Children.Add(button);
                }
                switch (buttons)
                {
                    case MessageButtons.OK:
                        AddButton("OK", MessageResult.OK, true, true);
                        break;
                    case MessageButtons.OKCancel:
                        AddButton("OK", MessageResult.OK, true, false);
                        AddButton("Cancel", MessageResult.Cancel, false, true);
                        break;
                    case MessageButtons.YesNo:
                        AddButton("Yes", MessageResult.Yes, true, false);
                        AddButton("No", MessageResult.No, false, true);
                        break;
                    case MessageButtons.YesNoCancel:
                        AddButton("Yes", MessageResult.Yes, true, false);
                        AddButton("No", MessageResult.No, false, false);
                        AddButton("Cancel", MessageResult.Cancel, false, true);
                        break;
                }
                var panel = new StackPanel();
                panel.Children.Add(text);
                panel.Children.Add(buttonPanel);
                window.Content = panel;
                return window;
            };
            Action<Window> closed = (window) =>
            {
                if (onAnswer == null) return;
                MessageResult answer = result;
                if (answer == MessageResult.None)
                {   // closed via window manager: treat as cancel/no
                    answer = (buttons == MessageButtons.YesNo) ? MessageResult.No : MessageResult.Cancel;
                    if (buttons == MessageButtons.OK) answer = MessageResult.OK;
                }
                GameThread.Post(() => onAnswer(answer));
            };
            ShowInternal(create, closed, modal);
        }
        #endregion

        #region File and folder pickers
        private static IStorageProvider GetStorageProvider()
        {
            // The storage provider needs a TopLevel. Use any open window, or a hidden host window.
            Window anyWindow = TrackedWindows().FirstOrDefault(w => w.IsVisible);
            if (anyWindow != null) return anyWindow.StorageProvider;
            if (storageHost == null)
            {
                storageHost = new Window { Title = "TrackViewer", Width = 1, Height = 1, ShowInTaskbar = false };
            }
            return storageHost.StorageProvider;
        }

        private static async Task<IStorageFolder> FolderFromPath(IStorageProvider provider, string directory)
        {
            if (String.IsNullOrEmpty(directory) || !System.IO.Directory.Exists(directory)) return null;
            try { return await provider.TryGetFolderFromPathAsync(directory); }
            catch { return null; }
        }

        private static void RunPicker(Func<IStorageProvider, Task<string>> picker, Action<string> onResult)
        {
            if (!UiThread.IsAvailable)
            {
                GameThread.Post(() => onResult(String.Empty));
                return;
            }
            Interlocked.Increment(ref modalCount);
            UiThread.Post(async () =>
            {
                string result = String.Empty;
                try
                {
                    result = await picker(GetStorageProvider()) ?? String.Empty;
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine("TrackViewer: file dialog failed: " + error.Message);
                }
                finally
                {
                    Interlocked.Decrement(ref modalCount);
                }
                GameThread.Post(() => onResult(result));
            });
        }

        /// <summary>
        /// Ask the user for a file name to save to.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="suggestedFileName">Default file name</param>
        /// <param name="extension">Default extension, including the dot, e.g. ".pat"</param>
        /// <param name="filterName">Human-readable name of the file type</param>
        /// <param name="initialDirectory">Directory to start in (may be null)</param>
        /// <param name="onResult">Callback on the game thread with the full path, or empty if cancelled</param>
        public static void SaveFile(string title, string suggestedFileName, string extension, string filterName, string initialDirectory, Action<string> onResult)
        {
            RunPicker(async (provider) =>
            {
                var options = new FilePickerSaveOptions
                {
                    Title = title,
                    SuggestedFileName = suggestedFileName,
                    DefaultExtension = extension.TrimStart('.'),
                    ShowOverwritePrompt = true,
                    FileTypeChoices = new[] { new FilePickerFileType(filterName) { Patterns = new[] { "*" + extension } } },
                    SuggestedStartLocation = await FolderFromPath(provider, initialDirectory),
                };
                IStorageFile file = await provider.SaveFilePickerAsync(options);
                string path = file?.TryGetLocalPath();
                if (!String.IsNullOrEmpty(path) && String.IsNullOrEmpty(System.IO.Path.GetExtension(path)))
                {
                    path += extension;
                }
                return path;
            }, onResult);
        }

        /// <summary>
        /// Ask the user for an existing file to open.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="extension">Extension, including the dot, e.g. ".json"</param>
        /// <param name="filterName">Human-readable name of the file type</param>
        /// <param name="initialDirectory">Directory to start in (may be null)</param>
        /// <param name="onResult">Callback on the game thread with the full path, or empty if cancelled</param>
        public static void OpenFile(string title, string extension, string filterName, string initialDirectory, Action<string> onResult)
        {
            RunPicker(async (provider) =>
            {
                var options = new FilePickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false,
                    FileTypeFilter = new[] { new FilePickerFileType(filterName) { Patterns = new[] { "*" + extension } }, FilePickerFileTypes.All },
                    SuggestedStartLocation = await FolderFromPath(provider, initialDirectory),
                };
                IReadOnlyList<IStorageFile> files = await provider.OpenFilePickerAsync(options);
                return files?.FirstOrDefault()?.TryGetLocalPath();
            }, onResult);
        }

        /// <summary>
        /// Ask the user for a folder.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="initialDirectory">Directory to start in (may be null)</param>
        /// <param name="onResult">Callback on the game thread with the full path, or empty if cancelled</param>
        public static void PickFolder(string title, string initialDirectory, Action<string> onResult)
        {
            RunPicker(async (provider) =>
            {
                var options = new FolderPickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false,
                    SuggestedStartLocation = await FolderFromPath(provider, initialDirectory),
                };
                IReadOnlyList<IStorageFolder> folders = await provider.OpenFolderPickerAsync(options);
                return folders?.FirstOrDefault()?.TryGetLocalPath();
            }, onResult);
        }
        #endregion

        #region Helpers for dialog windows
        /// <summary>
        /// Place a window near the given position in the game window client area. Positions are only a hint;
        /// on Wayland the compositor decides.
        /// </summary>
        public static void PlaceNear(Window window, int x, int y)
        {
            if (x <= 0 && y <= 0)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                return;
            }
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = new PixelPoint(x, y);
        }

        /// <summary>
        /// Install a handler that only allows digits to be typed in the textbox.
        /// </summary>
        public static void DigitsOnly(TextBox textBox)
        {
            textBox.AddHandler(InputElement.TextInputEvent, (object sender, TextInputEventArgs e) =>
            {
                if (e.Text != null && e.Text.Any(c => !char.IsDigit(c))) e.Handled = true;
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }
        #endregion
    }
}
