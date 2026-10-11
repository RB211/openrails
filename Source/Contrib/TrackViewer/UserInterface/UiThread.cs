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
// Threading model of TrackViewer
// ==============================
// TrackViewer runs on two threads:
//  * The game thread (the process main thread). This runs the MonoGame loop, owns the GraphicsDevice and
//    all route/path/editor state, and draws the main window including the in-window menu bar, context
//    menus and status bar.
//  * The Avalonia UI thread (started by UiThread.Start). All Avalonia windows (message boxes, file pickers,
//    search, metadata, wait-point, label editing, chart, other-paths window, ...) live on this thread.
//
// Rules:
//  * Avalonia objects may only be touched on the UI thread: use UiThread.Post / UiThread.Invoke.
//  * Game state may only be touched on the game thread: from the UI thread use GameThread.Post. Queued
//    actions are executed at the start of TrackViewer.Update.
//  * The game thread never blocks waiting for a dialog. Dialogs report their result through a callback
//    that is posted back to the game thread. While a "modal" dialog is open (Dialogs.ModalIsOpen),
//    the game ignores mouse/keyboard input but keeps drawing.

using System;
using System.Collections.Concurrent;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace ORTS.TrackViewer.UserInterface
{
    /// <summary>
    /// The (minimal) Avalonia application object.
    /// </summary>
    public class App : Application
    {
        /// <summary>Set up the theme</summary>
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        }
    }

    /// <summary>
    /// Owns the dedicated Avalonia UI thread and provides marshalling onto it.
    /// </summary>
    public static class UiThread
    {
        private static Thread thread;
        private static readonly CancellationTokenSource shutdown = new CancellationTokenSource();

        /// <summary>True when the Avalonia platform was initialised successfully</summary>
        public static bool IsAvailable { get; private set; }

        /// <summary>Exception (if any) that prevented Avalonia from starting</summary>
        public static Exception StartupError { get; private set; }

        /// <summary>
        /// Start the Avalonia UI thread and wait until the platform has been set up.
        /// Must be called before any other Avalonia API is used (the first thread that touches
        /// the Avalonia dispatcher becomes the UI thread).
        /// </summary>
        public static void Start()
        {
            if (thread != null) return;
            using (var ready = new ManualResetEventSlim(false))
            {
                thread = new Thread(() =>
                {
                    try
                    {
                        AppBuilder.Configure<App>()
                            .UsePlatformDetect()
                            .SetupWithoutStarting();
                        IsAvailable = true;
                    }
                    catch (Exception error)
                    {
                        StartupError = error;
                    }
                    ready.Set();
                    if (!IsAvailable) return;
                    try
                    {
                        Dispatcher.UIThread.MainLoop(shutdown.Token);
                    }
                    catch (OperationCanceledException) { }
                })
                {
                    Name = "Avalonia UI",
                    IsBackground = true,
                };
                thread.Start();
                ready.Wait();
            }
            if (!IsAvailable)
            {
                Console.Error.WriteLine("TrackViewer: Avalonia UI could not be started, dialogs are unavailable: " + StartupError);
            }
        }

        /// <summary>Stop the UI thread (closes all dialogs)</summary>
        public static void Stop()
        {
            if (!IsAvailable) return;
            Post(() =>
            {
                foreach (var window in OpenWindows())
                {
                    try { window.Close(); } catch { }
                }
                shutdown.Cancel();
            });
            thread?.Join(TimeSpan.FromSeconds(2));
        }

        private static Window[] OpenWindows()
        {
            return Dialogs.TrackedWindows();
        }

        /// <summary>Is the calling thread the Avalonia UI thread?</summary>
        public static bool IsOnUiThread => thread != null && Thread.CurrentThread == thread;

        /// <summary>Run an action asynchronously on the Avalonia UI thread</summary>
        public static void Post(Action action)
        {
            if (!IsAvailable) return;
            Dispatcher.UIThread.Post(() =>
            {
                try { action(); }
                catch (Exception error) { Console.Error.WriteLine("TrackViewer UI error: " + error); }
            });
        }

        /// <summary>Run a function on the Avalonia UI thread and wait for the result. Use sparingly, never for dialogs.</summary>
        public static T Invoke<T>(Func<T> function)
        {
            if (!IsAvailable) return default(T);
            if (IsOnUiThread) return function();
            return Dispatcher.UIThread.Invoke(function);
        }
    }

    /// <summary>
    /// Queue of actions that need to be executed on the game (MonoGame) thread.
    /// </summary>
    public static class GameThread
    {
        private static readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();
        private static int gameThreadId = -1;

        /// <summary>Mark the calling thread as the game thread</summary>
        public static void SetCurrentThreadAsGameThread() { gameThreadId = Environment.CurrentManagedThreadId; }

        /// <summary>Is the calling thread the game thread?</summary>
        public static bool IsOnGameThread => Environment.CurrentManagedThreadId == gameThreadId;

        /// <summary>Queue an action to be executed on the game thread (at the start of the next Update)</summary>
        public static void Post(Action action)
        {
            queue.Enqueue(action);
        }

        /// <summary>Execute all queued actions. Only to be called from the game thread.</summary>
        /// <returns>true if any action was executed</returns>
        public static bool RunPending()
        {
            bool any = false;
            // Only run what is queued now; actions queued while running will be done next frame.
            int count = queue.Count;
            while (count-- > 0 && queue.TryDequeue(out Action action))
            {
                any = true;
                action();
            }
            return any;
        }
    }
}
