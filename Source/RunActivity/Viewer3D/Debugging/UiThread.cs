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
// Threading model of the RunActivity tool windows (map window, sound debug window)
// ================================================================================
//  * The MonoGame loop runs on the "Render" thread (process main thread); the simulation runs on the
//    "Updater" thread.
//  * The Avalonia tool windows live on their own "Avalonia UI" thread, started by UiThread.Start.
//    Avalonia objects may only be touched on that thread: use UiThread.Post.
//  * Like the WinForms versions did from the render thread, the windows read simulator state directly
//    from the UI thread (read-only, tolerant to concurrent changes).
//  * Changes to game state requested from the windows (throwing switches, setting signals, multiplayer
//    messages, ...) are queued and executed on the Updater thread by Viewer.Update.
//  * The game never blocks waiting for the UI thread, so a busy or dead UI thread cannot hang the game.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace Orts.Viewer3D.Debugging
{
    /// <summary>
    /// The (minimal) Avalonia application object of RunActivity.
    /// </summary>
    public class ToolWindowsApp : Application
    {
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
        static Thread thread;
        static readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        static readonly List<Window> windows = new List<Window>();

        /// <summary>True when the Avalonia platform was initialised successfully</summary>
        public static bool IsAvailable { get; private set; }

        /// <summary>
        /// Start the Avalonia UI thread and wait until the platform has been set up.
        /// Should be called early, before the game window is created.
        /// </summary>
        public static void Start()
        {
            if (thread != null) return;
            using (var ready = new ManualResetEventSlim(false))
            {
                Exception startupError = null;
                thread = new Thread(() =>
                {
                    try
                    {
                        AppBuilder.Configure<ToolWindowsApp>()
                            .UsePlatformDetect()
                            .SetupWithoutStarting();
                        IsAvailable = true;
                    }
                    catch (Exception error)
                    {
                        startupError = error;
                    }
                    ready.Set();
                    if (!IsAvailable) return;
                    try
                    {
                        Dispatcher.UIThread.MainLoop(shutdown.Token);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception error)
                    {
                        Trace.TraceWarning("Avalonia UI thread terminated: {0}", error);
                    }
                })
                {
                    Name = "Avalonia UI",
                    IsBackground = true,
                };
                thread.Start();
                // Never wait forever: a broken display connection must not stop the game from starting.
                ready.Wait(TimeSpan.FromSeconds(10));
                if (!IsAvailable)
                    Trace.TraceWarning("Avalonia UI could not be started, the map and sound debug windows are unavailable: {0}", startupError?.Message ?? "timeout");
            }
        }

        /// <summary>
        /// Close all tool windows and stop the UI thread. Waits at most a short time.
        /// </summary>
        public static void Stop()
        {
            if (!IsAvailable || thread == null) return;
            Post(() =>
            {
                Window[] open;
                lock (windows) open = windows.ToArray();
                foreach (var window in open)
                {
                    try { window.Close(); } catch { }
                }
                shutdown.Cancel();
            });
            // The thread is a background thread, so even if this times out the process can exit.
            thread.Join(TimeSpan.FromSeconds(2));
        }

        /// <summary>Is the calling thread the Avalonia UI thread?</summary>
        public static bool IsOnUiThread => thread != null && Thread.CurrentThread == thread;

        /// <summary>Run an action asynchronously on the Avalonia UI thread</summary>
        public static void Post(Action action)
        {
            if (!IsAvailable || shutdown.IsCancellationRequested) return;
            Dispatcher.UIThread.Post(() =>
            {
                try { action(); }
                catch (Exception error) { Trace.TraceWarning("Tool window error: {0}", error); }
            });
        }

        /// <summary>Remember a window so that it is closed when the UI thread stops. UI thread only.</summary>
        public static void Track(Window window)
        {
            lock (windows) windows.Add(window);
            window.Closed += (s, e) => { lock (windows) windows.Remove(window); };
        }
    }
}
